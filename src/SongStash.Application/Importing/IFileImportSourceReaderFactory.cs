namespace SongStash.Application.Importing;

/// <summary>Creates a metadata reader for a supported local import file.</summary>
public interface IFileImportSourceReaderFactory
{
    IImportSourceReader Create(string path);
}
