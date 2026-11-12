using SongStash.Application.Importing;
using SongStash.Application.Library;
using SongStash.Application.LibraryActions;
using SongStash.Application.Matching;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Desktop.ViewModels;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class ManualAddViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 11, 15, 0, TimeSpan.Zero);

    [Fact]
    public void UseTodayIsExplicitAndUsesInjectedClock()
    {
        var fixture = new Fixture();
        Assert.Equal(Now.ToLocalTime().Date, fixture.ViewModel.FoundDate?.LocalDateTime.Date);
        fixture.ViewModel.UseTodayCommand.Execute(null);
        Assert.NotNull(fixture.ViewModel.FoundAt);
        Assert.Equal(Now, fixture.ViewModel.FoundAt);
        Assert.Equal(Now.ToLocalTime().Date, fixture.ViewModel.FoundDate?.LocalDateTime.Date);
    }

    [Fact]
    public void NewFormInitializesDateAndTimeFromOneLocalClockValue()
    {
        var fixture = new Fixture();
        var localNow = Now.ToLocalTime();

        Assert.Equal(localNow.Day, fixture.ViewModel.SelectedDay);
        Assert.Equal(localNow.Month, fixture.ViewModel.SelectedMonth);
        Assert.Equal(localNow.Year, fixture.ViewModel.SelectedYear);
        Assert.Equal(localNow.ToString("HH", System.Globalization.CultureInfo.InvariantCulture), fixture.ViewModel.SelectedHour);
        Assert.Equal(localNow.ToString("mm", System.Globalization.CultureInfo.InvariantCulture), fixture.ViewModel.SelectedMinute);
        Assert.Equal(localNow.TimeOfDay, fixture.ViewModel.FoundTime);
    }

    [Fact]
    public void ResetForReopenedFormUsesFreshClockValue()
    {
        var fixture = new Fixture();
        var later = Now.AddHours(3).AddMinutes(7);
        fixture.Clock.UtcNowValue = later;

        fixture.ViewModel.Reset();
        var localNow = later.ToLocalTime();

        Assert.Equal(localNow.Day, fixture.ViewModel.SelectedDay);
        Assert.Equal(localNow.Month, fixture.ViewModel.SelectedMonth);
        Assert.Equal(localNow.Year, fixture.ViewModel.SelectedYear);
        Assert.Equal(localNow.ToString("HH", System.Globalization.CultureInfo.InvariantCulture), fixture.ViewModel.SelectedHour);
        Assert.Equal(localNow.ToString("mm", System.Globalization.CultureInfo.InvariantCulture), fixture.ViewModel.SelectedMinute);
    }

    [Fact]
    public async Task ClearingOptionalDatePartsKeepsFoundAtNullOnSave()
    {
        var fixture = new Fixture();
        fixture.ViewModel.Title = "Undated song";
        fixture.ViewModel.Artist = "Artist";
        fixture.ViewModel.SelectedDay = null;
        fixture.ViewModel.SelectedMonth = null;
        fixture.ViewModel.SelectedYear = null;

        Assert.Null(fixture.ViewModel.FoundAt);
        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(fixture.Committer.FindEvents).FoundAt);
    }

    [Fact]
    public async Task DateSelectorsComposeValidDateAndAllowSave()
    {
        var fixture = new Fixture();
        fixture.ViewModel.Title = "Leap day song";
        fixture.ViewModel.Artist = "Artist";
        fixture.ViewModel.SelectedDay = 29;
        fixture.ViewModel.SelectedMonth = 2;
        fixture.ViewModel.SelectedYear = 2024;

        Assert.False(fixture.ViewModel.HasDateError);
        Assert.Equal(new DateTime(2024, 2, 29), fixture.ViewModel.FoundDate?.LocalDateTime.Date);

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Single(fixture.Committer.Songs);
        Assert.Equal(new DateTime(2024, 2, 29), Assert.Single(fixture.Committer.FindEvents).FoundAt?.LocalDateTime.Date);
    }

    [Fact]
    public async Task InvalidDateSelectorCombinationIsInlineAndDoesNotSave()
    {
        var fixture = new Fixture();
        fixture.ViewModel.Title = "Invalid date song";
        fixture.ViewModel.Artist = "Artist";
        fixture.ViewModel.SelectedYear = 2025;
        fixture.ViewModel.SelectedMonth = 2;
        fixture.ViewModel.SelectedDay = 31;

        Assert.True(fixture.ViewModel.HasDateError);
        Assert.Null(fixture.ViewModel.FoundDate);

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(fixture.Committer.Songs);
        Assert.Empty(fixture.Committer.FindEvents);
    }

    [Fact]
    public async Task EmptyRequiredFieldsShowInlineErrors()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.HasTitleError);
        Assert.True(fixture.ViewModel.HasArtistError);
        Assert.Empty(fixture.Committer.Songs);
    }

    [Fact]
    public async Task SaveReportsExactMatchAndOffersAttachOrSeparateActions()
    {
        var existing = new Song("Song", "Artist");
        var fixture = new Fixture([existing]);
        fixture.ViewModel.Title = "Song";
        fixture.ViewModel.Artist = "Artist";
        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.RequiresChoice);
        Assert.Single(fixture.ViewModel.Matches);
        Assert.Contains("already", fixture.ViewModel.Message!, StringComparison.OrdinalIgnoreCase);

        await fixture.ViewModel.Matches[0].AttachCommand.ExecuteAsync(null);
        Assert.Single(fixture.Committer.FindEvents);
        Assert.Empty(fixture.Committer.Songs);
    }

    [Fact]
    public async Task InvalidArtworkUrlIsInlineAndValidArtworkIsStoredForNewSong()
    {
        var fixture = new Fixture();
        fixture.ViewModel.Title = "Song";
        fixture.ViewModel.Artist = "Artist";
        fixture.ViewModel.ArtworkUrlText = "not a URL";
        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.HasArtworkUrlError);
        Assert.Empty(fixture.Committer.Songs);

        fixture.ViewModel.ArtworkUrlText = "https://example.test/art.png";
        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);
        Assert.Equal(new Uri("https://example.test/art.png"), Assert.Single(fixture.Committer.Songs).ArtworkUrl);
        Assert.Equal(FindSource.Manual, Assert.Single(fixture.Committer.FindEvents).Source);
    }

    private sealed class Fixture
    {
        public FakeCommitter Committer { get; } = new();
        public MutableClock Clock { get; } = new();
        public ManualAddViewModel ViewModel { get; }
        public Fixture(IEnumerable<Song>? initial = null)
        {
            var songs = new FakeSongRepository(initial ?? []);
            var service = new ManualSongService(songs, new FakeReferences(), new SongMatcher(), Committer, Clock);
            ViewModel = new ManualAddViewModel(service, Clock);
        }
    }

    private sealed class MutableClock : IClock { public DateTimeOffset UtcNowValue { get; set; } = Now; public DateTimeOffset UtcNow => UtcNowValue; }
    private sealed class FakeSongRepository(IEnumerable<Song> initial) : ISongRepository
    {
        public Dictionary<Guid, Song> Stored { get; } = initial.ToDictionary(song => song.Id);
        public Task<IReadOnlyList<Song>> GetAllForMatchingAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Song>>(Stored.Values.ToArray());
        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Stored.GetValueOrDefault(id));
        public Task AddAsync(Song song, CancellationToken cancellationToken = default) { Stored.Add(song.Id, song); return Task.CompletedTask; }
        public Task UpdateAsync(Song song, CancellationToken cancellationToken = default) { Stored[song.Id] = song; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { Stored.Remove(id); return Task.CompletedTask; }
    }
    private sealed class FakeReferences : IExternalReferenceRepository
    {
        public Task<IReadOnlyList<ExternalReference>> GetBySongIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ExternalReference>>([]);
        public Task AddAsync(ExternalReference reference, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class FakeCommitter : IManualEntryCommitter
    {
        public List<Song> Songs { get; } = [];
        public List<FindEvent> FindEvents { get; } = [];
        public Task CommitNewSongAsync(Song song, FindEvent item, CancellationToken cancellationToken = default) { Songs.Add(song); FindEvents.Add(item); return Task.CompletedTask; }
        public Task CommitFindEventAsync(FindEvent item, CancellationToken cancellationToken = default) { FindEvents.Add(item); return Task.CompletedTask; }
    }
}
