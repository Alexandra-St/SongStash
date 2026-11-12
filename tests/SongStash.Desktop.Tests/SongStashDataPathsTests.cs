using SongStash.Application.Backup;
using SongStash.Application.Importing;
using SongStash.Application.Settings;
using SongStash.Desktop.Services;
using SongStash.Desktop.ViewModels;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class SongStashDataPathsTests
{
    [Fact]
    public void MissingOverridePreservesProductionPathLayout()
    {
        var paths = SongStashDataPaths.Resolve(null, "/app-support");
        Assert.Equal(Path.GetFullPath("/app-support/SongStash"), paths.RootDirectory);
        Assert.Equal(Path.Combine(paths.RootDirectory, "songstash.db"), paths.DatabasePath);
        Assert.Equal(Path.Combine(paths.RootDirectory, "settings.json"), paths.SettingsPath);
        Assert.Equal(Path.Combine(paths.RootDirectory, "Backups"), paths.BackupDirectory);
    }

    [Fact]
    public void ExplicitOverrideRedirectsAllSongStashRuntimeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "songstash-test-data-root");
        var paths = SongStashDataPaths.Resolve(root, "/app-support");
        Assert.Equal(Path.GetFullPath(root), paths.RootDirectory);
        Assert.StartsWith(paths.RootDirectory, paths.DatabasePath, StringComparison.Ordinal);
        Assert.StartsWith(paths.RootDirectory, paths.SettingsPath, StringComparison.Ordinal);
        Assert.StartsWith(paths.RootDirectory, paths.BackupDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentOverrideIsUsedWhenPresent()
    {
        var previous = Environment.GetEnvironmentVariable(SongStashDataPaths.OverrideEnvironmentVariable);
        var expected = Path.Combine(Path.GetTempPath(), "songstash-env-test");
        try
        {
            Environment.SetEnvironmentVariable(SongStashDataPaths.OverrideEnvironmentVariable, expected);
            var paths = SongStashDataPaths.ResolveFromEnvironment();
            Assert.Equal(Path.GetFullPath(expected), paths.RootDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SongStashDataPaths.OverrideEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void BackupViewModelUsesOverriddenDefaultBackupLocation()
    {
        var paths = SongStashDataPaths.Resolve("/tmp/songstash-isolated", "/app-support");
        var viewModel = new BackupViewModel(new EmptyBackupService(), new SettingsLoadResult(new AppSettings()),
            new NoOpLauncher(), new FixedClock(), paths);
        Assert.Equal(Path.Combine(paths.RootDirectory, "Backups"), viewModel.BackupFolder);
        Assert.StartsWith(paths.RootDirectory, viewModel.BackupFolder, StringComparison.Ordinal);
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }
    private sealed class NoOpLauncher : IExternalUriLauncher { public void Open(Uri uri) { } }
    private sealed class EmptyBackupService : IBackupService
    {
        public Task<BackupOperationResult> CreateAsync(string folderPath, CancellationToken cancellationToken = default) => Task.FromResult(new BackupOperationResult(true, null));
        public Task<BackupInspectionResult> InspectAsync(string backupPath, CancellationToken cancellationToken = default) => Task.FromResult(new BackupInspectionResult(false, null, null));
        public Task<BackupOperationResult> RestoreAsync(string backupPath, BackupRestoreMode mode, bool restoreSettings, CancellationToken cancellationToken = default) => Task.FromResult(new BackupOperationResult(false, null));
        public Task<IReadOnlyList<BackupInfo>> ListAsync(string folderPath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BackupInfo>>([]);
        public Task<bool> DeleteAsync(string backupPath, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
