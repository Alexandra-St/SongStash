namespace SongStash.Desktop.Services;

/// <summary>Resolves SongStash-owned runtime paths without changing external source paths.</summary>
public sealed record SongStashDataPaths(string RootDirectory)
{
    public const string OverrideEnvironmentVariable = "SONGSTASH_DATA_DIR";

    public string DatabasePath => Path.Combine(RootDirectory, "songstash.db");
    public string SettingsPath => Path.Combine(RootDirectory, "settings.json");
    public string BackupDirectory => Path.Combine(RootDirectory, "Backups");

    public static SongStashDataPaths Resolve(string? overrideDirectory, string localApplicationData)
    {
        var root = string.IsNullOrWhiteSpace(overrideDirectory)
            ? Path.Combine(localApplicationData, "SongStash")
            : overrideDirectory.Trim();
        return new SongStashDataPaths(Path.GetFullPath(root));
    }

    public static SongStashDataPaths ResolveFromEnvironment() => Resolve(
        Environment.GetEnvironmentVariable(OverrideEnvironmentVariable),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
}
