namespace SongStash.Application.Settings;

public sealed record AppSettings
{
    public SongStashTheme Theme { get; init; } = SongStashTheme.Peachy;
    public bool FollowSystemTheme { get; init; }
    public SongStashTheme LightTheme { get; init; } = SongStashTheme.Peachy;
    public SongStashTheme DarkTheme { get; init; } = SongStashTheme.Afterdark;
    public SongStashPet Pet { get; init; } = SongStashPet.PinkCat;
    public PetAnimationLevel PetAnimationLevel { get; init; } = PetAnimationLevel.Normal;
    public bool ReduceAnimations { get; init; }
    public AppLanguage Language { get; init; } = AppLanguage.English;
    public bool LaunchAtStartup { get; init; }
    public bool RememberWindowSizeAndPosition { get; init; } = true;
    public StartPage StartPage { get; init; } = StartPage.Overview;
    public bool NotifyImportComplete { get; init; } = true;
    public bool NotifyImportIssues { get; init; } = true;
    public bool ConfirmDestructiveActions { get; init; } = true;

    public string PreferredMusicService { get; init; } = MusicService.YouTubeMusic.ToString();
    public IReadOnlyList<string> MusicServiceOrder { get; init; } = MusicServiceDefaults.Order;
    public IReadOnlyList<string> EnabledMusicServices { get; init; } = MusicServiceDefaults.Order;
    public string? BackupLocation { get; init; }
    public BackupFrequency AutomaticBackupFrequency { get; init; } = BackupFrequency.Off;
    public DateTimeOffset? LastAutomaticBackupAtUtc { get; init; }

    public double? WindowWidth { get; init; }
    public double? WindowHeight { get; init; }
    public int? WindowLeft { get; init; }
    public int? WindowTop { get; init; }
}

public enum MusicService
{
    YouTubeMusic,
    YouTube,
    Spotify,
    AppleMusic
}

public enum BackupFrequency
{
    Off,
    Weekly,
    Monthly
}

public static class MusicServiceDefaults
{
    public static IReadOnlyList<string> Order { get; } =
        [nameof(MusicService.YouTubeMusic), nameof(MusicService.YouTube), nameof(MusicService.Spotify), nameof(MusicService.AppleMusic)];
}
