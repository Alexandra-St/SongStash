namespace SongStash.Application.Backup;

using SongStash.Application.Settings;

public enum BackupRestoreMode
{
    Merge,
    Replace
}

public sealed record BackupManifest(
    string Marker,
    int FormatVersion,
    DateTimeOffset CreatedAtUtc,
    string LibrarySchemaVersion,
    int Songs,
    int FindEvents,
    int ExternalReferences,
    int Tags);

public sealed record BackupInfo(
    string Path,
    BackupManifest Manifest,
    long SizeBytes,
    bool ContainsSettings);

public sealed record BackupInspectionResult(
    bool IsValid,
    string? ErrorMessage,
    BackupInfo? Backup);

public sealed record BackupOperationResult(bool Success, string? ErrorMessage, BackupInfo? Backup = null, AppSettings? RestoredSettings = null);

public interface IBackupService
{
    Task<BackupOperationResult> CreateAsync(string folderPath, CancellationToken cancellationToken = default);
    Task<BackupInspectionResult> InspectAsync(string backupPath, CancellationToken cancellationToken = default);
    Task<BackupOperationResult> RestoreAsync(string backupPath, BackupRestoreMode mode, bool restoreSettings,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupInfo>> ListAsync(string folderPath, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string backupPath, CancellationToken cancellationToken = default);
}
