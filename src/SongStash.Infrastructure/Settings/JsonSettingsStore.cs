using System.Text.Json;
using System.Text.Json.Serialization;
using SongStash.Application.Settings;

namespace SongStash.Infrastructure.Settings;

/// <summary>Stores per-device preferences in a separate atomic JSON file.</summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private const string SafeLoadDiagnostic = "Settings could not be read. Defaults are active; the settings file was left unchanged.";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return new SettingsLoadResult(new AppSettings());
        try
        {
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return settings is null
                ? new SettingsLoadResult(new AppSettings(), SafeLoadDiagnostic)
                : new SettingsLoadResult(settings);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new SettingsLoadResult(new AppSettings(), SafeLoadDiagnostic);
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
