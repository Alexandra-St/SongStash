using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using Xunit;

namespace SongStash.Application.Tests.Importing;

public sealed class ImportOrchestratorTests
{
    [Fact]
    public async Task ReadBuildsPreviewAndCountsEveryPreviewStatus()
    {
        var existing = new Song("Existing", "Artist");
        var ambiguousOne = new Song("Ambiguous", "Artist");
        var ambiguousTwo = new Song("Ambiguous", "Artist");
        var reader = new FakeReader(ImportReadResult.Success(
        [
            Candidate("New song", "Artist"),
            Candidate("Existing", "Artist"),
            Candidate("", ""),
            Candidate("Ambiguous", "Artist")
        ]));
        var orchestrator = Create(reader, [existing, ambiguousOne, ambiguousTwo]);

        var result = await orchestrator.ReadPreviewAsync();

        Assert.Equal(ImportPreviewRunStatus.Success, result.Status);
        Assert.Equal(new ImportPreviewSummary(4, 1, 1, 1, 1), result.Summary);
        Assert.Equal(
            new[] { ImportPreviewStatus.New, ImportPreviewStatus.AlreadyInLibrary,
                ImportPreviewStatus.Incomplete, ImportPreviewStatus.NeedsReview },
            result.Items.Select(item => item.Status));
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task UnavailableAndUnsupportedReaderResultsArePreserved()
    {
        var diagnostic = new ImportDiagnostic(ImportDiagnosticSeverity.Error, "missing", "Not found.");
        var unavailable = await Create(new FakeReader(ImportReadResult.Unavailable(diagnostic))).ReadPreviewAsync();
        var unsupported = await Create(new FakeReader(ImportReadResult.UnsupportedSource(diagnostic))).ReadPreviewAsync();

        Assert.Equal(ImportPreviewRunStatus.SourceUnavailable, unavailable.Status);
        Assert.Equal(ImportPreviewRunStatus.UnsupportedSchema, unsupported.Status);
        Assert.Contains(diagnostic, unavailable.Diagnostics);
        Assert.Contains(diagnostic, unsupported.Diagnostics);
        Assert.Null(unavailable.Summary);
    }

    [Fact]
    public async Task RepeatRecognitionCandidatesRemainSeparatePreviewRows()
    {
        var first = Candidate("Same song", "Same artist", "recognition-a");
        var second = Candidate("Same song", "Same artist", "recognition-b");
        first = first with { ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key")] };
        second = second with { ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key")] };
        var result = await Create(new FakeReader(ImportReadResult.Success([first, second]))).ReadPreviewAsync();

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(ImportPreviewStatus.New, item.Status));
        Assert.Equal(new[] { "recognition-a", "recognition-b" },
            result.Items.Select(item => item.Candidate.SourceItemId));
    }

    [Fact]
    public async Task EmptyHistoryReturnsSuccessfulEmptyPreview()
    {
        var result = await Create(new FakeReader(ImportReadResult.Success())).ReadPreviewAsync();

        Assert.Equal(ImportPreviewRunStatus.Success, result.Status);
        Assert.Empty(result.Items);
        Assert.Equal(new ImportPreviewSummary(0, 0, 0, 0, 0), result.Summary);
    }

    [Fact]
    public async Task ReaderExceptionBecomesControlledFailure()
    {
        var result = await Create(new FakeReader(new InvalidOperationException("sensitive"))).ReadPreviewAsync();

        Assert.Equal(ImportPreviewRunStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == "import.read.failed");
        Assert.DoesNotContain(result.Diagnostics, item => item.Message.Contains("sensitive", StringComparison.Ordinal));
    }

    private static ImportCandidate Candidate(string title, string artist, string? sourceItemId = null) =>
        new(title, artist, FindSource.ShazamApple) { SourceItemId = sourceItemId };

    private static ImportOrchestrator Create(FakeReader reader, IReadOnlyList<Song>? songs = null)
    {
        var songRepository = new FakeSongRepository(songs ?? []);
        var findEvents = new FakeFindEventRepository();
        var references = new FakeReferenceRepository();
        var importService = new ImportService(songRepository, findEvents, new NoOpCommitter(),
            new FindEventDeduplicator(), new FakeClock());
        return new ImportOrchestrator(reader, songRepository, references,
            new ImportPreviewBuilder(new SongMatcher()), importService);
    }

    private sealed class FakeReader : IImportSourceReader
    {
        private readonly ImportReadResult? result;
        private readonly Exception? exception;
        public int Calls { get; private set; }
        public FakeReader(ImportReadResult result) => this.result = result;
        public FakeReader(Exception failure) => exception = failure;
        public Task<ImportReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return exception is null ? Task.FromResult(result!) : Task.FromException<ImportReadResult>(exception);
        }
    }

    private sealed class FakeSongRepository(IReadOnlyList<Song> initial) : ISongRepository
    {
        private readonly Dictionary<Guid, Song> stored = initial.ToDictionary(song => song.Id);
        public Task<IReadOnlyList<Song>> GetAllForMatchingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Song>>(stored.Values.ToArray());
        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(stored.GetValueOrDefault(id));
        public Task AddAsync(Song song, CancellationToken cancellationToken = default) { stored.Add(song.Id, song); return Task.CompletedTask; }
        public Task UpdateAsync(Song song, CancellationToken cancellationToken = default) { stored[song.Id] = song; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { stored.Remove(id); return Task.CompletedTask; }
    }

    private sealed class FakeFindEventRepository : IFindEventRepository
    {
        public Task<IReadOnlyList<FindEvent>> GetForSongsAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FindEvent>>([]);
        public Task<IReadOnlyList<FindEvent>> GetBySongIdAsync(Guid songId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FindEvent>>([]);
        public Task AddAsync(FindEvent findEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeReferenceRepository : IExternalReferenceRepository
    {
        public Task<IReadOnlyList<ExternalReference>> GetBySongIdsAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExternalReference>>([]);
        public Task AddAsync(ExternalReference reference, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpCommitter : IImportItemCommitter
    {
        public Task CommitNewSongAsync(Song song, IReadOnlyCollection<ExternalReference> externalReferences,
            FindEvent findEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitFindEventAsync(FindEvent findEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }
}
