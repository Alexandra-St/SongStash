using SongStash.Application.Settings;
using SongStash.Infrastructure.Settings;
using Xunit;

namespace SongStash.Infrastructure.Tests.Settings;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task MissingFileReturnsDocumentedDefaults()
    {
        using var file = new SettingsFile();
        var loaded = await new JsonSettingsStore(file.Path).LoadAsync();
        Assert.Null(loaded.DiagnosticMessage);
        Assert.Equal(SongStashTheme.Peachy, loaded.Settings.Theme);
        Assert.Equal(SongStashPet.PinkCat, loaded.Settings.Pet);
        Assert.Equal(AppLanguage.English, loaded.Settings.Language);
        Assert.Equal(StartPage.Overview, loaded.Settings.StartPage);
        Assert.True(loaded.Settings.RememberWindowSizeAndPosition);
        Assert.False(loaded.Settings.LaunchAtStartup);
        Assert.False(loaded.Settings.ReduceAnimations);
        Assert.Equal("YouTubeMusic", loaded.Settings.PreferredMusicService);
        Assert.Equal(new[] { "YouTubeMusic", "YouTube", "Spotify", "AppleMusic" }, loaded.Settings.MusicServiceOrder);
        Assert.Equal(loaded.Settings.MusicServiceOrder, loaded.Settings.EnabledMusicServices);
        Assert.Equal(PetAnimationLevel.Normal, loaded.Settings.PetAnimationLevel);
        Assert.True(loaded.Settings.ConfirmDestructiveActions);
    }

    [Fact]
    public async Task SaveAndReloadRoundTripsAllCurrentPreferencesAndFutureMusicFields()
    {
        using var file = new SettingsFile();
        var settings = new AppSettings
        {
            Theme = SongStashTheme.Sage, FollowSystemTheme = true, LightTheme = SongStashTheme.Sage,
            DarkTheme = SongStashTheme.Midnight, Pet = SongStashPet.Ghost,
            PetAnimationLevel = PetAnimationLevel.Subtle, ReduceAnimations = true,
            Language = AppLanguage.Russian, LaunchAtStartup = true,
            RememberWindowSizeAndPosition = false, StartPage = StartPage.Library,
            NotifyImportComplete = false, NotifyImportIssues = false, ConfirmDestructiveActions = false,
            PreferredMusicService = "AppleMusic", MusicServiceOrder = ["AppleMusic", "Spotify"],
            EnabledMusicServices = ["AppleMusic", "Spotify"],
            BackupLocation = "/tmp/songstash-backups", AutomaticBackupFrequency = BackupFrequency.Monthly,
            LastAutomaticBackupAtUtc = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero),
            WindowWidth = 1280, WindowHeight = 800, WindowLeft = 120, WindowTop = 70
        };
        var store = new JsonSettingsStore(file.Path);
        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();
        Assert.Null(loaded.DiagnosticMessage);
        Assert.Equal(settings.Theme, loaded.Settings.Theme);
        Assert.Equal(settings.FollowSystemTheme, loaded.Settings.FollowSystemTheme);
        Assert.Equal(settings.LightTheme, loaded.Settings.LightTheme);
        Assert.Equal(settings.DarkTheme, loaded.Settings.DarkTheme);
        Assert.Equal(settings.Pet, loaded.Settings.Pet);
        Assert.Equal(settings.PetAnimationLevel, loaded.Settings.PetAnimationLevel);
        Assert.Equal(settings.ReduceAnimations, loaded.Settings.ReduceAnimations);
        Assert.Equal(settings.Language, loaded.Settings.Language);
        Assert.Equal(settings.StartPage, loaded.Settings.StartPage);
        Assert.Equal(settings.WindowWidth, loaded.Settings.WindowWidth);
        Assert.Equal(settings.WindowHeight, loaded.Settings.WindowHeight);
        Assert.Equal(settings.WindowLeft, loaded.Settings.WindowLeft);
        Assert.Equal(settings.WindowTop, loaded.Settings.WindowTop);
        Assert.Equal(settings.NotifyImportComplete, loaded.Settings.NotifyImportComplete);
        Assert.Equal(settings.NotifyImportIssues, loaded.Settings.NotifyImportIssues);
        Assert.Equal(settings.ConfirmDestructiveActions, loaded.Settings.ConfirmDestructiveActions);
        Assert.Equal(settings.PreferredMusicService, loaded.Settings.PreferredMusicService);
        Assert.Equal(settings.MusicServiceOrder, loaded.Settings.MusicServiceOrder);
        Assert.Equal(settings.EnabledMusicServices, loaded.Settings.EnabledMusicServices);
        Assert.Equal(settings.BackupLocation, loaded.Settings.BackupLocation);
        Assert.Equal(settings.AutomaticBackupFrequency, loaded.Settings.AutomaticBackupFrequency);
        Assert.Equal(settings.LastAutomaticBackupAtUtc, loaded.Settings.LastAutomaticBackupAtUtc);
    }

    [Fact]
    public async Task PetPreferencePersistsIndependentlyOfTheme()
    {
        using var file = new SettingsFile();
        var store = new JsonSettingsStore(file.Path);
        await store.SaveAsync(new AppSettings { Theme = SongStashTheme.Afterdark, Pet = SongStashPet.Bat });
        var loaded = await store.LoadAsync();
        Assert.Equal(SongStashTheme.Afterdark, loaded.Settings.Theme);
        Assert.Equal(SongStashPet.Bat, loaded.Settings.Pet);
    }

    [Fact]
    public async Task CorruptJsonReturnsDefaultsAndLeavesOriginalForDiagnostics()
    {
        using var file = new SettingsFile();
        await File.WriteAllTextAsync(file.Path, "{broken");
        var loaded = await new JsonSettingsStore(file.Path).LoadAsync();
        Assert.NotNull(loaded.DiagnosticMessage);
        Assert.Equal(SongStashTheme.Peachy, loaded.Settings.Theme);
        Assert.Equal("{broken", await File.ReadAllTextAsync(file.Path));
        Assert.DoesNotContain(file.Path, loaded.DiagnosticMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownFuturePropertiesAreIgnored()
    {
        using var file = new SettingsFile();
        await File.WriteAllTextAsync(file.Path, "{\"theme\":\"Sage\",\"futurePreference\":{\"nested\":true}}");
        var loaded = await new JsonSettingsStore(file.Path).LoadAsync();
        Assert.Null(loaded.DiagnosticMessage);
        Assert.Equal(SongStashTheme.Sage, loaded.Settings.Theme);
    }

    [Fact]
    public async Task SettingsWithoutMusicServiceFieldsLoadMusicDefaults()
    {
        using var file = new SettingsFile();
        await File.WriteAllTextAsync(file.Path, "{\"theme\":\"Sage\",\"language\":\"Russian\"}");
        var loaded = await new JsonSettingsStore(file.Path).LoadAsync();
        Assert.Null(loaded.DiagnosticMessage);
        Assert.Equal(SongStashTheme.Sage, loaded.Settings.Theme);
        Assert.Equal("YouTubeMusic", loaded.Settings.PreferredMusicService);
        Assert.Equal(new[] { "YouTubeMusic", "YouTube", "Spotify", "AppleMusic" }, loaded.Settings.EnabledMusicServices);
    }

    [Fact]
    public async Task AtomicSaveLeavesNoTemporaryFiles()
    {
        using var file = new SettingsFile();
        var store = new JsonSettingsStore(file.Path);
        await store.SaveAsync(new AppSettings { Theme = SongStashTheme.Midnight });
        Assert.Empty(Directory.GetFiles(file.DirectoryPath, "*.tmp"));
    }

    private sealed class SettingsFile : IDisposable
    {
        public string DirectoryPath { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"songstash-settings-{Guid.NewGuid():N}");
        public string Path => System.IO.Path.Combine(DirectoryPath, "settings.json");
        public SettingsFile() => Directory.CreateDirectory(DirectoryPath);
        public void Dispose() { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true); }
    }
}
