using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Desktop.ViewModels;
using SongStash.Domain.Finds;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class ImportViewModelTests
{
    [Fact]
    public async Task FileSourceUsesSamePreviewAndImportPipeline()
    {
        var orchestrator = new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New)));
        var factory = new FakeFileReaderFactory();
        var viewModel = new ImportViewModel(orchestrator, factory);

        await viewModel.ReadFileAsync("/tmp/sample.csv");
        Assert.Equal(ImportWorkflowState.PreviewReady, viewModel.State);
        Assert.True(viewModel.IsFileSource);
        Assert.Equal("sample.csv", viewModel.SelectedFileName);
        Assert.Same(factory.Reader, orchestrator.SelectedReader);
        await viewModel.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);
        Assert.Single(orchestrator.ImportedItems);
    }

    [Fact]
    public async Task FilePickerCancellationDoesNotChangeImportState()
    {
        var orchestrator = new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New)));
        var factory = new FakeFileReaderFactory();
        var viewModel = new ImportViewModel(orchestrator, factory);

        await viewModel.ReadFileAsync(null);

        Assert.Equal(ImportWorkflowState.Idle, viewModel.State);
        Assert.False(viewModel.IsFileSource);
        Assert.Equal(0, factory.Calls);
        Assert.Empty(viewModel.PreviewItems);
    }

    [Fact]
    public async Task SuccessfulShazamReadMovesToPreviewReady()
    {
        var orchestrator = new FakeOrchestrator(Preview([Item(ImportPreviewStatus.New)]));
        var viewModel = new ImportViewModel(orchestrator);

        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ImportWorkflowState.PreviewReady, viewModel.State);
        Assert.Single(viewModel.PreviewItems);
    }

    [Fact]
    public async Task UnavailableSourceHasControlledErrorAndKeepsDiagnostics()
    {
        var diagnostic = Diagnostic("source-unavailable");
        var viewModel = new ImportViewModel(new FakeOrchestrator(
            ImportPreviewRunResult.Failure(ImportPreviewRunStatus.SourceUnavailable, [diagnostic])));

        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ImportWorkflowState.SourceUnavailable, viewModel.State);
        Assert.Contains("unavailable", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(diagnostic, viewModel.Diagnostics);
    }

    [Fact]
    public async Task UnsupportedSchemaHasControlledError()
    {
        var viewModel = new ImportViewModel(new FakeOrchestrator(
            ImportPreviewRunResult.Failure(ImportPreviewRunStatus.UnsupportedSchema, [Diagnostic("schema.unsupported")])));

        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ImportWorkflowState.UnsupportedSchema, viewModel.State);
        Assert.Contains("not supported", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SummaryCountsNewCandidates()
    {
        var viewModel = await ReadAsync(Item(ImportPreviewStatus.New), Item(ImportPreviewStatus.New));
        Assert.Equal(2, viewModel.Summary?.NewSongs);
    }

    [Fact]
    public async Task SummaryCountsAlreadyMatchedCandidates()
    {
        var viewModel = await ReadAsync(Item(ImportPreviewStatus.AlreadyInLibrary));
        Assert.Equal(1, viewModel.Summary?.AlreadyInLibrary);
    }

    [Fact]
    public async Task SummaryCountsIncompleteCandidates()
    {
        var viewModel = await ReadAsync(Item(ImportPreviewStatus.Incomplete));
        Assert.Equal(1, viewModel.Summary?.Incomplete);
    }

    [Fact]
    public async Task SummaryCountsReviewCandidates()
    {
        var viewModel = await ReadAsync(Item(ImportPreviewStatus.NeedsReview));
        Assert.Equal(1, viewModel.Summary?.NeedsReview);
    }

    [Fact]
    public async Task NewCandidatesAreSelectedByDefault()
    {
        var viewModel = await ReadAsync(Item(ImportPreviewStatus.New));
        Assert.True(Assert.Single(viewModel.PreviewItems).IsSelected);
    }

    [Fact]
    public async Task AlreadyInLibraryCandidatesAreSelectedByDefault()
    {
        var viewModel = await ReadAsync(Item(ImportPreviewStatus.AlreadyInLibrary));
        Assert.True(Assert.Single(viewModel.PreviewItems).IsSelected);
    }

    [Fact]
    public async Task IncompleteCandidatesAreNotSelectedByDefault()
    {
        var viewModel = await ReadAsync(Item(ImportPreviewStatus.Incomplete));
        Assert.False(Assert.Single(viewModel.PreviewItems).IsSelected);
    }

    [Fact]
    public async Task NeedsReviewCandidatesAreNotSelectedByDefault()
    {
        var viewModel = await ReadAsync(Item(ImportPreviewStatus.NeedsReview));
        Assert.False(Assert.Single(viewModel.PreviewItems).IsSelected);
    }

    [Fact]
    public async Task ImportSendsOnlySelectedPreviewItems()
    {
        var selectedItem = Item(ImportPreviewStatus.New);
        var unselectedItem = Item(ImportPreviewStatus.New);
        var orchestrator = new FakeOrchestrator(Preview(selectedItem, unselectedItem));
        var viewModel = new ImportViewModel(orchestrator);
        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
        viewModel.PreviewItems[1].IsSelected = false;

        await viewModel.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(new[] { selectedItem }, orchestrator.ImportedItems);
    }

    [Fact]
    public async Task IncompleteAndNeedsReviewCannotBeSelectedOrImported()
    {
        var incomplete = Item(ImportPreviewStatus.Incomplete);
        var needsReview = Item(ImportPreviewStatus.NeedsReview);
        var orchestrator = new FakeOrchestrator(Preview(incomplete, needsReview));
        var viewModel = new ImportViewModel(orchestrator);
        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        foreach (var row in viewModel.PreviewItems)
        {
            Assert.False(row.CanSelect);
            row.IsSelected = true;
            Assert.False(row.IsSelected);
        }

        Assert.False(viewModel.CanImport);
        Assert.False(viewModel.ImportSelectedCommand.CanExecute(null));
        Assert.Empty(orchestrator.ImportedItems);
    }

    [Fact]
    public async Task UnknownFoundAtUsesNeutralLabel()
    {
        var item = Item(ImportPreviewStatus.New) with
        {
            Candidate = Item(ImportPreviewStatus.New).Candidate with { FoundAt = null }
        };
        var viewModel = await ReadAsync(item);
        Assert.Equal("Unknown", Assert.Single(viewModel.PreviewItems).FoundAtText);
    }

    [Fact]
    public async Task LargeHistoryCreatesSeparateRowsWithoutChangingApplicationContracts()
    {
        var items = Enumerable.Range(0, 500)
            .Select(index => Item(ImportPreviewStatus.New, $"recognition-{index}"))
            .ToArray();
        var viewModel = await ReadAsync(items);

        Assert.Equal(500, viewModel.PreviewItems.Count);
        Assert.Equal(500, viewModel.Summary?.TotalCandidates);
        Assert.Equal(500, viewModel.PreviewItems.Select(row => row.PreviewItem.Candidate.SourceItemId).Distinct().Count());
    }

    [Fact]
    public async Task CompletedStateContainsImportResultAndRequestsLibraryRefresh()
    {
        var item = Item(ImportPreviewStatus.New);
        var result = new ImportResult([new ImportItemResult(item, ImportItemOutcomeStatus.AddedSong)]);
        var orchestrator = new FakeOrchestrator(Preview(item), result);
        var viewModel = new ImportViewModel(orchestrator);
        var refreshRequested = false;
        viewModel.LibraryRefreshRequested += () => refreshRequested = true;
        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        await viewModel.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ImportWorkflowState.Completed, viewModel.State);
        Assert.Same(result, viewModel.Result);
        Assert.True(refreshRequested);
    }

    [Fact]
    public async Task CancellationIsNotReportedAsGenericError()
    {
        var orchestrator = new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New))) { WaitForCancellation = true };
        var viewModel = new ImportViewModel(orchestrator);
        var operation = viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
        await orchestrator.ReadStarted.Task;
        viewModel.CancelCommand.Execute(null);
        await operation;

        Assert.Equal(ImportWorkflowState.Cancelled, viewModel.State);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ImportCancellationIsNotReportedAsGenericError()
    {
        var orchestrator = new FakeOrchestrator(Preview(Item(ImportPreviewStatus.New)))
        {
            WaitForImportCancellation = true
        };
        var viewModel = new ImportViewModel(orchestrator);
        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
        var operation = viewModel.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);
        await orchestrator.ImportStarted.Task;
        viewModel.CancelCommand.Execute(null);
        await operation;

        Assert.Equal(ImportWorkflowState.Cancelled, viewModel.State);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task FailedItemRemainsInCompletedImportResult()
    {
        var item = Item(ImportPreviewStatus.New);
        var result = new ImportResult([new ImportItemResult(item, ImportItemOutcomeStatus.Failed, "Could not save item.")]);
        var viewModel = new ImportViewModel(new FakeOrchestrator(Preview(item), result));
        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        await viewModel.ImportSelectedCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ImportWorkflowState.Completed, viewModel.State);
        Assert.Equal(1, viewModel.Result?.Failed);
        Assert.Same(item, Assert.Single(viewModel.Result!.Items).PreviewItem);
    }

    [Fact]
    public async Task RepeatRecognitionCandidatesRemainSeparatePreviewRows()
    {
        var first = Item(ImportPreviewStatus.New, "recognition-one");
        var second = Item(ImportPreviewStatus.New, "recognition-two");
        var viewModel = await ReadAsync(first, second);

        Assert.Equal(2, viewModel.PreviewItems.Count);
        Assert.NotEqual(viewModel.PreviewItems[0].PreviewItem.Candidate.SourceItemId,
            viewModel.PreviewItems[1].PreviewItem.Candidate.SourceItemId);
    }

    [Fact]
    public async Task EmptyHistoryIsAValidEmptyPreview()
    {
        var viewModel = await ReadAsync();

        Assert.Equal(ImportWorkflowState.PreviewReady, viewModel.State);
        Assert.Equal(0, viewModel.Summary?.TotalCandidates);
        Assert.Empty(viewModel.PreviewItems);
        Assert.False(viewModel.CanImport);
    }

    [Fact]
    public async Task StartingPreviewAgainClearsPreviousTransientState()
    {
        var first = ImportPreviewRunResult.Success([Item(ImportPreviewStatus.New)], [Diagnostic("first.warning")]);
        var second = Preview();
        var viewModel = new ImportViewModel(new FakeOrchestrator(first, second));
        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
        Assert.Single(viewModel.PreviewItems);

        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        Assert.Empty(viewModel.PreviewItems);
        Assert.Empty(viewModel.Diagnostics);
        Assert.Equal(0, viewModel.Summary?.TotalCandidates);
        Assert.Null(viewModel.Result);
    }

    [Fact]
    public async Task ReaderExceptionBecomesSafeFailureDiagnostic()
    {
        var orchestrator = new FakeOrchestrator(Preview()) { ThrowOnRead = true };
        var viewModel = new ImportViewModel(orchestrator);

        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ImportWorkflowState.Error, viewModel.State);
        Assert.DoesNotContain("sensitive detail", viewModel.ErrorMessage);
        Assert.Contains(viewModel.Diagnostics, item => item.Code == "import.preview.failed");
    }

    private static async Task<ImportViewModel> ReadAsync(params ImportPreviewItem[] items)
    {
        var viewModel = new ImportViewModel(new FakeOrchestrator(Preview(items)));
        await viewModel.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
        return viewModel;
    }

    private static ImportPreviewRunResult Preview(params ImportPreviewItem[] items) =>
        ImportPreviewRunResult.Success(items, []);

    private static ImportPreviewItem Item(ImportPreviewStatus status, string? sourceItemId = null)
    {
        var candidate = new ImportCandidate("Song", "Artist", FindSource.ShazamApple)
        {
            SourceItemId = sourceItemId,
            ExternalReferences = [new ExternalReferenceCandidate(
                SongStash.Domain.ExternalReferences.ExternalReferenceProvider.Shazam, "same-track-key")]
        };
        return new ImportPreviewItem(candidate, status,
            status == ImportPreviewStatus.AlreadyInLibrary ? Guid.NewGuid() : null, [], null, []);
    }

    private static ImportDiagnostic Diagnostic(string code) =>
        new(ImportDiagnosticSeverity.Warning, code, "Source diagnostic.");

    private sealed class FakeOrchestrator : IImportOrchestrator
    {
        private readonly ImportPreviewRunResult[] previews;
        private readonly ImportResult? configuredImportResult;
        private int nextPreview;
        public bool WaitForCancellation { get; init; }
        public bool WaitForImportCancellation { get; init; }
        public bool ThrowOnRead { get; init; }
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ImportStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<ImportPreviewItem> ImportedItems { get; private set; } = [];
        public IImportSourceReader? SelectedReader { get; private set; }
        public FakeOrchestrator(params ImportPreviewRunResult[] previews)
        {
            this.previews = previews;
            configuredImportResult = null;
        }

        public FakeOrchestrator(ImportPreviewRunResult preview, ImportResult? importResult)
        {
            previews = [preview];
            configuredImportResult = importResult;
        }

        public async Task<ImportPreviewRunResult> ReadPreviewAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("sensitive detail");
            }
            ReadStarted.TrySetResult();
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return previews[Math.Min(nextPreview++, previews.Length - 1)];
        }

        public Task<ImportPreviewRunResult> ReadPreviewAsync(
            IImportSourceReader sourceReader,
            CancellationToken cancellationToken = default)
        {
            SelectedReader = sourceReader;
            return ReadPreviewAsync(cancellationToken);
        }

        public Task<ImportResult> ImportAsync(
            IEnumerable<ImportPreviewItem> confirmedItems,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImportedItems = confirmedItems.ToArray();
            if (WaitForImportCancellation)
            {
                return WaitForImportCancellationAsync(cancellationToken);
            }
            return Task.FromResult(configuredImportResult ?? new ImportResult([]));
        }

        private async Task<ImportResult> WaitForImportCancellationAsync(CancellationToken cancellationToken)
        {
            ImportStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return configuredImportResult ?? new ImportResult([]);
        }
    }

    private sealed class FakeFileReaderFactory : IFileImportSourceReaderFactory
    {
        public FakeReader Reader { get; } = new();
        public int Calls { get; private set; }
        public IImportSourceReader Create(string path) { Calls++; return Reader; }
    }

    private sealed class FakeReader : IImportSourceReader
    {
        public Task<ImportReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ImportReadResult.Success());
    }
}
