using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using SongStash.Application.Importing;
using SongStash.Application.LibraryActions;
using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed partial class ManualAddViewModel : ObservableObject
{
    private readonly ManualSongService service;
    private readonly IClock clock;
    private readonly SettingsViewModel? settings;

    public ManualAddViewModel(ManualSongService service, IClock clock, SettingsViewModel? settings = null)
    {
        this.service = service;
        this.clock = clock;
        this.settings = settings;
        if (settings is not null) settings.PropertyChanged += OnSettingsPropertyChanged;
        var now = clock.UtcNow.ToLocalTime();
        YearOptions = Enumerable.Range(1900, Math.Max(1, now.Year - 1898)).ToArray();
        _syncingDateParts = true;
        FoundDate = new DateTimeOffset(now.Date, TimeZoneInfo.Local.GetUtcOffset(now.Date));
        SelectedDay = now.Day;
        SelectedMonth = now.Month;
        SelectedYear = now.Year;
        _syncingDateParts = false;
        FoundTime = now.TimeOfDay;
    }

    public event Func<Guid, Task>? SongSaved;
    public event Action? Cancelled;
    public ObservableCollection<ManualSongMatchOptionViewModel> Matches { get; } = [];

    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string artist = string.Empty;
    [ObservableProperty] private string? album;
    [ObservableProperty] private string? genre;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private string? artworkUrlText;
    [ObservableProperty] private string? artworkUrlError;
    [ObservableProperty] private DateTimeOffset? foundDate;
    [ObservableProperty] private TimeSpan? foundTime;
    [ObservableProperty] private int? selectedDay;
    [ObservableProperty] private int? selectedMonth;
    [ObservableProperty] private int? selectedYear;
    [ObservableProperty] private string? dateError;
    [ObservableProperty] private string? selectedHour;
    [ObservableProperty] private string? selectedMinute;
    [ObservableProperty] private string? titleError;
    [ObservableProperty] private string? artistError;
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool requiresChoice;
    [ObservableProperty] private bool isSaving;
    private bool _syncingDateParts;
    private bool _syncingTimeParts;

    public ManualAddStrings Strings => ManualAddStrings.For(settings?.Language ?? AppLanguage.English);
    public bool HasMatches => Matches.Count > 0;
    public bool HasFoundAt => FoundDate.HasValue;
    public bool HasTitleError => !string.IsNullOrWhiteSpace(TitleError);
    public bool HasArtistError => !string.IsNullOrWhiteSpace(ArtistError);
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool HasArtworkUrlError => !string.IsNullOrWhiteSpace(ArtworkUrlError);
    public bool HasDateError => !string.IsNullOrWhiteSpace(DateError);
    public IReadOnlyList<int> DayOptions { get; } = Enumerable.Range(1, 31).ToArray();
    public IReadOnlyList<int> YearOptions { get; }
    public IReadOnlyList<string> HourOptions { get; } = Enumerable.Range(0, 24).Select(hour => hour.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
    public IReadOnlyList<string> MinuteOptions { get; } = Enumerable.Range(0, 60).Select(minute => minute.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
    public IReadOnlyList<ManualMonthOption> MonthOptions
    {
        get
        {
            var culture = CultureInfo.GetCultureInfo(settings?.Language == AppLanguage.Russian ? "ru-RU" : "en-US");
            return Enumerable.Range(1, 12)
                .Select(month => new ManualMonthOption(month, culture.DateTimeFormat.GetMonthName(month)))
                .ToArray();
        }
    }
    public DateTimeOffset? FoundAt => FoundDate is { } date
        ? new DateTimeOffset(date.Date.Add(FoundTime ?? TimeSpan.Zero), TimeZoneInfo.Local.GetUtcOffset(date.Date.Add(FoundTime ?? TimeSpan.Zero)))
        : null;

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.Language)) return;
        OnPropertyChanged(nameof(Strings));
        OnPropertyChanged(nameof(MonthOptions));
        if (TitleError is not null) TitleError = Strings.TitleRequired;
        if (ArtistError is not null) ArtistError = Strings.ArtistRequired;
        if (ArtworkUrlError is not null) ArtworkUrlError = Strings.InvalidArtworkUrl;
        if (DateError is not null) DateError = Strings.InvalidDate;
    }

    partial void OnTitleErrorChanged(string? value) => OnPropertyChanged(nameof(HasTitleError));
    partial void OnArtistErrorChanged(string? value) => OnPropertyChanged(nameof(HasArtistError));
    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));
    partial void OnArtworkUrlErrorChanged(string? value) => OnPropertyChanged(nameof(HasArtworkUrlError));
    partial void OnDateErrorChanged(string? value) => OnPropertyChanged(nameof(HasDateError));
    partial void OnFoundDateChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(HasFoundAt));
        OnPropertyChanged(nameof(FoundAt));
        if (_syncingDateParts) return;
        _syncingDateParts = true;
        SelectedDay = value?.LocalDateTime.Day;
        SelectedMonth = value?.LocalDateTime.Month;
        SelectedYear = value?.LocalDateTime.Year;
        _syncingDateParts = false;
        DateError = null;
    }
    partial void OnFoundTimeChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(FoundAt));
        if (_syncingTimeParts) return;
        _syncingTimeParts = true;
        SelectedHour = value?.Hours.ToString("D2", CultureInfo.InvariantCulture);
        SelectedMinute = value?.Minutes.ToString("D2", CultureInfo.InvariantCulture);
        _syncingTimeParts = false;
    }
    partial void OnSelectedHourChanged(string? value) => UpdateTimeFromParts();
    partial void OnSelectedMinuteChanged(string? value) => UpdateTimeFromParts();
    partial void OnSelectedDayChanged(int? value) => UpdateDateFromParts();
    partial void OnSelectedMonthChanged(int? value) => UpdateDateFromParts();
    partial void OnSelectedYearChanged(int? value) => UpdateDateFromParts();

    public void Reset()
    {
        Title = Artist = string.Empty; Album = Genre = Notes = ArtworkUrlText = null;
        var now = clock.UtcNow.ToLocalTime();
        _syncingDateParts = true;
        FoundDate = new DateTimeOffset(now.Date, TimeZoneInfo.Local.GetUtcOffset(now.Date));
        SelectedDay = now.Day;
        SelectedMonth = now.Month;
        SelectedYear = now.Year;
        _syncingDateParts = false;
        FoundTime = now.TimeOfDay;
        DateError = null;
        TitleError = ArtistError = Message = ArtworkUrlError = null; RequiresChoice = false;
        Matches.Clear();
        OnPropertyChanged(nameof(HasMatches));
    }

    [RelayCommand]
    private void UseToday()
    {
        var now = clock.UtcNow.ToLocalTime();
        FoundDate = now.Date;
        FoundTime = now.TimeOfDay;
    }

    private void UpdateDateFromParts()
    {
        if (_syncingDateParts) return;
        if (SelectedDay is null && SelectedMonth is null && SelectedYear is null)
        {
            _syncingDateParts = true;
            FoundDate = null;
            _syncingDateParts = false;
            DateError = null;
            OnPropertyChanged(nameof(FoundAt));
            return;
        }

        if (SelectedDay is not int day || SelectedMonth is not int month || SelectedYear is not int year ||
            !DateTime.TryParseExact($"{year:D4}-{month:D2}-{day:D2}", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var selectedDate))
        {
            _syncingDateParts = true;
            FoundDate = null;
            _syncingDateParts = false;
            DateError = Strings.InvalidDate;
            OnPropertyChanged(nameof(FoundAt));
            return;
        }

        var localDate = DateTime.SpecifyKind(selectedDate.Date, DateTimeKind.Unspecified);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDate);
        _syncingDateParts = true;
        FoundDate = new DateTimeOffset(localDate, offset);
        _syncingDateParts = false;
        DateError = null;
        OnPropertyChanged(nameof(FoundAt));
    }

    private void UpdateTimeFromParts()
    {
        if (_syncingTimeParts ||
            !int.TryParse(SelectedHour, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(SelectedMinute, NumberStyles.None, CultureInfo.InvariantCulture, out var minute) ||
            hour is < 0 or > 23 || minute is < 0 or > 59) return;

        _syncingTimeParts = true;
        FoundTime = new TimeSpan(hour, minute, 0);
        _syncingTimeParts = false;
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        TitleError = string.IsNullOrWhiteSpace(Title) ? Strings.TitleRequired : null;
        ArtistError = string.IsNullOrWhiteSpace(Artist) ? Strings.ArtistRequired : null;
        Message = null; ArtworkUrlError = null; RequiresChoice = false; Matches.Clear();
        OnPropertyChanged(nameof(HasMatches));
        if (TitleError is not null || ArtistError is not null) return;
        if (HasDateError) return;
        if (!TryGetArtworkUrl(out _)) return;
        await SubmitAsync(null, keepSeparate: false, cancellationToken);
    }

    [RelayCommand]
    private async Task KeepSeparateAsync(CancellationToken cancellationToken) =>
        await SubmitAsync(null, keepSeparate: true, cancellationToken);

    private async Task AttachAsync(Guid songId, CancellationToken cancellationToken = default) =>
        await SubmitAsync(songId, keepSeparate: false, cancellationToken);

    private bool TryGetArtworkUrl(out Uri? artworkUrl)
    {
        artworkUrl = null;
        if (string.IsNullOrWhiteSpace(ArtworkUrlText)) return true;
        if (Uri.TryCreate(ArtworkUrlText.Trim(), UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp))
        {
            artworkUrl = parsed;
            return true;
        }
        ArtworkUrlError = Strings.InvalidArtworkUrl;
        return false;
    }

    private async Task SubmitAsync(Guid? attachId, bool keepSeparate, CancellationToken cancellationToken)
    {
        if (HasDateError) return;
        if (!TryGetArtworkUrl(out var artworkUrl)) return;
        IsSaving = true;
        try
        {
            var result = await service.AddAsync(new ManualSongInput(Title, Artist, Album, Genre, Notes, FoundAt, artworkUrl),
                attachId, keepSeparate, cancellationToken);
            switch (result.Outcome)
            {
                case ManualAddOutcome.AddedNewSong:
                case ManualAddOutcome.AddedFindToExistingSong:
                    if (result.SongId is { } songId && SongSaved is { } saved) await saved(songId);
                    break;
                case ManualAddOutcome.ExactMatchRequiresChoice:
                    RequiresChoice = true;
                    Message = Strings.ExactMatchMessage;
                    SetMatches(result.Match?.PossibleMatches ?? []);
                    break;
                case ManualAddOutcome.NeedsReview:
                    RequiresChoice = true;
                    Message = Strings.ReviewMatchMessage;
                    SetMatches(result.Match?.PossibleMatches ?? []);
                    break;
                case ManualAddOutcome.Invalid:
                    if (result.Message?.StartsWith("Title", StringComparison.Ordinal) == true) TitleError = Strings.TitleRequired;
                    else if (result.Message?.StartsWith("Artist", StringComparison.Ordinal) == true) ArtistError = Strings.ArtistRequired;
                    else Message = Strings.SaveFailed;
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { Message = Strings.SaveFailed; }
        finally { IsSaving = false; }
    }

    private void SetMatches(IEnumerable<SongStash.Domain.Songs.Song> songs)
    {
        Matches.Clear();
        foreach (var song in songs) Matches.Add(new ManualSongMatchOptionViewModel(song, Strings.Attach, id => AttachAsync(id)));
        OnPropertyChanged(nameof(HasMatches));
    }
}

public sealed record ManualMonthOption(int Month, string Label);
