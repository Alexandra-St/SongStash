using SongStash.Application.Importing;
using SongStash.Application.Library;
using SongStash.Desktop.ViewModels;
using SongStash.Domain.Finds;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class ImportNavigationTests
{
    [Fact]
    public void ImportNavigationOpensSourceScreen()
    {
        using var shell = CreateShell(new FakeOrchestrator(Preview()));

        shell.OpenImportCommand.Execute(null);

        Assert.Equal(ShellPage.ImportSource, shell.CurrentPage);
    }

    [Fact]
    public async Task ShazamSourceCommandStartsLoadingThenOpensPreviewOnSuccess()
    {
        var orchestrator = new FakeOrchestrator(Preview(Item()));
        using var shell = CreateShell(orchestrator);
        shell.OpenImportCommand.Execute(null);
        orchestrator.WaitForRead = true;

        var read = shell.Import.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
        await orchestrator.ReadStarted.Task;
        Assert.Equal(ImportWorkflowState.Loading, shell.Import.State);
        Assert.Equal(ShellPage.ImportSource, shell.CurrentPage);
        shell.Import.CancelCommand.Execute(null);
        await read;
        Assert.Equal(ImportWorkflowState.Cancelled, shell.Import.State);

        orchestrator.WaitForRead = false;
        await shell.Import.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ImportWorkflowState.PreviewReady, shell.Import.State);
        Assert.Equal(ShellPage.ImportPreview, shell.CurrentPage);
    }

    [Fact]
    public async Task BackReturnsToSourceAndClearsOldPreview()
    {
        using var shell = CreateShell(new FakeOrchestrator(Preview(Item())));
        shell.OpenImportCommand.Execute(null);
        await shell.Import.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
        Assert.Equal(ShellPage.ImportPreview, shell.CurrentPage);

        shell.Import.BackToSourceCommand.Execute(null);

        Assert.Equal(ShellPage.ImportSource, shell.CurrentPage);
        Assert.Equal(ImportWorkflowState.Idle, shell.Import.State);
        Assert.Empty(shell.Import.PreviewItems);
    }

    [Fact]
    public async Task ReopeningImportResetsTransientPreviewState()
    {
        using var shell = CreateShell(new FakeOrchestrator(Preview(Item())));
        shell.OpenImportCommand.Execute(null);
        await shell.Import.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);
        shell.ShowLibraryCommand.Execute(null);

        shell.OpenImportCommand.Execute(null);

        Assert.Equal(ShellPage.ImportSource, shell.CurrentPage);
        Assert.Equal(ImportWorkflowState.Idle, shell.Import.State);
        Assert.Empty(shell.Import.PreviewItems);
    }

    [Theory]
    [InlineData(ImportPreviewRunStatus.SourceUnavailable, ImportWorkflowState.SourceUnavailable)]
    [InlineData(ImportPreviewRunStatus.UnsupportedSchema, ImportWorkflowState.UnsupportedSchema)]
    [InlineData(ImportPreviewRunStatus.Failed, ImportWorkflowState.Error)]
    public async Task SourceFailuresRemainOnSourceScreenWithDistinctState(
        ImportPreviewRunStatus loadStatus,
        ImportWorkflowState expectedState)
    {
        var result = ImportPreviewRunResult.Failure(loadStatus, [
            new ImportDiagnostic(ImportDiagnosticSeverity.Error, "source.problem", "A readable diagnostic.")]);
        using var shell = CreateShell(new FakeOrchestrator(result));
        shell.OpenImportCommand.Execute(null);

        await shell.Import.ReadPreviewCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ShellPage.ImportSource, shell.CurrentPage);
        Assert.Equal(expectedState, shell.Import.State);
        Assert.Single(shell.Import.Diagnostics);
    }

    private static ShellViewModel CreateShell(IImportOrchestrator orchestrator) => new(
        new LibraryViewModel(new EmptyLibrary()), new ImportViewModel(orchestrator));

    private static ImportPreviewRunResult Preview(params ImportPreviewItem[] items) =>
        ImportPreviewRunResult.Success(items, []);

    private static ImportPreviewItem Item()
    {
        var candidate = new ImportCandidate("Song", "Artist", FindSource.ShazamApple);
        return new ImportPreviewItem(candidate, ImportPreviewStatus.New, null, [], null, []);
    }

    private sealed class EmptyLibrary : ILibraryQueryService
    {
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibrarySongItem>>([]);
        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SongDetails?>(null);
    }

    private sealed class FakeOrchestrator(params ImportPreviewRunResult[] results) : IImportOrchestrator
    {
        private int nextResult;
        public bool WaitForRead { get; set; }
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ImportPreviewRunResult> ReadPreviewAsync(CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            if (WaitForRead)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return results[Math.Min(nextResult++, results.Length - 1)];
        }

        public Task<ImportResult> ImportAsync(
            IEnumerable<ImportPreviewItem> confirmedItems,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImportResult([]));
    }
}
