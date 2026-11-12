using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SongStash.Application.Backup;
using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Application.Settings;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Infrastructure.Persistence;

namespace SongStash.Infrastructure.Backup;

/// <summary>Creates and restores versioned local archive backups.</summary>
public sealed class SongStashBackupService(
    SongStashDbContext database,
    ISettingsStore settingsStore,
    SongMatcher matcher,
    FindEventDeduplicator eventDeduplicator,
    IClock clock) : IBackupService
{
    public const string BackupExtension = ".songstashbackup";
    private const string Marker = "SongStash.Backup";
    private const int FormatVersion = 1;
    private const string CurrentSchemaVersion = "20261026203933_InitialLibrarySchema";
    private const string ManifestEntry = "manifest.json";
    private const string DatabaseEntry = "library.sqlite";
    private const string SettingsEntry = "settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<BackupOperationResult> CreateAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return Fail("Choose a backup folder first.");
        var work = Path.Combine(Path.GetTempPath(), $"songstash-backup-{Guid.NewGuid():N}");
        var stagedArchive = Path.Combine(Path.GetFullPath(folderPath), $".SongStash-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(folderPath);
            Directory.CreateDirectory(work);
            var databaseSnapshot = Path.Combine(work, DatabaseEntry);
            await CreateDatabaseSnapshotAsync(databaseSnapshot, cancellationToken).ConfigureAwait(false);
            var counts = await ReadDatabaseMetadataAsync(databaseSnapshot, cancellationToken).ConfigureAwait(false);
            if (counts.SchemaVersion != CurrentSchemaVersion) return Fail("The library database schema is not supported for backup.");

            var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var manifest = new BackupManifest(Marker, FormatVersion, clock.UtcNow.ToUniversalTime(), counts.SchemaVersion,
                counts.Songs, counts.FindEvents, counts.ExternalReferences, counts.Tags);
            var finalPath = Path.Combine(Path.GetFullPath(folderPath), $"SongStash_{manifest.CreatedAtUtc:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}" + BackupExtension);
            using (var archive = ZipFile.Open(stagedArchive, ZipArchiveMode.Create))
            {
                await AddFileAsync(archive, databaseSnapshot, DatabaseEntry, cancellationToken).ConfigureAwait(false);
                await AddBytesAsync(archive, ManifestEntry, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
                await AddBytesAsync(archive, SettingsEntry, JsonSerializer.SerializeToUtf8Bytes(settings.Settings, JsonOptions), cancellationToken).ConfigureAwait(false);
            }

            var verification = await InspectAsync(stagedArchive, cancellationToken).ConfigureAwait(false);
            if (!verification.IsValid) return Fail("The backup could not be verified after creation.");
            File.Move(stagedArchive, finalPath);
            var info = verification.Backup! with { Path = finalPath, SizeBytes = new FileInfo(finalPath).Length };
            return new BackupOperationResult(true, null, info);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            return Fail("The backup could not be created. Check the database and backup folder, then try again.");
        }
        finally
        {
            TryDeleteFile(stagedArchive);
            TryDeleteDirectory(work);
        }
    }

    public async Task<BackupInspectionResult> InspectAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath)) return Invalid("Backup file was not found.");
        var work = Path.Combine(Path.GetTempPath(), $"songstash-inspect-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(work);
            BackupManifest? manifest;
            bool hasSettings;
            string dbPath = Path.Combine(work, DatabaseEntry);
            using (var archive = ZipFile.OpenRead(backupPath))
            {
                var manifestEntry = archive.GetEntry(ManifestEntry);
                var databaseEntry = archive.GetEntry(DatabaseEntry);
                if (manifestEntry is null || databaseEntry is null) return Invalid("This file is not a complete SongStash backup.");
                await using (var stream = manifestEntry.Open())
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (manifest is null || manifest.Marker != Marker) return Invalid("This file is not a SongStash backup.");
                if (manifest.FormatVersion != FormatVersion) return Invalid("This backup format version is not supported by this version of SongStash.");
                hasSettings = archive.GetEntry(SettingsEntry) is not null;
                await using var input = databaseEntry.Open();
                await using var output = new FileStream(dbPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            var counts = await ReadDatabaseMetadataAsync(dbPath, cancellationToken).ConfigureAwait(false);
            if (counts.SchemaVersion != manifest.LibrarySchemaVersion || counts.SchemaVersion != CurrentSchemaVersion)
                return Invalid("This backup uses an incompatible library schema.");
            if (counts.Songs != manifest.Songs || counts.FindEvents != manifest.FindEvents ||
                counts.ExternalReferences != manifest.ExternalReferences || counts.Tags != manifest.Tags)
                return Invalid("The backup manifest does not match its library data.");
            var info = new BackupInfo(Path.GetFullPath(backupPath), manifest, new FileInfo(backupPath).Length, hasSettings);
            return new BackupInspectionResult(true, null, info);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or SqliteException)
        {
            return Invalid("The backup is damaged or cannot be read.");
        }
        finally { TryDeleteDirectory(work); }
    }

    public async Task<BackupOperationResult> RestoreAsync(string backupPath, BackupRestoreMode mode, bool restoreSettings,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) return Fail("Choose a valid restore mode.");
        var inspection = await InspectAsync(backupPath, cancellationToken).ConfigureAwait(false);
        if (!inspection.IsValid || inspection.Backup is null) return Fail(inspection.ErrorMessage ?? "The backup is invalid.");
        var work = Path.Combine(Path.GetTempPath(), $"songstash-restore-{Guid.NewGuid():N}");
        var connection = (SqliteConnection)database.Database.GetDbConnection();
        var attached = false;
        try
        {
            Directory.CreateDirectory(work);
            var snapshot = Path.Combine(work, DatabaseEntry);
            AppSettings? restoredSettings = null;
            using (var archive = ZipFile.OpenRead(backupPath))
            {
                await using (var input = archive.GetEntry(DatabaseEntry)!.Open())
                await using (var output = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                if (restoreSettings && archive.GetEntry(SettingsEntry) is { } settingsEntry)
                {
                    await using var input = settingsEntry.Open();
                    restoredSettings = await JsonSerializer.DeserializeAsync<AppSettings>(input, JsonOptions, cancellationToken).ConfigureAwait(false);
                    if (restoredSettings is null) return Fail("Application settings in this backup are invalid.");
                }
            }

            var extractedInfo = await ReadDatabaseMetadataAsync(snapshot, cancellationToken).ConfigureAwait(false);
            if (extractedInfo.SchemaVersion != inspection.Backup.Manifest.LibrarySchemaVersion ||
                extractedInfo.Songs != inspection.Backup.Manifest.Songs || extractedInfo.FindEvents != inspection.Backup.Manifest.FindEvents ||
                extractedInfo.ExternalReferences != inspection.Backup.Manifest.ExternalReferences || extractedInfo.Tags != inspection.Backup.Manifest.Tags)
                return Fail("The backup changed or failed validation before restore.");

            await database.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (var attach = connection.CreateCommand())
            {
                attach.CommandText = "ATTACH DATABASE $backupPath AS backupDb;";
                attach.Parameters.AddWithValue("$backupPath", snapshot);
                await attach.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                attached = true;
            }

            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            if (mode == BackupRestoreMode.Replace)
            {
                await ReplaceLibraryAsync(transaction, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await MergeLibraryAsync(snapshot, transaction, cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            database.ChangeTracker.Clear();

            if (restoredSettings is not null)
            {
                try { await settingsStore.SaveAsync(restoredSettings, CancellationToken.None).ConfigureAwait(false); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return new BackupOperationResult(true, "Library restored, but application settings could not be restored.", inspection.Backup);
                }
            }
            return new BackupOperationResult(true, null, inspection.Backup, restoredSettings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException or DbUpdateException or JsonException or ArgumentException)
        {
            return Fail("The library could not be restored. Your current library was kept unchanged.");
        }
        finally
        {
            if (attached)
            {
                try { await database.Database.ExecuteSqlRawAsync("DETACH DATABASE backupDb", CancellationToken.None).ConfigureAwait(false); }
                catch (Exception) { }
            }
            TryDeleteDirectory(work);
        }
    }

    public async Task<IReadOnlyList<BackupInfo>> ListAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return [];
        var results = new List<BackupInfo>();
        foreach (var path in Directory.EnumerateFiles(folderPath, "*" + BackupExtension).OrderByDescending(File.GetCreationTimeUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inspected = await InspectAsync(path, cancellationToken).ConfigureAwait(false);
            if (inspected.IsValid && inspected.Backup is not null) results.Add(inspected.Backup);
        }
        return results;
    }

    public Task<bool> DeleteAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(backupPath) || !backupPath.EndsWith(BackupExtension, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(false);
        try
        {
            if (!File.Exists(backupPath)) return Task.FromResult(false);
            File.Delete(backupPath);
            return Task.FromResult(!File.Exists(backupPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return Task.FromResult(false); }
    }

    private async Task CreateDatabaseSnapshotAsync(string destination, CancellationToken cancellationToken)
    {
        await database.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var source = (SqliteConnection)database.Database.GetDbConnection();
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination }.ToString());
        await target.OpenAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(target);
    }

    private async Task<DatabaseCounts> ReadDatabaseMetadataAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SQLite integrity check failed.");
        }
        var schema = "";
        await using (var migration = connection.CreateCommand())
        {
            migration.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;";
            schema = Convert.ToString(await migration.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) ?? "";
        }
        var songs = await CountAsync(connection, "Songs", cancellationToken).ConfigureAwait(false);
        var events = await CountAsync(connection, "FindEvents", cancellationToken).ConfigureAwait(false);
        var references = await CountAsync(connection, "ExternalReferences", cancellationToken).ConfigureAwait(false);
        var tags = await CountAsync(connection, "Tags", cancellationToken).ConfigureAwait(false);
        return new DatabaseCounts(schema, songs, events, references, tags);
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task ReplaceLibraryAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await ExecuteAsync("DELETE FROM FindEvents; DELETE FROM ExternalReferences; DELETE FROM Songs; DELETE FROM Tags;", transaction, cancellationToken).ConfigureAwait(false);
        foreach (var table in new[] { "Songs", "FindEvents", "ExternalReferences", "Tags" })
            await ExecuteAsync($"INSERT INTO main.{table} SELECT * FROM backupDb.{table};", transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task MergeLibraryAsync(string snapshot, IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        var sourceOptions = new DbContextOptionsBuilder<SongStashDbContext>().UseSqlite($"Data Source={snapshot}").Options;
        await using var source = new SongStashDbContext(sourceOptions);
        var sourceSongs = await source.Songs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var sourceEvents = await source.FindEvents.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var sourceRefs = await source.ExternalReferences.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var sourceTags = await source.Tags.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var targetSongs = await database.Songs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var targetRefs = await database.ExternalReferences.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var targetEvents = await database.FindEvents.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var songMap = new Dictionary<Guid, Guid>();
        var matcherIdToDatabaseId = new Dictionary<Guid, Guid>();

        foreach (var sourceSong in sourceSongs)
        {
            var refs = sourceRefs.Where(reference => reference.SongId == sourceSong.Id).ToArray();
            var candidate = ToCandidate(sourceSong, refs);
            var matched = matcher.Match(candidate, targetSongs, targetRefs);
            Guid targetId;
            Guid matcherSongId;
            if (matched.Status == SongStash.Application.Matching.SongMatchStatus.ExactMatch && matched.MatchedSong is not null)
            {
                matcherSongId = matched.MatchedSong.Id;
                targetId = matcherIdToDatabaseId.GetValueOrDefault(matched.MatchedSong.Id, matched.MatchedSong.Id);
            }
            else
            {
                targetId = targetSongs.Any(song => song.Id == sourceSong.Id) ? Guid.NewGuid() : sourceSong.Id;
                await ExecuteAsync("INSERT INTO main.Songs SELECT $targetId, Title, Artist, Album, ArtworkUrl, Genre, Notes, IsFavorite, CreatedAt, UpdatedAt FROM backupDb.Songs WHERE Id=$sourceId;",
                    transaction, cancellationToken, new SqliteParameter("$targetId", targetId), new SqliteParameter("$sourceId", sourceSong.Id)).ConfigureAwait(false);
                var matchEntity = new Song(sourceSong.Title, sourceSong.Artist, sourceSong.Album, sourceSong.ArtworkUrl,
                    sourceSong.Genre, sourceSong.Notes, sourceSong.CreatedAt);
                matchEntity.SetFavorite(sourceSong.IsFavorite);
                targetSongs.Add(matchEntity);
                matcherSongId = matchEntity.Id;
                matcherIdToDatabaseId[matchEntity.Id] = targetId;
            }
            songMap[sourceSong.Id] = targetId;

            foreach (var reference in refs)
            {
                var exists = targetRefs.Any(existing => existing.SongId == targetId && existing.Provider == reference.Provider &&
                    existing.ExternalId == reference.ExternalId && Equals(existing.Url, reference.Url));
                if (exists) continue;
                var referenceId = targetRefs.Any(existing => existing.Id == reference.Id) ? Guid.NewGuid() : reference.Id;
                await ExecuteAsync("INSERT INTO main.ExternalReferences SELECT $targetId, SongId, Provider, ExternalId, Url FROM backupDb.ExternalReferences WHERE Id=$sourceId;",
                    transaction, cancellationToken, new SqliteParameter("$targetId", referenceId), new SqliteParameter("$sourceId", reference.Id)).ConfigureAwait(false);
                targetRefs.Add(new ExternalReference(targetId, reference.Provider, reference.ExternalId, reference.Url));
            }
            if (matcherSongId != targetId)
                targetRefs.AddRange(refs.Select(reference => new ExternalReference(matcherSongId, reference.Provider, reference.ExternalId, reference.Url)));
        }

        foreach (var sourceEvent in sourceEvents)
        {
            var targetSongId = songMap[sourceEvent.SongId];
            var song = sourceSongs.Single(item => item.Id == sourceEvent.SongId);
            var candidate = new ImportCandidate(song.Title, song.Artist, sourceEvent.Source)
            {
                FoundAt = sourceEvent.FoundAt,
                SourceItemId = sourceEvent.SourceItemId
            };
            var eventsForSong = targetEvents.Where(item => item.SongId == targetSongId).ToArray();
            var duplicate = eventDeduplicator.Evaluate(candidate, eventsForSong);
            if (duplicate.Status == FindEventDeduplicationStatus.Duplicate) continue;
            if (duplicate.Status == FindEventDeduplicationStatus.Indeterminate && targetEvents.Any(item => item.Id == sourceEvent.Id)) continue;
            var eventId = targetEvents.Any(item => item.Id == sourceEvent.Id) ? Guid.NewGuid() : sourceEvent.Id;
            await ExecuteAsync("INSERT INTO main.FindEvents SELECT $targetId, $targetSongId, FoundAt, Source, ImportedAt, SourceItemId FROM backupDb.FindEvents WHERE Id=$sourceId;",
                transaction, cancellationToken, new SqliteParameter("$targetId", eventId), new SqliteParameter("$targetSongId", targetSongId),
                new SqliteParameter("$sourceId", sourceEvent.Id)).ConfigureAwait(false);
            targetEvents.Add(new FindEvent(targetSongId, sourceEvent.Source, sourceEvent.ImportedAt, sourceEvent.FoundAt, sourceEvent.SourceItemId));
        }

        foreach (var tag in sourceTags)
        {
            if (await database.Tags.AnyAsync(existing => existing.NormalizedName == tag.NormalizedName, cancellationToken).ConfigureAwait(false)) continue;
            await ExecuteAsync("INSERT OR IGNORE INTO main.Tags SELECT Id, Name, NormalizedName FROM backupDb.Tags WHERE Id=$sourceId;",
                transaction, cancellationToken, new SqliteParameter("$sourceId", tag.Id)).ConfigureAwait(false);
        }
    }

    private static ImportCandidate ToCandidate(Song song, IEnumerable<ExternalReference> references) =>
        new(song.Title, song.Artist, FindSource.ImportedUnknown)
        {
            ExternalReferences = references.Select(reference => new ExternalReferenceCandidate(reference.Provider,
                reference.ExternalId ?? reference.Url?.AbsoluteUri ?? "", reference.Url)).Where(reference => reference.Value.Length > 0).ToArray()
        };

    private async Task ExecuteAsync(string sql, IDbContextTransaction transaction, CancellationToken cancellationToken,
        params SqliteParameter[] parameters)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.Transaction = (SqliteTransaction)transaction.GetDbTransaction();
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddFileAsync(ZipArchive archive, string path, string name, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var output = entry.Open();
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddBytesAsync(ZipArchive archive, string name, byte[] bytes, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static BackupOperationResult Fail(string message) => new(false, message);
    private static BackupInspectionResult Invalid(string message) => new(false, message, null);
    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { } }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (Exception) { } }
    private sealed record DatabaseCounts(string SchemaVersion, int Songs, int FindEvents, int ExternalReferences, int Tags);

}
