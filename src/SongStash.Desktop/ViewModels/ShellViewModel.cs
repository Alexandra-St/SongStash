using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    public LibraryViewModel Library { get; }
    public OverviewViewModel? Overview { get; }
    public ImportViewModel Import { get; }
    public SettingsViewModel? Settings { get; }
    public ExportViewModel? Export { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewPage))]
    [NotifyPropertyChangedFor(nameof(IsLibraryPage))]
    [NotifyPropertyChangedFor(nameof(IsImportSourcePage))]
    [NotifyPropertyChangedFor(nameof(IsImportPreviewPage))]
    [NotifyPropertyChangedFor(nameof(IsImportingPage))]
    [NotifyPropertyChangedFor(nameof(IsImportResultsPage))]
    [NotifyPropertyChangedFor(nameof(IsImportPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    private ShellPage currentPage = ShellPage.Library;

    public bool IsOverviewPage => CurrentPage == ShellPage.Overview;
    public bool IsLibraryPage => CurrentPage == ShellPage.Library;
    public bool IsImportSourcePage => CurrentPage == ShellPage.ImportSource;
    public bool IsImportPreviewPage => CurrentPage == ShellPage.ImportPreview;
    public bool IsImportingPage => CurrentPage == ShellPage.Importing;
    public bool IsImportResultsPage => CurrentPage == ShellPage.ImportResults;
    public bool IsImportPage => IsImportSourcePage || IsImportPreviewPage || IsImportingPage || IsImportResultsPage;
    public bool IsSettingsPage => CurrentPage == ShellPage.Settings;

    public ShellViewModel(LibraryViewModel library, ImportViewModel import)
        : this(library, import, null, null, library.Export)
    {
    }

    public ShellViewModel(LibraryViewModel library, ImportViewModel import, SettingsViewModel? settings)
        : this(library, import, settings, null, library.Export)
    {
    }

    public ShellViewModel(LibraryViewModel library, ImportViewModel import, SettingsViewModel? settings, OverviewViewModel? overview)
        : this(library, import, settings, overview, library.Export)
    {
    }

    public ShellViewModel(LibraryViewModel library, ImportViewModel import, SettingsViewModel? settings, OverviewViewModel? overview, ExportViewModel? export)
    {
        Library = library;
        Overview = overview;
        Import = import;
        Settings = settings;
        Export = export;
        if (settings?.CurrentSettings.StartPage == SongStash.Application.Settings.StartPage.Overview && overview is not null)
            CurrentPage = ShellPage.Overview;
        Import.PropertyChanged += OnImportPropertyChanged;
        if (Settings?.Backup is not null) Settings.Backup.LibraryRefreshRequested += OnLibraryRefreshRequested;
        Import.BackToSourceRequested += BackToImportSource;
        Import.BackToLibraryRequested += RequestBackToLibrary;
        Import.LibraryRefreshRequested += OnLibraryRefreshRequested;
        if (Overview is not null) Overview.SongSelected += SelectSongFromOverview;
        if (Settings is not null) Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public string LibraryNavigationLabel => Library.Strings.NavigationLibrary;
    public string OverviewNavigationLabel => Overview?.Strings.Title ?? Library.Strings.NavigationOverview;
    public string ImportNavigationLabel => Import.Strings.NavigationImport;

    [RelayCommand]
    private void OpenSettings()
    {
        Library.ResetTransientState();
        if (CurrentPage != ShellPage.Settings && Settings is not null)
            Settings.CurrentTab = SettingsTab.Appearance;
        CurrentPage = ShellPage.Settings;
    }

    [RelayCommand]
    private async Task ShowLibraryAsync()
    {
        Library.ResetTransientState();
        await Library.LoadAsync();
        Import.ResetToSource();
        CurrentPage = ShellPage.Library;
    }

    [RelayCommand]
    private async Task ShowOverviewAsync()
    {
        Library.ResetTransientState();
        if (Overview is not null) await Overview.LoadAsync();
        Import.ResetToSource();
        CurrentPage = ShellPage.Overview;
    }

    [RelayCommand]
    private void OpenImport()
    {
        Library.ResetTransientState();
        if (Import.IsBusy)
        {
            CurrentPage = Import.IsImporting ? ShellPage.Importing : ShellPage.ImportSource;
            return;
        }

        Import.ResetToSource();
        CurrentPage = ShellPage.ImportSource;
    }

    [RelayCommand]
    private void BackToImportSource()
    {
        Import.ResetToSource();
        CurrentPage = ShellPage.ImportSource;
    }

    [RelayCommand]
    private async Task BackToLibraryAsync()
    {
        Library.ResetTransientState();
        await Library.LoadAsync();
        Import.ResetToSource();
        CurrentPage = ShellPage.Library;
    }

    private void RequestBackToLibrary() => _ = BackToLibraryAsync();

    private void OnLibraryRefreshRequested()
    {
        if (CurrentPage == ShellPage.Library)
        {
            _ = Library.LoadAsync();
        }
        else if (CurrentPage == ShellPage.Overview && Overview is not null)
        {
            _ = Overview.LoadAsync();
        }
    }

    private void SelectSongFromOverview(Guid songId) => _ = SelectSongFromOverviewAsync(songId);

    private async Task SelectSongFromOverviewAsync(Guid songId)
    {
        Library.ResetTransientState();
        await Library.LoadAsync();
        await Library.SelectSongByIdAsync(songId);
        Import.ResetToSource();
        CurrentPage = ShellPage.Library;
    }

    private void OnImportPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ImportViewModel.State))
        {
            return;
        }

        if (Import.State == ImportWorkflowState.PreviewReady && CurrentPage == ShellPage.ImportSource)
        {
            CurrentPage = ShellPage.ImportPreview;
        }
        else if (Import.State == ImportWorkflowState.Importing && CurrentPage == ShellPage.ImportPreview)
        {
            CurrentPage = ShellPage.Importing;
        }
        else if ((Import.State is ImportWorkflowState.Completed or ImportWorkflowState.Cancelled or ImportWorkflowState.Error) &&
                 CurrentPage == ShellPage.Importing)
        {
            CurrentPage = ShellPage.ImportResults;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SettingsViewModel.Language) or nameof(SettingsViewModel.Strings))) return;
        OnPropertyChanged(nameof(LibraryNavigationLabel));
        OnPropertyChanged(nameof(OverviewNavigationLabel));
        OnPropertyChanged(nameof(ImportNavigationLabel));
    }

    public void Dispose()
    {
        if (Settings is not null) Settings.PropertyChanged -= OnSettingsPropertyChanged;
        Import.PropertyChanged -= OnImportPropertyChanged;
        Import.BackToSourceRequested -= BackToImportSource;
        Import.BackToLibraryRequested -= RequestBackToLibrary;
        Import.LibraryRefreshRequested -= OnLibraryRefreshRequested;
        if (Settings?.Backup is not null) Settings.Backup.LibraryRefreshRequested -= OnLibraryRefreshRequested;
        if (Overview is not null) Overview.SongSelected -= SelectSongFromOverview;
        Import.Dispose();
    }
}
