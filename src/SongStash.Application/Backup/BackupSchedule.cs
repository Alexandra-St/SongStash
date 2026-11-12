using SongStash.Application.Settings;

namespace SongStash.Application.Backup;

public static class BackupSchedule
{
    public static bool IsDue(BackupFrequency frequency, DateTimeOffset? lastSuccessfulBackupUtc, DateTimeOffset nowUtc)
    {
        if (frequency == BackupFrequency.Off) return false;
        if (lastSuccessfulBackupUtc is null) return true;
        var last = lastSuccessfulBackupUtc.Value.ToUniversalTime();
        var now = nowUtc.ToUniversalTime();
        return frequency switch
        {
            BackupFrequency.Weekly => now >= last.AddDays(7),
            BackupFrequency.Monthly => now >= last.AddMonths(1),
            _ => false
        };
    }
}
