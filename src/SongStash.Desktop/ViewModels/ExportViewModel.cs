using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SongStash.Application.Exporting;
using SongStash.Application.Importing;

namespace SongStash.Desktop.ViewModels;

public sealed partial class ExportViewModel : ObservableObject
{
    private readonly ILibraryExportService service;
    private readonly IClock clock;
    private readonly SettingsViewModel? settings;
    private Func<LibraryStrings, string>? messageText;
    private Func<LibraryStrings, string>? errorText;

    public ExportViewModel(ILibraryExportService service, IClock clock, SettingsViewModel? settings = null)
    {
        this.service = service;
        this.clock = clock;
        this.settings = settings;
        if (settings is not null) settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    [ObservableProperty] private string? message;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    private LibraryStrings Strings => LibraryStrings.For(settings?.Language ?? SongStash.Application.Settings.AppLanguage.English);

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public string GetDefaultFileName(ExportFormat format) =>
        $"SongStash_Export_{clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.{Extension(format)}";

    public void BeginExport()
    {
        messageText = null;
        errorText = null;
        Message = null;
        ErrorMessage = null;
    }

    public async Task ChooseAndExportAsync(
        ExportFormat format,
        Func<string, Task<ExportDestination?>> chooseDestination,
        CancellationToken cancellationToken = default)
    {
        BeginExport();
        try
        {
            var destination = await chooseDestination(GetDefaultFileName(format));
            if (destination is null) return;
            await using (destination.Stream.ConfigureAwait(false))
            {
                IsBusy = true;
                if (format == ExportFormat.Csv)
                    await service.ExportCsvAsync(destination.Stream, cancellationToken).ConfigureAwait(false);
                else
                    await service.ExportJsonAsync(destination.Stream, cancellationToken).ConfigureAwait(false);
            }

            messageText = value => value.ExportSuccess(format == ExportFormat.Csv);
            Message = messageText(Strings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is a neutral outcome; the destination picker canceled or the caller stopped the operation.
        }
        catch (OperationCanceledException)
        {
            // Native picker cancellation is not an export failure.
        }
        catch (Exception)
        {
            errorText = value => value.ExportFailure(format == ExportFormat.Csv);
            ErrorMessage = errorText(Strings);
        }
        finally { IsBusy = false; }
    }

    public void ReportPickerFailure(ExportFormat format)
    {
        BeginExport();
        errorText = value => value.ExportFailure(format == ExportFormat.Csv);
        ErrorMessage = errorText(Strings);
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.Language)) return;
        if (messageText is not null) Message = messageText(Strings);
        if (errorText is not null) ErrorMessage = errorText(Strings);
    }

    private static string Label(ExportFormat format) => format == ExportFormat.Csv ? "CSV" : "JSON";
    private static string Extension(ExportFormat format) => format == ExportFormat.Csv ? "csv" : "json";
}
