namespace SongStash.Desktop.ViewModels;

public sealed record ExportDestination(Stream Stream, string? Path = null);
