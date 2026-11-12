using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Backup;
using SongStash.Application.Importing;
using SongStash.Application.Settings;
using SongStash.Desktop.Services;

namespace SongStash.Desktop.ViewModels;

public sealed partial class BackupFrequencyOption : ObservableObject
{
    public BackupFrequencyOption(BackupFrequency value, string label)
    {
        Value = value;
        this.label = label;
    }

    public BackupFrequency Value { get; }

    [ObservableProperty]
    private string label;
}

public sealed partial class BackupViewModel : ObservableObject
{
    private readonly IBackupService _service;
    private readonly IExternalUriLauncher _launcher;
    private readonly IClock _clock;
    private AppSettings _settings;
    private bool _confirmDestructive;
    private bool _isApplyingSettings;
    private readonly SongStashDataPaths? _dataPaths;

    public BackupViewModel(IBackupService service, SettingsLoadResult load, IExternalUriLauncher launcher, IClock clock,
        SongStashDataPaths? dataPaths = null)
    {
        _service = service; _launcher = launcher; _clock = clock; _settings = load.Settings; _dataPaths = dataPaths;
        _confirmDestructive = _settings.ConfirmDestructiveActions;
        backupFolder = string.IsNullOrWhiteSpace(_settings.BackupLocation) ? DefaultBackupLocation : _settings.BackupLocation;
        automaticFrequency = Enum.IsDefined(_settings.AutomaticBackupFrequency) ? _settings.AutomaticBackupFrequency : BackupFrequency.Off;
        FrequencyOptions.Add(new BackupFrequencyOption(BackupFrequency.Off, Strings.Off));
        FrequencyOptions.Add(new BackupFrequencyOption(BackupFrequency.Weekly, Strings.Weekly));
        FrequencyOptions.Add(new BackupFrequencyOption(BackupFrequency.Monthly, Strings.Monthly));
    }

    public static string DefaultBackupFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SongStash", "Backups");
    public string DefaultBackupLocation => _dataPaths?.BackupDirectory ?? DefaultBackupFolder;
    public ObservableCollection<BackupListRowViewModel> Backups { get; } = [];
    public event Action<AppSettings>? SettingsChanged;
    public event Action<AppSettings>? SettingsRestored;
    public event Action? LibraryRefreshRequested;
    public Func<Task>? WaitForSettingsSaveAsync { get; set; }
    [ObservableProperty] private string backupFolder;
    [ObservableProperty] private BackupFrequency automaticFrequency;
    [ObservableProperty] private bool restoreSettings;
    [ObservableProperty] private BackupInspectionResult? preview;
    [ObservableProperty] private BackupRestoreMode restoreMode = BackupRestoreMode.Merge;
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? pendingDeletePath;
    [ObservableProperty] private bool replaceConfirmationPending;

    public bool HasPreview => Preview?.IsValid == true;
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasPendingDelete => !string.IsNullOrWhiteSpace(PendingDeletePath);
    public bool HasBackups => Backups.Count > 0;
    public ObservableCollection<BackupFrequencyOption> FrequencyOptions { get; } = [];
    public SettingsStrings Strings => SettingsStrings.For(_settings.Language);
    public bool IsAutomaticBackupEnabled => AutomaticFrequency != BackupFrequency.Off;
    public string PreviewCounts => string.Format(Strings.BackupCounts, PreviewSongs, PreviewEvents);
    public bool IsMergeSelected
    {
        get => RestoreMode == BackupRestoreMode.Merge;
        set { if (value) RestoreMode = BackupRestoreMode.Merge; }
    }
    public bool IsReplaceSelected
    {
        get => RestoreMode == BackupRestoreMode.Replace;
        set { if (value) RestoreMode = BackupRestoreMode.Replace; }
    }
    public string PreviewDate => Preview?.Backup is { } backup ? UserDateTimeFormatter.Format(backup.Manifest.CreatedAtUtc) : "";
    public int PreviewSongs => Preview?.Backup?.Manifest.Songs ?? 0;
    public int PreviewEvents => Preview?.Backup?.Manifest.FindEvents ?? 0;
    public bool PreviewContainsSettings => Preview?.Backup?.ContainsSettings == true;
    public string PreviewCompatibility => Preview?.IsValid == true ? Strings.BackupCompatible : Preview?.ErrorMessage ?? "";

