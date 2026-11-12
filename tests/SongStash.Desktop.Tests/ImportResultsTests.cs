using SongStash.Application.Importing;
using SongStash.Application.Library;
using SongStash.Desktop.ViewModels;
using SongStash.Domain.Finds;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class ImportResultsTests
{
    [Fact]
    public async Task ImportingStateShowsProgressPageAndSelectedCount()
    {
        var orchestrator = new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New)))
        {
            WaitForImport = true
        };
        using var shell = CreateShell(orchestrator);
        await OpenPreviewAsync(shell);

        var operation = shell.Import.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);
        await orchestrator.ImportStarted.Task;

        Assert.Equal(ShellPage.Importing, shell.CurrentPage);
        Assert.Equal(1, shell.Import.SelectedCount);
        shell.Import.CancelCommand.Execute(null);
        await operation;
    }

    [Fact]
    public async Task CancelRequestsCancellationAndOpensControlledCancelledResults()
    {
        var orchestrator = new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New)))
        {
            WaitForImport = true
        };
        using var shell = CreateShell(orchestrator);
        await OpenPreviewAsync(shell);

        var operation = shell.Import.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);
        await orchestrator.ImportStarted.Task;
        shell.Import.CancelCommand.Execute(null);
        await operation;

        Assert.True(orchestrator.CancellationObserved);
        Assert.Equal(ImportWorkflowState.Cancelled, shell.Import.State);
        Assert.Equal(ShellPage.ImportResults, shell.CurrentPage);
        Assert.Null(shell.Import.Result);
        Assert.Null(shell.Import.ErrorMessage);
    }

    [Fact]
    public async Task SuccessfulCompletionOpensResultsPage()
    {
        var result = Result((ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null));
        using var shell = CreateShell(new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New)), result));
        await OpenPreviewAsync(shell);

        await shell.Import.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ImportWorkflowState.Completed, shell.Import.State);
        Assert.Equal(ShellPage.ImportResults, shell.CurrentPage);
        Assert.Same(result, shell.Import.Result);
    }

    [Fact]
    public async Task AddedSongsAreReportedSeparately()
    {
        var viewModel = await CompleteAsync(Result(
            (ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null),
            (ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null)));
        Assert.Equal(2, viewModel.Result?.AddedSongs);
    }

    [Fact]
    public async Task AddedFindEventsAreReportedAsTheirOwnCount()
    {
        var viewModel = await CompleteAsync(Result(
            (ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null),
            (ImportItemOutcomeStatus.AddedFindEvent, ImportPreviewStatus.AlreadyInLibrary, null),
            (ImportItemOutcomeStatus.AddedFindEvent, ImportPreviewStatus.AlreadyInLibrary, null)));

        Assert.Equal(1, viewModel.Result?.AddedSongs);
        Assert.Equal(3, viewModel.Result?.AddedFindEvents);
    }

    [Fact]
    public async Task SkippedDuplicatesHaveSeparateNeutralCount()
    {
        var viewModel = await CompleteAsync(Result(
            (ImportItemOutcomeStatus.SkippedDuplicate, ImportPreviewStatus.AlreadyInLibrary, null),
            (ImportItemOutcomeStatus.SkippedDuplicate, ImportPreviewStatus.AlreadyInLibrary, null)));
        Assert.Equal(2, viewModel.Result?.SkippedDuplicates);
    }

    [Fact]
    public async Task NeedsAttentionCountAndRowsAreSeparate()
    {
        var viewModel = await CompleteAsync(Result(
            (ImportItemOutcomeStatus.NeedsAttention, ImportPreviewStatus.Incomplete, "Complete the title and artist."),
            (ImportItemOutcomeStatus.NeedsAttention, ImportPreviewStatus.NeedsReview, "Review the possible matches.")));

        Assert.Equal(2, viewModel.Result?.NeedsAttention);
        Assert.Equal(2, viewModel.NeedsAttentionItems.Count);
    }

    [Fact]
    public async Task FailedCountAndRowsAreSeparate()
    {
        var viewModel = await CompleteAsync(Result(
            (ImportItemOutcomeStatus.Failed, ImportPreviewStatus.New, "Could not save this item.")));

        Assert.Equal(1, viewModel.Result?.Failed);
        Assert.Single(viewModel.FailedItems);
    }

    [Fact]
    public async Task DuplicateOnlyResultIsCompletedNotAnError()
    {
        var viewModel = await CompleteAsync(Result(
            (ImportItemOutcomeStatus.SkippedDuplicate, ImportPreviewStatus.AlreadyInLibrary, null),
            (ImportItemOutcomeStatus.SkippedDuplicate, ImportPreviewStatus.AlreadyInLibrary, null)));

        Assert.Equal(ImportWorkflowState.Completed, viewModel.State);
        Assert.True(viewModel.IsDuplicateOnly);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(0, viewModel.Result?.AddedSongs);
        Assert.Equal(0, viewModel.Result?.AddedFindEvents);
    }

    [Fact]
    public async Task PartialFailureKeepsSuccessCountsAndFailureDetails()
    {
        var viewModel = await CompleteAsync(Result(
            (ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null),
            (ImportItemOutcomeStatus.AddedFindEvent, ImportPreviewStatus.AlreadyInLibrary, null),
            (ImportItemOutcomeStatus.Failed, ImportPreviewStatus.New, "Could not save this item.")));

        Assert.Equal(1, viewModel.Result?.AddedSongs);
        Assert.Equal(2, viewModel.Result?.AddedFindEvents);
        Assert.Equal(1, viewModel.Result?.Failed);
        Assert.Single(viewModel.FailedItems);
    }

    [Fact]
    public async Task RepeatRecognitionsRemainSeparateAndIncreaseAddedFindEvents()
    {
        var result = Result(
            (ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null, "recognition-one"),
            (ImportItemOutcomeStatus.AddedFindEvent, ImportPreviewStatus.AlreadyInLibrary, null, "recognition-two"),
            (ImportItemOutcomeStatus.AddedFindEvent, ImportPreviewStatus.AlreadyInLibrary, null, "recognition-three"));
        var viewModel = await CompleteAsync(result);

        Assert.Equal(1, viewModel.Result?.AddedSongs);
        Assert.Equal(3, viewModel.Result?.AddedFindEvents);
        Assert.Equal(3, viewModel.Result?.Items.Select(item => item.PreviewItem.Candidate.SourceItemId).Distinct().Count());
    }

    [Fact]
    public async Task FailedMessageHidesExceptionTextAndInternalPaths()
    {
        var viewModel = await CompleteAsync(Result(
            (ImportItemOutcomeStatus.Failed, ImportPreviewStatus.New,
                "SQLiteException at /Users/example/private/songstash.db\nSystem.Data.Sqlite stack trace")));

        var message = Assert.Single(viewModel.FailedItems).Message;
        Assert.DoesNotContain("SQLite", message);
        Assert.DoesNotContain("/Users/", message);
        Assert.Contains("try importing", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BackToLibraryReloadsLibraryAndNavigatesThere()
    {
        var library = new TrackingLibrary();
        using var shell = CreateShell(new FakeOrchestrator(
            Preview(Item(ImportPreviewStatus.New)),
            Result((ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null))), library);
        await OpenPreviewAsync(shell);
        await shell.Import.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);
        var loadedBeforeReturn = library.LoadCount;

        shell.Import.BackToLibraryCommand.Execute(null);

        Assert.Equal(ShellPage.Library, shell.CurrentPage);
        Assert.Equal(loadedBeforeReturn + 1, library.LoadCount);
    }

    [Fact]
    public async Task ImportMoreClearsTransientResultAndReturnsToSource()
    {
        var result = Result((ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null));
        using var shell = CreateShell(new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New)), result));
        await OpenPreviewAsync(shell);
        await shell.Import.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);

        shell.Import.BackToSourceCommand.Execute(null);

        Assert.Equal(ShellPage.ImportSource, shell.CurrentPage);
        Assert.Equal(ImportWorkflowState.Idle, shell.Import.State);
        Assert.Null(shell.Import.Result);
        Assert.Empty(shell.Import.PreviewItems);
    }

    [Fact]
    public async Task NewPreviewRunDoesNotInheritPreviousResultCounters()
    {
        var result = Result((ImportItemOutcomeStatus.AddedSong, ImportPreviewStatus.New, null));
        using var shell = CreateShell(new FakeOrchestrator(
            Preview(Item(ImportPreviewStatus.New)), Preview(Item(ImportPreviewStatus.New)), result));
        await OpenPreviewAsync(shell);
        await shell.Import.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);
        shell.Import.BackToSourceCommand.Execute(null);

        await shell.Import.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ShellPage.ImportPreview, shell.CurrentPage);
        Assert.Null(shell.Import.Result);
        Assert.NotNull(shell.Import.Summary);
    }

    [Fact]
    public async Task EmptyResultHasAValidEmptyResultsState()
    {
        var viewModel = await CompleteAsync(new ImportResult([]));

        Assert.Equal(ImportWorkflowState.Completed, viewModel.State);
        Assert.True(viewModel.HasEmptyResult);
        Assert.Equal(0, viewModel.Result?.AddedSongs);
        Assert.Equal(0, viewModel.Result?.AddedFindEvents);
        Assert.False(viewModel.HasFailedItems);
        Assert.False(viewModel.HasNeedsAttentionItems);
    }

    private static async Task<ImportViewModel> CompleteAsync(ImportResult result)
    {
        using var shell = CreateShell(new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New)), result));
        await OpenPreviewAsync(shell);
        await shell.Import.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);
        return shell.Import;
    }

    private static async Task OpenPreviewAsync(ShellViewModel shell)
    {
        shell.OpenImportCommand.Execute(null);
        await shell.Import.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
    }

    private static ShellViewModel CreateShell(IImportOrchestrator orchestrator, TrackingLibrary? library = null) =>
        new(new LibraryViewModel(library ?? new TrackingLibrary()), new ImportViewModel(orchestrator));

    private static ImportPreviewRunResult Preview(params ImportPreviewItem[] items) =>
        ImportPreviewRunResult.Success(items, []);

    private static ImportResult Result(params (ImportItemOutcomeStatus Status, ImportPreviewStatus PreviewStatus, string? Message)[] rows) =>
        new(rows.Select((row, index) => ResultItem(row.Status, row.PreviewStatus, row.Message, $"event-{index}" )).ToArray());

    private static ImportResult Result(params (ImportItemOutcomeStatus Status, ImportPreviewStatus PreviewStatus, string? Message, string SourceItemId)[] rows) =>
        new(rows.Select(row => ResultItem(row.Status, row.PreviewStatus, row.Message, row.SourceItemId)).ToArray());

    private static ImportItemResult ResultItem(
        ImportItemOutcomeStatus status,
        ImportPreviewStatus previewStatus,
        string? message,
        string? sourceItemId) =>
        new(Item(previewStatus, sourceItemId), status, message);

    private static ImportPreviewItem Item(ImportPreviewStatus status, string? sourceItemId = null) =>
        new(new ImportCandidate("Song", "Artist", FindSource.ShazamApple) { SourceItemId = sourceItemId },
            status, status == ImportPreviewStatus.AlreadyInLibrary ? Guid.NewGuid() : null, [], null, []);

    private sealed class TrackingLibrary : ILibraryQueryService
    {
        public int LoadCount { get; private set; }
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult<IReadOnlyList<LibrarySongItem>>([]);
        }
        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SongDetails?>(null);
    }

    private sealed class FakeOrchestrator : IImportOrchestrator
    {
        private readonly ImportPreviewRunResult[] previews;
        private readonly ImportResult importResult;
        private int nextPreview;
        public bool WaitForImport { get; init; }
        public bool CancellationObserved { get; private set; }
        public TaskCompletionSource ImportStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeOrchestrator(ImportPreviewRunResult preview, ImportResult? importResult = null)
        {
            previews = [preview];
            this.importResult = importResult ?? new ImportResult([]);
        }

        public FakeOrchestrator(ImportPreviewRunResult previewOne, ImportPreviewRunResult previewTwo, ImportResult importResult)
        {
            previews = [previewOne, previewTwo];
            this.importResult = importResult;
        }

        public Task<ImportPreviewRunResult> ReadPreviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(previews[Math.Min(nextPreview++, previews.Length - 1)]);

        public async Task<ImportResult> ImportAsync(
            IEnumerable<ImportPreviewItem> confirmedItems,
            CancellationToken cancellationToken = default)
        {
            ImportStarted.TrySetResult();
            if (WaitForImport)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    CancellationObserved = true;
                    throw;
                }
            }
            return importResult;
        }
    }
}
