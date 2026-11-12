using CommunityToolkit.Mvvm.ComponentModel;
using SongStash.Application.Importing;

namespace SongStash.Desktop.ViewModels;

public sealed partial class ImportPreviewRowViewModel : ObservableObject
{
    private readonly ImportPreviewItem item;
    private readonly Func<ImportUiText> getStrings;

    public ImportPreviewRowViewModel(ImportPreviewItem item, Func<ImportUiText>? getStrings = null)
    {
        this.item = item;
        this.getStrings = getStrings ?? (() => ImportUiText.For(SongStash.Application.Settings.AppLanguage.English));
        isSelected = item.Status is ImportPreviewStatus.New or ImportPreviewStatus.AlreadyInLibrary;
    }

    private ImportUiText Strings => getStrings();
    public ImportPreviewItem PreviewItem => item;
    public string Title => item.Candidate.Title;
    public string Artist => item.Candidate.Artist;
    public string Album => item.Candidate.Album ?? string.Empty;
    public string FoundAtText => item.Candidate.FoundAt is { } foundAt ? UserDateTimeFormatter.Format(foundAt) : Strings.UnknownDate;
    public ImportPreviewStatus Status => item.Status;
    public string StatusLabel => item.Status switch
    {
        ImportPreviewStatus.New => Strings.New,
        ImportPreviewStatus.AlreadyInLibrary => Strings.AlreadyInLibrary,
        ImportPreviewStatus.Incomplete => Strings.Incomplete,
        _ => Strings.NeedsReview
    };
    public bool IsNew => Status == ImportPreviewStatus.New;
    public bool IsAlreadyInLibrary => Status == ImportPreviewStatus.AlreadyInLibrary;
    public bool IsIncomplete => Status == ImportPreviewStatus.Incomplete;
    public bool IsNeedsReview => Status == ImportPreviewStatus.NeedsReview;
    public bool CanSelect => IsNew || IsAlreadyInLibrary;
    public bool HasWarning => item.Diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Warning);
    public bool HasError => item.Diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error);
    public bool HasDiagnostics => HasWarning || HasError;
    public string DiagnosticMessage => string.Join(Environment.NewLine,
        item.Diagnostics.Where(diagnostic => diagnostic.Severity != ImportDiagnosticSeverity.Info)
            .Select(diagnostic => Strings.Diagnostic(diagnostic.Code, diagnostic.Message)));

    [ObservableProperty]
    private bool isSelected;

    public event Action? SelectionChanged;

    public void RefreshStrings()
    {
        OnPropertyChanged(nameof(FoundAtText));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(DiagnosticMessage));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && !CanSelect)
        {
            IsSelected = false;
            return;
        }
        SelectionChanged?.Invoke();
    }
}
