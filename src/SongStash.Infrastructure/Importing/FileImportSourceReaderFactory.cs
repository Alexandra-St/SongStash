using SongStash.Application.Importing;

namespace SongStash.Infrastructure.Importing;

public sealed class FileImportSourceReaderFactory : IFileImportSourceReaderFactory
{
    public IImportSourceReader Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csv" => new CsvImportSourceReader(path),
            ".json" => new JsonImportSourceReader(path),
            _ => throw new ArgumentException("Only CSV and JSON files are supported.", nameof(path))
        };
    }
}
