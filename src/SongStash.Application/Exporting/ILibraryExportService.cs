namespace SongStash.Application.Exporting;

public interface ILibraryExportService
{
    Task ExportJsonAsync(Stream destination, CancellationToken cancellationToken = default);
    Task ExportCsvAsync(Stream destination, CancellationToken cancellationToken = default);
}
