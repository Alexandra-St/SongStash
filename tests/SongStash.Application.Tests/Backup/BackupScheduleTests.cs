using SongStash.Application.Backup;
using SongStash.Application.Settings;
using Xunit;

namespace SongStash.Application.Tests.Backup;

public sealed class BackupScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact] public void OffNeverCreatesAutomaticBackup() => Assert.False(BackupSchedule.IsDue(BackupFrequency.Off, null, Now));
    [Fact] public void WeeklyIsDueWhenNoSuccessfulBackupExists() => Assert.True(BackupSchedule.IsDue(BackupFrequency.Weekly, null, Now));
    [Fact] public void WeeklyIsNotDueBeforeSevenDays() => Assert.False(BackupSchedule.IsDue(BackupFrequency.Weekly, Now.AddDays(-6), Now));
    [Fact] public void WeeklyIsDueAtSevenDays() => Assert.True(BackupSchedule.IsDue(BackupFrequency.Weekly, Now.AddDays(-7), Now));
    [Fact] public void MonthlyIsDueByCalendarMonth() => Assert.True(BackupSchedule.IsDue(BackupFrequency.Monthly, Now.AddMonths(-1), Now));
    [Fact] public void MonthlyIsNotDueBeforeCalendarMonth() => Assert.False(BackupSchedule.IsDue(BackupFrequency.Monthly, Now.AddDays(-29), Now));
}
