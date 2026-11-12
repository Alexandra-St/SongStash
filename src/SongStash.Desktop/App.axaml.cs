using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SongStash.Application.Library;
using SongStash.Application.LibraryActions;
using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Application.Persistence;
using SongStash.Application.Settings;
using SongStash.Application.MusicServices;
using SongStash.Application.Backup;
using SongStash.Desktop.ViewModels;
using SongStash.Desktop.Services;
using SongStash.Infrastructure.Importing.Shazam;
using SongStash.Infrastructure.Importing;
using SongStash.Infrastructure.Persistence;
using SongStash.Infrastructure.Persistence.Repositories;
using SongStash.Infrastructure.Settings;
using SongStash.Infrastructure.Backup;

namespace SongStash.Desktop;

public sealed class App : Avalonia.Application
{
    private ServiceProvider? services;
    private IServiceScope? windowScope;
    private SettingsViewModel? themeSettings;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataPaths = SongStashDataPaths.ResolveFromEnvironment();
            Directory.CreateDirectory(dataPaths.RootDirectory);
            var settingsStore = new JsonSettingsStore(dataPaths.SettingsPath);
            var settingsLoad = settingsStore.LoadAsync().GetAwaiter().GetResult();
            if (!File.Exists(dataPaths.SettingsPath))
                settingsStore.SaveAsync(settingsLoad.Settings).GetAwaiter().GetResult();
            var themeManager = new ThemeManager();
            themeManager.Apply(settingsLoad.Settings);
            var registrations = new ServiceCollection();
            registrations.AddDbContext<SongStashDbContext>(options => options.UseSqlite($"Data Source={dataPaths.DatabasePath}"));
            registrations.AddScoped<ISongRepository, SongRepository>();
            registrations.AddScoped<IFindEventRepository, FindEventRepository>();
            registrations.AddScoped<IExternalReferenceRepository, ExternalReferenceRepository>();
            registrations.AddScoped<ILibraryQueryService, LibraryQueryService>();
            registrations.AddScoped<IOverviewQueryService, OverviewQueryService>();
            registrations.AddSingleton<MusicServiceLinkResolver>();
            registrations.AddSingleton<IExternalUriLauncher, SystemExternalUriLauncher>();
            registrations.AddScoped<IBackupService, SongStashBackupService>();
            registrations.AddScoped<SongStash.Application.Exporting.ILibraryExportService, SongStash.Infrastructure.Exporting.LibraryExportService>();
            registrations.AddSingleton(dataPaths);
            registrations.AddScoped<IImportSourceReader, ShazamMacLocalReader>();
            registrations.AddSingleton<IFileImportSourceReaderFactory, FileImportSourceReaderFactory>();
            registrations.AddScoped<IClock, SystemClock>();
            registrations.AddScoped<SongMatcher>();
            registrations.AddScoped<ImportPreviewBuilder>();
            registrations.AddScoped<FindEventDeduplicator>();
            registrations.AddScoped<IImportItemCommitter, EfImportItemCommitter>();
            registrations.AddScoped<IManualEntryCommitter, EfManualEntryCommitter>();
            registrations.AddScoped<ManualSongService>();
            registrations.AddScoped<LibraryActionsService>();
            registrations.AddScoped<ImportService>();
            registrations.AddScoped<IImportOrchestrator, ImportOrchestrator>();
            registrations.AddScoped<LibraryViewModel>();
            registrations.AddScoped<ExportViewModel>();
            registrations.AddScoped<OverviewViewModel>();
            registrations.AddScoped<ImportViewModel>();
            registrations.AddScoped<SettingsViewModel>();
            registrations.AddScoped<BackupViewModel>();
            registrations.AddSingleton<ISettingsStore>(settingsStore);
            registrations.AddSingleton(settingsLoad);
            registrations.AddSingleton(themeManager);
            registrations.AddScoped<ShellViewModel>();
            services = registrations.BuildServiceProvider();
            OpenMainWindowOrRecovery(desktop);
            ActualThemeVariantChanged += OnActualThemeVariantChanged;
            desktop.Exit += (_, _) =>
            {
                ActualThemeVariantChanged -= OnActualThemeVariantChanged;
                themeSettings = null;
                windowScope?.Dispose();
                services?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) => themeSettings?.ApplySystemThemeChange();

    private void OpenMainWindowOrRecovery(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var previousWindow = desktop.MainWindow;
        themeSettings = null;
        windowScope?.Dispose();
        windowScope = services!.CreateScope();
        var database = windowScope.ServiceProvider.GetRequiredService<SongStashDbContext>();
        var ready = DatabaseStartupGuard.InitializeAsync(database).GetAwaiter().GetResult();
        if (!ready)
        {
            windowScope.Dispose();
            windowScope = null;
            desktop.MainWindow = new DatabaseRecoveryWindow(
                retry: () => OpenMainWindowOrRecovery(desktop),
                exit: () => desktop.Shutdown());
            return;
        }

        var shellViewModel = windowScope.ServiceProvider.GetRequiredService<ShellViewModel>();
        shellViewModel.Settings?.Backup?.RunAutomaticBackupIfDueAsync().GetAwaiter().GetResult();
        shellViewModel.Settings?.WaitForPendingSaveAsync().GetAwaiter().GetResult();
        themeSettings = shellViewModel.Settings;
        desktop.MainWindow = new MainWindow(shellViewModel);
        if (previousWindow is DatabaseRecoveryWindow)
            previousWindow.Close();
    }
}
