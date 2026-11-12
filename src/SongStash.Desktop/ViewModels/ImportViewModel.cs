using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Importing;
using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed partial class ImportViewModel : ObservableObject, IDisposable
{
    private readonly IImportOrchestrator orchestrator;
    private readonly IFileImportSourceReaderFactory? fileReaderFactory;
    private readonly SettingsViewModel? settings;
    private Func<ImportUiText, string>? errorText;

    public ImportViewModel(IImportOrchestrator orchestrator, IFileImportSourceReaderFactory? fileReaderFactory = null,
        SettingsViewModel? settings = null)
    {
        this.orchestrator = orchestrator;
        this.fileReaderFactory = fileReaderFactory;
        this.settings = settings;
        if (settings is not null) settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public ObservableCollection<ImportPreviewRowViewModel> PreviewItems { get; } = [];
    public ObservableCollection<ImportDiagnostic> Diagnostics { get; } = [];
    public ObservableCollection<ImportDiagnosticRowViewModel> DiagnosticRows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(IsImporting))]
    [NotifyPropertyChangedFor(nameof(IsCancelled))]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    [NotifyPropertyChangedFor(nameof(IsUnavailable))]
    [NotifyPropertyChangedFor(nameof(IsUnsupportedSchema))]
    [NotifyPropertyChangedFor(nameof(IsReadError))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private ImportWorkflowState state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyPropertyChangedFor(nameof(CandidateCountLabel))]
    private ImportPreviewSummary? summary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailures))]
    [NotifyPropertyChangedFor(nameof(HasFailedItems))]
    [NotifyPropertyChangedFor(nameof(HasNeedsAttentionItems))]
    [NotifyPropertyChangedFor(nameof(IsDuplicateOnly))]
    [NotifyPropertyChangedFor(nameof(HasEmptyResult))]
    [NotifyPropertyChangedFor(nameof(FailedItems))]
    [NotifyPropertyChangedFor(nameof(NeedsAttentionItems))]
    [NotifyPropertyChangedFor(nameof(NewSongsCountLabel))]
    [NotifyPropertyChangedFor(nameof(FindEventsCountLabel))]
    [NotifyPropertyChangedFor(nameof(FailedCountLabel))]
    private ImportResult? result;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isFileSource;

    [ObservableProperty]
    private string? selectedFileName;

    private CancellationTokenSource? activeOperation;

    public bool IsBusy => State is ImportWorkflowState.Loading or ImportWorkflowState.Importing;
    public bool IsLoading => State == ImportWorkflowState.Loading;
    public bool IsImporting => State == ImportWorkflowState.Importing;
    public bool IsCancelled => State == ImportWorkflowState.Cancelled;
    public bool IsCompleted => State == ImportWorkflowState.Completed;
    public bool IsUnavailable => State == ImportWorkflowState.SourceUnavailable;
    public bool IsUnsupportedSchema => State == ImportWorkflowState.UnsupportedSchema;
    public bool IsReadError => State == ImportWorkflowState.Error;
    public bool IsLoadingShazam => IsLoading && !IsFileSource;
    public bool IsLoadingFile => IsLoading && IsFileSource;
    public bool IsUnavailableShazam => IsUnavailable && !IsFileSource;
    public bool IsUnavailableFile => IsUnavailable && IsFileSource;
    public bool IsUnsupportedShazam => IsUnsupportedSchema && !IsFileSource;
    public bool IsUnsupportedFile => IsUnsupportedSchema && IsFileSource;
    public bool IsShazamReadError => IsReadError && !IsFileSource;
    public bool IsFileReadError => IsReadError && IsFileSource;
    public bool IsCancelledShazam => IsCancelled && !IsFileSource;
    public bool IsCancelledFile => IsCancelled && IsFileSource;
    public bool ShowShazamDiagnostics => HasDiagnostics && !IsFileSource;
    public bool ShowFileDiagnostics => HasDiagnostics && IsFileSource;
    public bool HasSelectedFileName => !string.IsNullOrWhiteSpace(SelectedFileName);
    public int SelectedCount => PreviewItems.Count(item => item.IsSelected);
    public string SelectedCountLabel => Strings.SelectedItems(SelectedCount);
    public string CandidateCountLabel => Strings.CandidatesFound(Summary?.TotalCandidates ?? 0);
    public string NewSongsCountLabel => Strings.NewSongsCount(Result?.AddedSongs ?? 0);
    public string FindEventsCountLabel => Strings.FindEventsCount(Result?.AddedFindEvents ?? 0);
    public string FailedCountLabel => Strings.FailedCount(Result?.Failed ?? 0);
    public ImportUiText Strings => ImportUiText.For(settings?.Language ?? AppLanguage.English);
    public bool CanImport => State == ImportWorkflowState.PreviewReady && SelectedCount > 0;
    public bool HasDiagnostics => Diagnostics.Count > 0;
    public bool HasFailures => Result?.Failed > 0;
    public bool HasFailedItems => FailedItems.Count > 0;
    public bool HasNeedsAttentionItems => NeedsAttentionItems.Count > 0;
    public bool IsDuplicateOnly => Result is { AddedSongs: 0, AddedFindEvents: 0, SkippedDuplicates: > 0, NeedsAttention: 0, Failed: 0 };
    public bool HasEmptyResult => Result is { Items.Count: 0 };
    public IReadOnlyList<ImportOutcomeRowViewModel> FailedItems => Result?.Items
        .Where(item => item.Status == ImportItemOutcomeStatus.Failed)
        .Select(item => new ImportOutcomeRowViewModel(item, () => Strings)).ToArray() ?? [];
    public IReadOnlyList<ImportOutcomeRowViewModel> NeedsAttentionItems => Result?.Items
        .Where(item => item.Status == ImportItemOutcomeStatus.NeedsAttention)
        .Select(item => new ImportOutcomeRowViewModel(item, () => Strings)).ToArray() ?? [];

    /// <summary>Raised after one or more FindEvents commit successfully, so Library can reload.</summary>
    public event Action? LibraryRefreshRequested;
    public event Action? BackToSourceRequested;
    public event Action? BackToLibraryRequested;

    partial void OnStateChanged(ImportWorkflowState value)
    {
        ImportSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsLoadingShazam));
        OnPropertyChanged(nameof(IsLoadingFile));
        OnPropertyChanged(nameof(IsUnavailableShazam));
        OnPropertyChanged(nameof(IsUnavailableFile));
        OnPropertyChanged(nameof(IsUnsupportedShazam));
        OnPropertyChanged(nameof(IsUnsupportedFile));
        OnPropertyChanged(nameof(IsShazamReadError));
        OnPropertyChanged(nameof(IsFileReadError));
        OnPropertyChanged(nameof(IsCancelledShazam));
        OnPropertyChanged(nameof(IsCancelledFile));
        OnPropertyChanged(nameof(ShowShazamDiagnostics));
        OnPropertyChanged(nameof(ShowFileDiagnostics));
    }

    partial void OnIsFileSourceChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLoadingShazam));
        OnPropertyChanged(nameof(IsLoadingFile));
        OnPropertyChanged(nameof(IsUnavailableShazam));
        OnPropertyChanged(nameof(IsUnavailableFile));
        OnPropertyChanged(nameof(IsUnsupportedShazam));
        OnPropertyChanged(nameof(IsUnsupportedFile));
        OnPropertyChanged(nameof(IsShazamReadError));
        OnPropertyChanged(nameof(IsFileReadError));
        OnPropertyChanged(nameof(IsCancelledShazam));
        OnPropertyChanged(nameof(IsCancelledFile));
        OnPropertyChanged(nameof(ShowShazamDiagnostics));
        OnPropertyChanged(nameof(ShowFileDiagnostics));
    }

    partial void OnSelectedFileNameChanged(string? value) => OnPropertyChanged(nameof(HasSelectedFileName));

    [RelayCommand]
    private async Task ReadPreviewAsync(CancellationToken cancellationToken)
    {
        IsFileSource = false;
        SelectedFileName = null;
        await ReadPreviewCoreAsync(null, cancellationToken);
    }

    public async Task ReadFileAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || IsBusy) return;
        IsFileSource = true;
        SelectedFileName = Path.GetFileName(path);
        if (fileReaderFactory is null)
        {
            ClearTransientState();
            SetErrorMessage(text => text.FileReadFailed);
            State = ImportWorkflowState.Error;
            return;
        }

        IImportSourceReader reader;
        try { reader = fileReaderFactory.Create(path); }
        catch (ArgumentException)
        {
            ClearTransientState();
            SetErrorMessage(text => text.UnsupportedFile);
            State = ImportWorkflowState.UnsupportedSchema;
            return;
        }
        await ReadPreviewCoreAsync(reader, cancellationToken);
    }

    private async Task ReadPreviewCoreAsync(IImportSourceReader? selectedReader, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        ClearTransientState();
        State = ImportWorkflowState.Loading;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        activeOperation = operation;
        try
        {
            var preview = selectedReader is null
                ? await orchestrator.ReadPreviewAsync(operation.Token)
                : await orchestrator.ReadPreviewAsync(selectedReader, operation.Token);
            foreach (var diagnostic in preview.Diagnostics)
            {
                AddDiagnostic(diagnostic);
            }
            NotifyDiagnosticState();

            switch (preview.Status)
            {
                case ImportPreviewRunStatus.Success:
                    foreach (var item in preview.Items)
                    {
                        var row = new ImportPreviewRowViewModel(item, () => Strings);
                        row.SelectionChanged += OnSelectionChanged;
                        PreviewItems.Add(row);
                    }
                    Summary = preview.Summary;
                    State = ImportWorkflowState.PreviewReady;
                    break;
                case ImportPreviewRunStatus.SourceUnavailable:
                    SetErrorMessage(text => IsFileSource ? text.FileUnavailable : text.SourceUnavailable);
                    State = ImportWorkflowState.SourceUnavailable;
                    break;
                case ImportPreviewRunStatus.UnsupportedSchema:
                    SetErrorMessage(text => IsFileSource ? text.UnsupportedFile : text.UnsupportedSchema);
                    State = ImportWorkflowState.UnsupportedSchema;
                    break;
                default:
                    SetErrorMessage(text => IsFileSource ? text.FileReadFailed : text.ReadFailed);
                    State = ImportWorkflowState.Error;
                    break;
            }
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            State = ImportWorkflowState.Cancelled;
        }
        catch (Exception)
        {
            AddDiagnostic(new ImportDiagnostic(ImportDiagnosticSeverity.Error, "import.preview.failed",
                "The import preview could not be prepared."));
            NotifyDiagnosticState();
            SetErrorMessage(text => IsFileSource ? text.FileReadFailed : text.ReadFailed);
            State = ImportWorkflowState.Error;
        }
        finally
        {
            if (ReferenceEquals(activeOperation, operation))
            {
                activeOperation = null;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportSelectedAsync(CancellationToken cancellationToken)
    {
        if (!CanImport || IsBusy)
        {
            return;
        }

        Result = null;
        ClearErrorMessage();
        State = ImportWorkflowState.Importing;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        activeOperation = operation;
        try
        {
            var selected = PreviewItems.Where(item => item.CanSelect && item.IsSelected)
                .Select(item => item.PreviewItem).ToArray();
            Result = await orchestrator.ImportAsync(selected, operation.Token);
            State = ImportWorkflowState.Completed;
            if (Result.AddedFindEvents > 0)
            {
                LibraryRefreshRequested?.Invoke();
            }
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            State = ImportWorkflowState.Cancelled;
            // The import service deliberately does not return partial counts on cancellation.
            // Still notify Library because earlier per-item commits may already have completed.
            LibraryRefreshRequested?.Invoke();
        }
        catch (Exception)
        {
            AddDiagnostic(new ImportDiagnostic(ImportDiagnosticSeverity.Error, "import.operation.failed",
                "The import could not be completed."));
            NotifyDiagnosticState();
            SetErrorMessage(text => text.ImportRunFailed);
            State = ImportWorkflowState.Error;
            // An unexpected orchestration failure may happen after earlier item commits.
            LibraryRefreshRequested?.Invoke();
        }
        finally
        {
            if (ReferenceEquals(activeOperation, operation))
            {
                activeOperation = null;
            }
        }
    }

    [RelayCommand]
    private void Cancel() => activeOperation?.Cancel();

    [RelayCommand]
    private void BackToSource() => BackToSourceRequested?.Invoke();

    [RelayCommand]
    private void BackToLibrary() => BackToLibraryRequested?.Invoke();

    public void ResetToSource()
    {
        if (IsBusy)
        {
            return;
        }

        ClearTransientState();
        IsFileSource = false;
        SelectedFileName = null;
        State = ImportWorkflowState.Idle;
    }

    private void ClearTransientState()
    {
        foreach (var item in PreviewItems)
        {
            item.SelectionChanged -= OnSelectionChanged;
        }
        PreviewItems.Clear();
        Diagnostics.Clear();
        DiagnosticRows.Clear();
        NotifyDiagnosticState();
        Summary = null;
        Result = null;
        ClearErrorMessage();
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountLabel));
        OnPropertyChanged(nameof(CanImport));
        ImportSelectedCommand.NotifyCanExecuteChanged();
    }

    private void NotifyDiagnosticState()
    {
        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(ShowShazamDiagnostics));
        OnPropertyChanged(nameof(ShowFileDiagnostics));
    }

    private void AddDiagnostic(ImportDiagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
        DiagnosticRows.Add(new ImportDiagnosticRowViewModel(diagnostic, () => Strings));
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.Language)) return;
        OnPropertyChanged(nameof(Strings));
        OnPropertyChanged(nameof(SelectedCountLabel));
        OnPropertyChanged(nameof(CandidateCountLabel));
        OnPropertyChanged(nameof(NewSongsCountLabel));
        OnPropertyChanged(nameof(FindEventsCountLabel));
        OnPropertyChanged(nameof(FailedCountLabel));
        OnPropertyChanged(nameof(FailedItems));
        OnPropertyChanged(nameof(NeedsAttentionItems));
        foreach (var item in PreviewItems) item.RefreshStrings();
        foreach (var diagnostic in DiagnosticRows) diagnostic.RefreshStrings();

        if (errorText is not null) ErrorMessage = errorText(Strings);
    }

    private void SetErrorMessage(Func<ImportUiText, string> getText)
    {
        errorText = getText;
        ErrorMessage = getText(Strings);
    }

    private void ClearErrorMessage()
    {
        errorText = null;
        ErrorMessage = null;
    }

    public void Dispose()
    {
        if (settings is not null) settings.PropertyChanged -= OnSettingsPropertyChanged;
        activeOperation?.Cancel();
        activeOperation?.Dispose();
    }
}
