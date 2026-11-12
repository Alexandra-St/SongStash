using SongStash.Application.Exporting;
using SongStash.Application.Importing;
using SongStash.Application.Library;
using SongStash.Desktop.ViewModels;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class ExportViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ExportFormat.Csv, "SongStash_Export_2026-10-27.csv")]
    [InlineData(ExportFormat.Json, "SongStash_Export_2026-10-27.json")]
    public void DefaultFilenameContainsOnlyProductAndDate(ExportFormat format, string expected) =>
        Assert.Equal(expected, new ExportViewModel(new FakeExportService(), new FixedClock()).GetDefaultFileName(format));

    [Fact]
    public async Task CsvSelectionCallsCsvWriterAndShowsSuccess()
    {
        var service = new FakeExportService();
        var vm = new ExportViewModel(service, new FixedClock());
        string? suggested = null;
        await vm.ChooseAndExportAsync(ExportFormat.Csv, name =>
        {
            suggested = name;
            return Task.FromResult<ExportDestination?>(new ExportDestination(new MemoryStream()));
        });

        Assert.Equal("SongStash_Export_2026-10-27.csv", suggested);
        Assert.Equal(1, service.CsvCalls);
        Assert.Equal(0, service.JsonCalls);
        Assert.True(vm.HasMessage);
        Assert.Equal("CSV export completed successfully.", vm.Message);
    }

    [Fact]
    public async Task JsonSelectionCallsJsonWriter()
    {
        var service = new FakeExportService();
        var vm = new ExportViewModel(service, new FixedClock());
        await vm.ChooseAndExportAsync(ExportFormat.Json, _ => Task.FromResult<ExportDestination?>(new(new MemoryStream())));
        Assert.Equal(1, service.JsonCalls);
        Assert.Equal(0, service.CsvCalls);
        Assert.True(vm.HasMessage);
    }

    [Fact]
    public async Task PickerCancellationDoesNotCallExportOrShowError()
    {
        var service = new FakeExportService();
        var vm = new ExportViewModel(service, new FixedClock());
        vm.Message = "Old status";
        await vm.ChooseAndExportAsync(ExportFormat.Csv, _ => Task.FromResult<ExportDestination?>(null));
        Assert.Equal(0, service.CsvCalls);
        Assert.Null(vm.Message);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task ExportFailureShowsSafeMessageWithoutExceptionDetails()
    {
        var service = new FakeExportService { Failure = new IOException("private destination details") };
        var vm = new ExportViewModel(service, new FixedClock());
        await vm.ChooseAndExportAsync(ExportFormat.Json, _ => Task.FromResult<ExportDestination?>(new(new MemoryStream())));
        Assert.True(vm.HasError);
        Assert.DoesNotContain("private destination details", vm.ErrorMessage);
    }

    [Fact]
    public void ExportActionIsAvailableEvenForAnEmptyLibrary()
    {
        var export = new ExportViewModel(new FakeExportService(), new FixedClock());
        var library = new LibraryViewModel(new EmptyLibraryQuery(), export: export);
        Assert.True(library.CanExport);
        Assert.Equal(0, library.SongCount);
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class FakeExportService : ILibraryExportService
    {
        public int CsvCalls { get; private set; }
        public int JsonCalls { get; private set; }
        public Exception? Failure { get; init; }
        public async Task ExportJsonAsync(Stream destination, CancellationToken cancellationToken = default)
        {
            JsonCalls++;
            if (Failure is not null) throw Failure;
            await destination.WriteAsync("{}"u8.ToArray(), cancellationToken);
        }
        public async Task ExportCsvAsync(Stream destination, CancellationToken cancellationToken = default)
        {
            CsvCalls++;
            if (Failure is not null) throw Failure;
            await destination.WriteAsync("Title,Artist\n"u8.ToArray(), cancellationToken);
        }
    }
    private sealed class EmptyLibraryQuery : ILibraryQueryService
    {
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LibrarySongItem>>([]);
        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default) => Task.FromResult<SongDetails?>(null);
    }
}
