using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Domain.Finds;

namespace SongStash.Desktop.ViewModels;

public sealed class FindEventViewModel(FindEvent findEvent, Action<Guid>? requestDelete = null,
    Func<LibraryStrings>? getStrings = null) : ObservableObject
{
    private LibraryStrings Strings => getStrings?.Invoke() ?? LibraryStrings.For(SongStash.Application.Settings.AppLanguage.English);
    public Guid Id => findEvent.Id;
    public RelayCommand DeleteCommand { get; } = new(() => requestDelete?.Invoke(findEvent.Id));
    public string DeleteLabel => Strings.DeleteFind;
    public string? SourceItemId => findEvent.SourceItemId;
    public string Source => findEvent.Source switch
    {
        FindSource.ShazamApple => Strings.SourceShazamApple,
        FindSource.ShazamAndroid => Strings.SourceShazamAndroid,
        FindSource.Manual => Strings.SourceManual,
        _ => Strings.SourceImported
    };
    public string FoundAt => findEvent.FoundAt is { } foundAt ? UserDateTimeFormatter.Format(foundAt) : Strings.DateUnknown;
    public string? ImportedAt => findEvent.FoundAt is null
        ? string.Format(Strings.ImportedAtFormat, UserDateTimeFormatter.Format(findEvent.ImportedAt))
        : null;

    public void RefreshStrings()
    {
        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(FoundAt));
        OnPropertyChanged(nameof(ImportedAt));
        OnPropertyChanged(nameof(DeleteLabel));
    }
}
