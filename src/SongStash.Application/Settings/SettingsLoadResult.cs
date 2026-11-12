namespace SongStash.Application.Settings;

public sealed record SettingsLoadResult(AppSettings Settings, string? DiagnosticMessage = null);