    public void SetSettingsSnapshot(AppSettings settings)
    {
        _settings = settings; _confirmDestructive = settings.ConfirmDestructiveActions;
        OnPropertyChanged(nameof(Strings));
        UpdateFrequencyLabels();
        OnPropertyChanged(nameof(PreviewCompatibility)); OnPropertyChanged(nameof(PreviewCounts));
    }

    public void ApplyRestoredSettings(AppSettings settings)
    {
        _settings = settings;
        _confirmDestructive = settings.ConfirmDestructiveActions;
        _isApplyingSettings = true;
        BackupFolder = string.IsNullOrWhiteSpace(settings.BackupLocation) ? DefaultBackupLocation : settings.BackupLocation;
        AutomaticFrequency = Enum.IsDefined(settings.AutomaticBackupFrequency) ? settings.AutomaticBackupFrequency : BackupFrequency.Off;
        _isApplyingSettings = false;
        OnPropertyChanged(nameof(IsAutomaticBackupEnabled));
        UpdateFrequencyLabels();
    }

    private void UpdateFrequencyLabels()
    {
        foreach (var option in FrequencyOptions)
        {
            option.Label = option.Value switch
            {
                BackupFrequency.Weekly => Strings.Weekly,
                BackupFrequency.Monthly => Strings.Monthly,
                _ => Strings.Off
            };
        }
    }
    partial void OnAutomaticFrequencyChanged(BackupFrequency value)
    {
        OnPropertyChanged(nameof(IsAutomaticBackupEnabled));
        if (!_isApplyingSettings && Enum.IsDefined(value)) _ = SaveSettingsAsync(_settings with { AutomaticBackupFrequency = value });
    }
    partial void OnBackupFolderChanged(string value) { if (!_isApplyingSettings && !string.IsNullOrWhiteSpace(value)) _ = SaveSettingsAsync(_settings with { BackupLocation = value }); }
    partial void OnPreviewChanged(BackupInspectionResult? value)
    {
        OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(PreviewDate)); OnPropertyChanged(nameof(PreviewSongs));
        OnPropertyChanged(nameof(PreviewEvents)); OnPropertyChanged(nameof(PreviewContainsSettings)); OnPropertyChanged(nameof(PreviewCompatibility)); OnPropertyChanged(nameof(PreviewCounts));
    }
    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnPendingDeletePathChanged(string? value) => OnPropertyChanged(nameof(HasPendingDelete));
    partial void OnRestoreModeChanged(BackupRestoreMode value)
    {
        OnPropertyChanged(nameof(IsMergeSelected)); OnPropertyChanged(nameof(IsReplaceSelected));
    }

    public async Task ChooseFolderAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        BackupFolder = Path.GetFullPath(path);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        try
        {
            var items = await _service.ListAsync(BackupFolder, cancellationToken);
            Backups.Clear();
            foreach (var item in items)
        Backups.Add(new BackupListRowViewModel(item, UserDateTimeFormatter.Format(item.Manifest.CreatedAtUtc), FormatSize(item.SizeBytes),
                    Strings.RestoreBackup, Strings.ShowInFolder, Strings.DeleteBackup,
                    item => _ = InspectAsync(item.Path), ShowInFolder, DeleteBackup));
            OnPropertyChanged(nameof(HasBackups));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { ErrorMessage = "Backup files could not be listed. Check that the folder is available."; }
    }

    [RelayCommand]
    public async Task CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true; ErrorMessage = null; Message = null;
        try
        {
            if (WaitForSettingsSaveAsync is { } wait) await wait().ConfigureAwait(false);
            var result = await _service.CreateAsync(BackupFolder, cancellationToken);
            if (!result.Success) ErrorMessage = result.ErrorMessage ?? "The backup could not be created.";
            else { Message = "Backup created and verified."; await LoadAsync(cancellationToken); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { ErrorMessage = "The backup could not be created."; }
        finally { IsBusy = false; }
    }

    public async Task InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        IsBusy = true; Preview = null; ErrorMessage = null; Message = null;
        try
        {
            Preview = await _service.InspectAsync(path, cancellationToken);
            if (!Preview.IsValid) ErrorMessage = Preview.ErrorMessage ?? "This backup cannot be restored.";
            else RestoreMode = BackupRestoreMode.Merge;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { ErrorMessage = "This backup cannot be read."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (Preview?.Backup is not { } info) return;
        if (RestoreMode == BackupRestoreMode.Replace) { ReplaceConfirmationPending = true; return; }
        await PerformRestoreAsync(info.Path, cancellationToken);
    }

    [RelayCommand]
    private async Task ConfirmReplaceAsync(CancellationToken cancellationToken = default)
    {
        if (Preview?.Backup is not { } info) return;
        ReplaceConfirmationPending = false;
        await PerformRestoreAsync(info.Path, cancellationToken);
    }

    [RelayCommand] private void CancelReplace() => ReplaceConfirmationPending = false;

    private async Task PerformRestoreAsync(string path, CancellationToken cancellationToken)
    {
        IsBusy = true; ErrorMessage = null; Message = null;
        try
        {
            var result = await _service.RestoreAsync(path, RestoreMode, RestoreSettings, cancellationToken);
            if (!result.Success) ErrorMessage = result.ErrorMessage ?? "The library could not be restored.";
            else
            {
                Message = result.ErrorMessage ?? (RestoreMode == BackupRestoreMode.Merge ? "Backup merged into the library." : "Library replaced from backup.");
                LibraryRefreshRequested?.Invoke();
                if (result.RestoredSettings is not null) { _settings = result.RestoredSettings; SettingsRestored?.Invoke(result.RestoredSettings); }
                await LoadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { ErrorMessage = "The library could not be restored. Your current library was kept unchanged."; }
        finally { IsBusy = false; }
    }

    private void ShowInFolder(BackupInfo info)
    {
        try { var folder = Path.GetDirectoryName(info.Path); if (!string.IsNullOrWhiteSpace(folder)) _launcher.Open(new Uri(folder + Path.DirectorySeparatorChar)); }
        catch (Exception) { ErrorMessage = "The backup folder could not be opened."; }
    }

    private void DeleteBackup(BackupInfo info)
    {
        if (_confirmDestructive) PendingDeletePath = info.Path;
        else _ = DeleteAsync(info.Path);
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
    {
        var path = PendingDeletePath; PendingDeletePath = null;
        if (path is not null) await DeleteAsync(path, cancellationToken);
    }
    [RelayCommand] private void CancelDelete() => PendingDeletePath = null;

    private async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!await _service.DeleteAsync(path, cancellationToken)) ErrorMessage = "The selected backup could not be deleted.";
        else { Message = "Backup deleted."; await LoadAsync(cancellationToken); }
    }

    public async Task RunAutomaticBackupIfDueAsync(CancellationToken cancellationToken = default)
    {
        if (!BackupSchedule.IsDue(AutomaticFrequency, _settings.LastAutomaticBackupAtUtc, _clock.UtcNow)) return;
        if (WaitForSettingsSaveAsync is { } wait) await wait().ConfigureAwait(false);
        var result = await _service.CreateAsync(BackupFolder, cancellationToken);
        if (!result.Success) { ErrorMessage = result.ErrorMessage ?? "Automatic backup was skipped."; return; }
        await SaveSettingsAsync(_settings with { LastAutomaticBackupAtUtc = _clock.UtcNow.ToUniversalTime() });
        await LoadAsync(cancellationToken);
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        _settings = settings;
        SettingsChanged?.Invoke(settings);
        await Task.CompletedTask;
    }

    private static string FormatSize(long size) => size switch
    {
        < 1024 => $"{size} B", < 1024 * 1024 => $"{size / 1024d:0.#} KB", _ => $"{size / (1024d * 1024):0.#} MB"
    };
}
