using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text;
using SongStash.Application.Backup;
using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Application.Settings;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Domain.Tags;
using SongStash.Infrastructure.Backup;
using SongStash.Infrastructure.Persistence;
using Xunit;

namespace SongStash.Infrastructure.Tests.Backup;

public sealed class SongStashBackupServiceTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreatedBackupHasVerifiedManifestAndCompleteSqliteSnapshot()
    {
        await using var source = await TempDb.CreateAsync();
        var song = new Song("Night Drive", "Artist", "Album", new Uri("https://example.test/art"), "Synth", "Keep this note.");
        song.SetFavorite(true);
        await using (var context = source.Context())
        {
            context.Songs.Add(song);
            context.FindEvents.AddRange(
                new FindEvent(song.Id, FindSource.ShazamApple, Time, Time.AddDays(-1), "rec-a"),
                new FindEvent(song.Id, FindSource.ShazamApple, Time, Time, "rec-b"));
            context.ExternalReferences.Add(new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "track-key"));
            context.Tags.Add(new Tag("Road trip"));
            await context.SaveChangesAsync();
        }
        var folder = Path.Combine(source.Folder, "backups");
        var store = new MemorySettingsStore(new AppSettings { Language = AppLanguage.Russian });
        await using var serviceContext = source.Context();
        var service = CreateService(serviceContext, store);

        var created = await service.CreateAsync(folder);

        Assert.True(created.Success, created.ErrorMessage);
        Assert.EndsWith(SongStashBackupService.BackupExtension, created.Backup!.Path);
        var inspected = await service.InspectAsync(created.Backup.Path);
        Assert.True(inspected.IsValid, inspected.ErrorMessage);
        Assert.Equal("SongStash.Backup", inspected.Backup!.Manifest.Marker);
        Assert.Equal(1, inspected.Backup.Manifest.FormatVersion);
        Assert.Equal(1, inspected.Backup.Manifest.Songs);
        Assert.Equal(2, inspected.Backup.Manifest.FindEvents);
        Assert.Equal(1, inspected.Backup.Manifest.ExternalReferences);
        Assert.Equal(1, inspected.Backup.Manifest.Tags);
        Assert.True(inspected.Backup.ContainsSettings);
        Assert.Single(await service.ListAsync(folder));
    }

    [Fact]
    public async Task MergeIntoEmptyLibraryRestoresSongMetadataReferencesTagsAndRepeatEvents()
    {
        await using var source = await TempDb.CreateAsync();
        var song = new Song("Night Drive", "Artist", "Album", null, "Synth", "Notes", Time);
        song.SetFavorite(true);
        await using (var context = source.Context())
        {
            context.Songs.Add(song);
            context.FindEvents.AddRange(
                new FindEvent(song.Id, FindSource.ShazamApple, Time, Time.AddMinutes(-1), "recognition-one"),
                new FindEvent(song.Id, FindSource.ShazamApple, Time, Time, "recognition-two"));
            context.ExternalReferences.Add(new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "same-track-key"));
            context.Tags.Add(new Tag("Road trip"));
            await context.SaveChangesAsync();
        }
        var backupFolder = Path.Combine(source.Folder, "backups");
        string backupPath;
        await using (var context = source.Context())
        {
            var result = await CreateService(context, new MemorySettingsStore()).CreateAsync(backupFolder);
            backupPath = result.Backup!.Path;
        }

        await using var target = await TempDb.CreateAsync();
        await using var targetContext = target.Context();
        var restore = await CreateService(targetContext, new MemorySettingsStore()).RestoreAsync(backupPath, BackupRestoreMode.Merge, false);

        Assert.True(restore.Success, restore.ErrorMessage);
        targetContext.ChangeTracker.Clear();
        var restoredSong = await targetContext.Songs.SingleAsync();
        Assert.Equal("Notes", restoredSong.Notes);
        Assert.True(restoredSong.IsFavorite);
        Assert.Equal(2, await targetContext.FindEvents.CountAsync());
        Assert.Single(await targetContext.ExternalReferences.ToListAsync());
        Assert.Equal("Road trip", (await targetContext.Tags.SingleAsync()).Name);
    }

    [Fact]
    public async Task MergeUsesMatcherAndEventDeduplicatorWithoutLosingRepeatRecognition()
    {
        await using var source = await TempDb.CreateAsync();
        var sourceSong = new Song("Night Drive", "Artist");
        await using (var context = source.Context())
        {
            context.Songs.Add(sourceSong);
            context.FindEvents.AddRange(
                new FindEvent(sourceSong.Id, FindSource.ShazamApple, Time, Time.AddMinutes(-2), "event-1"),
                new FindEvent(sourceSong.Id, FindSource.ShazamApple, Time, Time, "event-2"));
            await context.SaveChangesAsync();
        }
        string backupPath;
        await using (var context = source.Context())
        {
            backupPath = (await CreateService(context, new MemorySettingsStore()).CreateAsync(Path.Combine(source.Folder, "backups"))).Backup!.Path;
        }
        await using var target = await TempDb.CreateAsync();
        var targetSong = new Song("Night Drive", "Artist");
        await using (var context = target.Context())
        {
            context.Songs.Add(targetSong);
            context.FindEvents.Add(new FindEvent(targetSong.Id, FindSource.ShazamApple, Time, Time.AddMinutes(-2), "event-1"));
            await context.SaveChangesAsync();
        }

        await using (var context = target.Context())
        {
            var result = await CreateService(context, new MemorySettingsStore()).RestoreAsync(backupPath, BackupRestoreMode.Merge, false);
            Assert.True(result.Success, result.ErrorMessage);
        }
        await using var verify = target.Context();
        Assert.Single(await verify.Songs.ToListAsync());
        Assert.Equal(new[] { "event-1", "event-2" }, (await verify.FindEvents.OrderBy(item => item.SourceItemId).ToListAsync()).Select(item => item.SourceItemId));
    }

    [Fact]
    public async Task ReplaceReplacesAllLibraryTablesAndRestoresSettingsOnlyWhenSelected()
    {
        await using var source = await TempDb.CreateAsync();
        var original = new Song("Backed up", "Artist", notes: "original");
        await using (var context = source.Context())
        {
            context.Songs.Add(original);
            context.FindEvents.Add(new FindEvent(original.Id, FindSource.Manual, Time, Time, "manual-event"));
            await context.SaveChangesAsync();
        }
        var folder = Path.Combine(source.Folder, "backups");
        string backupPath;
        await using (var context = source.Context())
            backupPath = (await CreateService(context, new MemorySettingsStore(new AppSettings { Language = AppLanguage.Russian })).CreateAsync(folder)).Backup!.Path;

        await using var target = await TempDb.CreateAsync();
        var current = new Song("Current", "Artist");
        await using (var context = target.Context()) { context.Songs.Add(current); await context.SaveChangesAsync(); }
        var store = new MemorySettingsStore(new AppSettings { Language = AppLanguage.English });
        await using (var context = target.Context())
        {
            var result = await CreateService(context, store).RestoreAsync(backupPath, BackupRestoreMode.Replace, restoreSettings: false);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Null(result.RestoredSettings);
        }
        await using (var verify = target.Context())
        {
            Assert.Equal("Backed up", (await verify.Songs.SingleAsync()).Title);
            Assert.Equal("original", (await verify.Songs.SingleAsync()).Notes);
            Assert.Single(await verify.FindEvents.ToListAsync());
        }
        Assert.Equal(AppLanguage.English, store.Current.Language);

        await using (var context = target.Context())
        {
            var result = await CreateService(context, store).RestoreAsync(backupPath, BackupRestoreMode.Replace, restoreSettings: true);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(AppLanguage.Russian, result.RestoredSettings!.Language);
        }
        Assert.Equal(AppLanguage.Russian, store.Current.Language);
    }

    [Fact]
    public async Task InvalidBackupIsRejectedBeforeCurrentLibraryChanges()
    {
        await using var target = await TempDb.CreateAsync();
        var song = new Song("Keep me", "Artist");
        await using (var context = target.Context()) { context.Songs.Add(song); await context.SaveChangesAsync(); }
        var corrupt = Path.Combine(target.Folder, "bad.songstashbackup");
        await File.WriteAllTextAsync(corrupt, "not a backup");
        await using (var context = target.Context())
        {
            var result = await CreateService(context, new MemorySettingsStore()).RestoreAsync(corrupt, BackupRestoreMode.Replace, false);
            Assert.False(result.Success);
        }
        await using var verify = target.Context();
        Assert.Equal("Keep me", (await verify.Songs.SingleAsync()).Title);
    }

    [Fact]
    public async Task ReplaceFailureRollsBackDeletedLibraryRows()
    {
        await using var source = await TempDb.CreateAsync();
        var backupSong = new Song("Backup song", "Artist");
        await using (var context = source.Context()) { context.Songs.Add(backupSong); context.FindEvents.Add(new FindEvent(backupSong.Id, FindSource.Manual, Time)); await context.SaveChangesAsync(); }
        string backupPath;
        await using (var context = source.Context())
            backupPath = (await CreateService(context, new MemorySettingsStore()).CreateAsync(Path.Combine(source.Folder, "backups"))).Backup!.Path;

        await using var target = await TempDb.CreateAsync();
        await using (var context = target.Context())
        {
            var keep = new Song("Keep on failure", "Artist");
            context.Songs.Add(keep);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailBackupRestore BEFORE INSERT ON FindEvents BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        }
        await using (var context = target.Context())
        {
            var result = await CreateService(context, new MemorySettingsStore()).RestoreAsync(backupPath, BackupRestoreMode.Replace, false);
            Assert.False(result.Success);
        }
        await using var verify = target.Context();
        Assert.Equal("Keep on failure", (await verify.Songs.SingleAsync()).Title);
        Assert.Empty(await verify.FindEvents.ToListAsync());
    }

    [Fact]
    public async Task CsvJsonAndUnknownZipFilesAreNotRecognizedAsBackups()
    {
        await using var target = await TempDb.CreateAsync();
        await using var context = target.Context();
        var service = CreateService(context, new MemorySettingsStore());
        var fake = Path.Combine(target.Folder, "library.json");
        await File.WriteAllTextAsync(fake, "{\"songs\":[]}");
        var result = await service.InspectAsync(fake);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UnknownFormatVersionAndCorruptDatabaseAreRejected(bool unknownVersion, bool corruptDatabase)
    {
        await using var db = await TempDb.CreateAsync();
        await using var context = db.Context();
        var service = CreateService(context, new MemorySettingsStore());
        var source = await service.CreateAsync(Path.Combine(db.Folder, "source"));
        var alteredPath = Path.Combine(db.Folder, $"altered-{unknownVersion}-{corruptDatabase}.songstashbackup");
        await RewriteBackupAsync(source.Backup!.Path, alteredPath, unknownVersion, corruptDatabase);
        var inspected = await service.InspectAsync(alteredPath);
        Assert.False(inspected.IsValid);
    }

    [Fact]
    public async Task MergeDoesNotRestoreSettingsUnlessExplicitlySelected()
    {
        await using var source = await TempDb.CreateAsync();
        var song = new Song("A backed-up song", "Artist");
        await using (var context = source.Context()) { context.Songs.Add(song); await context.SaveChangesAsync(); }
        string path;
        await using (var context = source.Context())
            path = (await CreateService(context, new MemorySettingsStore(new AppSettings { Language = AppLanguage.Russian }))
                .CreateAsync(Path.Combine(source.Folder, "backups"))).Backup!.Path;
        await using var target = await TempDb.CreateAsync();
        var store = new MemorySettingsStore(new AppSettings { Language = AppLanguage.English });
        await using (var context = target.Context())
        {
            var restored = await CreateService(context, store).RestoreAsync(path, BackupRestoreMode.Merge, restoreSettings: false);
            Assert.True(restored.Success, restored.ErrorMessage);
            Assert.Null(restored.RestoredSettings);
        }
        Assert.Equal(AppLanguage.English, store.Current.Language);
        await using var verify = target.Context();
        Assert.Equal("A backed-up song", (await verify.Songs.SingleAsync()).Title);
    }

    [Fact]
    public async Task EmptyDatabaseBackupIsValidAndFailedCreateDoesNotChangeLibrary()
    {
        await using var db = await TempDb.CreateAsync();
        var fileAsDirectory = Path.Combine(db.Folder, "not-a-folder");
        await File.WriteAllTextAsync(fileAsDirectory, "keep");
        await using var context = db.Context();
        var service = CreateService(context, new MemorySettingsStore());
        var backupFolder = Path.Combine(db.Folder, "empty-backups");
        var created = await service.CreateAsync(backupFolder);
        Assert.True(created.Success, created.ErrorMessage);
        Assert.Equal(0, created.Backup!.Manifest.Songs);
        Assert.Equal(0, created.Backup.Manifest.FindEvents);
        var failed = await service.CreateAsync(fileAsDirectory);
        Assert.False(failed.Success);
        Assert.Equal("keep", await File.ReadAllTextAsync(fileAsDirectory));
        Assert.Single(await service.ListAsync(backupFolder));
        Assert.Empty(await context.Songs.ToListAsync());
    }

    [Fact]
    public async Task SnapshotIncludesCommittedRowsWhileSourceUsesWal()
    {
        await using var source = await TempDb.CreateAsync();
        await using (var context = source.Context())
        {
            await context.Database.OpenConnectionAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            var song = new Song("Written with WAL", "Artist");
            context.Songs.Add(song);
            context.FindEvents.Add(new FindEvent(song.Id, FindSource.Manual, Time, Time, "wal-event"));
            await context.SaveChangesAsync();
        }
        await using var backupContext = source.Context();
        var result = await CreateService(backupContext, new MemorySettingsStore()).CreateAsync(Path.Combine(source.Folder, "wal-backups"));
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1, result.Backup!.Manifest.Songs);
        Assert.Equal(1, result.Backup.Manifest.FindEvents);
        Assert.True((await CreateService(backupContext, new MemorySettingsStore()).InspectAsync(result.Backup.Path)).IsValid);
    }

    [Fact]
    public async Task ListAndDeleteOperateOnlyOnTheSelectedBackup()
    {
        await using var db = await TempDb.CreateAsync();
        await using var context = db.Context();
        var service = CreateService(context, new MemorySettingsStore());
        var folder = Path.Combine(db.Folder, "backups");
        var first = await service.CreateAsync(folder);
        var second = await service.CreateAsync(folder);
        var listed = await service.ListAsync(folder);
        Assert.Equal(2, listed.Count);
        Assert.True(await service.DeleteAsync(first.Backup!.Path));
        var remaining = await service.ListAsync(folder);
        Assert.Single(remaining);
        Assert.Equal(second.Backup!.Path, remaining[0].Path);
    }

    private static SongStashBackupService CreateService(SongStashDbContext context, MemorySettingsStore store) =>
        new(context, store, new SongMatcher(), new FindEventDeduplicator(), new FixedClock());

    private static async Task RewriteBackupAsync(string sourcePath, string targetPath, bool unknownVersion, bool corruptDatabase)
    {
        using var source = ZipFile.OpenRead(sourcePath);
        using var target = ZipFile.Open(targetPath, ZipArchiveMode.Create);
        foreach (var sourceEntry in source.Entries)
        {
            var targetEntry = target.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
            await using var output = targetEntry.Open();
            if (sourceEntry.FullName == "library.sqlite" && corruptDatabase)
            {
                await output.WriteAsync(Encoding.UTF8.GetBytes("not sqlite"));
                continue;
            }
            await using var input = sourceEntry.Open();
            if (sourceEntry.FullName == "manifest.json" && unknownVersion)
            {
                using var reader = new StreamReader(input, Encoding.UTF8);
                var json = await reader.ReadToEndAsync();
                json = json.Replace("\"formatVersion\":1", "\"formatVersion\":99", StringComparison.Ordinal);
                await output.WriteAsync(Encoding.UTF8.GetBytes(json));
            }
            else await input.CopyToAsync(output);
        }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => Time; }
    private sealed class MemorySettingsStore(AppSettings? settings = null) : ISettingsStore
    {
        public AppSettings Current { get; private set; } = settings ?? new AppSettings();
        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SettingsLoadResult(Current));
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) { Current = settings; return Task.CompletedTask; }
    }
    private sealed class TempDb : IAsyncDisposable
    {
        public string Folder { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"songstash-backup-tests-{Guid.NewGuid():N}");
        private string Path => System.IO.Path.Combine(Folder, "library.sqlite");
        public static async Task<TempDb> CreateAsync()
        {
            var db = new TempDb(); Directory.CreateDirectory(db.Folder);
            await using var context = db.Context(); await context.Database.MigrateAsync(); return db;
        }
        public SongStashDbContext Context() => new(new DbContextOptionsBuilder<SongStashDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = Path }.ToString()).Options);
        public ValueTask DisposeAsync() { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); return ValueTask.CompletedTask; }
    }
}
