using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using SongStash.Application.Settings;
using SongStash.Desktop.Services;
using SongStash.Desktop.ViewModels;

namespace SongStash.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Bitmap> _petBitmapCache = [];

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public MainWindow(ShellViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        UpdatePetVisual(viewModel.Settings?.Pet ?? SongStashPet.None, viewModel.Settings?.ReduceAnimations ?? false);
        if (viewModel.Settings is { } settings)
            settings.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(SettingsViewModel.Pet) or nameof(SettingsViewModel.ReduceAnimations))
                    UpdatePetVisual(settings.Pet, settings.ReduceAnimations);
            };
        Opened += async (_, _) =>
        {
            RestoreWindowBounds(viewModel.Settings?.CurrentSettings);
            if (viewModel.IsOverviewPage && viewModel.Overview is not null)
                await viewModel.Overview.LoadAsync();
            else
                await viewModel.Library.LoadAsync();
        };
        Closed += async (_, _) =>
        {
            var position = GetPositionIfFullyVisible();
            if (viewModel.Settings is not null)
            {
                await viewModel.Settings.SaveWindowBoundsAsync(Width, Height, position?.X, position?.Y);
                await viewModel.Settings.WaitForPendingSaveAsync();
            }
            viewModel.Dispose();
        };
    }

    private void UpdatePetVisual(SongStashPet pet, bool reduceAnimations)
    {
        var assets = PetVisualAssetCatalog.Resolve(pet);
        var isVisible = assets is not null;
        this.FindControl<Grid>("SidebarPetArtwork")!.IsVisible = isVisible;
        this.FindControl<Grid>("PetPlacement")!.IsVisible = isVisible;
        var salemDownLeft = this.FindControl<Image>("SalemDownLeftBackground")!;
        var salemDownRight = this.FindControl<Image>("SalemDownRightBackground")!;
        var salemTop = this.FindControl<Image>("SalemTopBackground")!;
        var ghostDown = this.FindControl<Image>("GhostDownBackground")!;
        var ghostTop = this.FindControl<Image>("GhostTopBackground")!;
        var batDown = this.FindControl<Image>("BatDownBackground")!;
        var batTop = this.FindControl<Image>("BatTopBackground")!;
        salemDownLeft.IsVisible = false;
        salemDownRight.IsVisible = false;
        salemTop.IsVisible = false;
        ghostDown.IsVisible = false;
        ghostTop.IsVisible = false;
        batDown.IsVisible = false;
        batTop.IsVisible = false;
        if (assets is null) return;

        SetPetImage("PetBasePreview", assets.BasePreview);
        var downBackground = this.FindControl<Image>("PetDownBackground")!;
        var topBackground = this.FindControl<Image>("PetTopBackground")!;
        var triptychArtwork = this.FindControl<Grid>("PetTriptychArtwork")!;

        if (pet == SongStashPet.Salem)
        {
            downBackground.IsVisible = false;
            topBackground.IsVisible = false;
            triptychArtwork.IsVisible = false;
            SetPetImage("SalemDownLeftBackground", "avares://SongStash.Desktop/Assets/Pets/Salem/Salem_DownLeft.png");
            SetPetImage("SalemDownRightBackground", "avares://SongStash.Desktop/Assets/Pets/Salem/Salem_DownRight.png");
            SetPetImage("SalemTopBackground", "avares://SongStash.Desktop/Assets/Pets/Salem/Salem_Top.png");
            salemDownLeft.IsVisible = true;
            salemDownRight.IsVisible = true;
            salemTop.IsVisible = true;
            return;
        }

        if (pet == SongStashPet.Ghost)
        {
            downBackground.IsVisible = false;
            topBackground.IsVisible = false;
            triptychArtwork.IsVisible = false;
            SetPetImage("GhostTopBackground", "avares://SongStash.Desktop/Assets/Pets/Ghost/Ghost_Top.png");
            SetPetImage("GhostDownBackground", "avares://SongStash.Desktop/Assets/Pets/Ghost/Ghost_Down.png");
            ghostDown.IsVisible = true;
            ghostTop.IsVisible = true;
            return;
        }

        if (pet == SongStashPet.Bat)
        {
            downBackground.IsVisible = false;
            topBackground.IsVisible = false;
            triptychArtwork.IsVisible = false;
            SetPetImage("BatDownBackground", "avares://SongStash.Desktop/Assets/Pets/Bat/Bat_Down.png");
            SetPetImage("BatTopBackground", "avares://SongStash.Desktop/Assets/Pets/Bat/Bat_Top.png");
            batDown.IsVisible = true;
            batTop.IsVisible = true;
            return;
        }

        var usesDownBackground = assets.Down is not null;
        downBackground.IsVisible = usesDownBackground;
        topBackground.IsVisible = assets.TopBackground is not null;
        triptychArtwork.IsVisible = !usesDownBackground;
        if (usesDownBackground)
        {
            if (assets.Down is not null)
                SetPetImage("PetDownBackground", assets.Down);
            if (assets.TopBackground is not null)
                SetPetImage("PetTopBackground", assets.TopBackground);
        }
        else
        {
            SetPetImage("PetTriptychBottom", assets.Bottom);
            SetPetImage("PetTriptychMiddle", assets.Middle);
            SetPetImage("PetTriptychTop", assets.Top);
        }
    }

    private void SetPetImage(string controlName, string assetUri)
    {
        var image = this.FindControl<Image>(controlName)!;
        if (!_petBitmapCache.TryGetValue(assetUri, out var bitmap))
        {
            using var stream = AssetLoader.Open(new Uri(assetUri));
            bitmap = new Bitmap(stream);
            _petBitmapCache.Add(assetUri, bitmap);
        }
        image.Source = bitmap;
    }


    private void EmptyImport_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell) shell.OpenImportCommand.Execute(null);
    }

    private async void ExportCsv_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ChooseExportAsync(ExportFormat.Csv);

    private async void ExportJson_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ChooseExportAsync(ExportFormat.Json);

    private async Task ChooseExportAsync(ExportFormat format)
    {
        if (DataContext is not ShellViewModel { Export: { } export } || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;
        export.BeginExport();
        try
        {
            var isCsv = format == ExportFormat.Csv;
            var extension = isCsv ? "csv" : "json";
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = isCsv ? "Export CSV" : "Export JSON",
                SuggestedFileName = export.GetDefaultFileName(format),
                DefaultExtension = extension,
                FileTypeChoices =
                [
                    new FilePickerFileType(isCsv ? "CSV file" : "JSON file")
                    {
                        Patterns = [$"*.{extension}"],
                        MimeTypes = [isCsv ? "text/csv" : "application/json"]
                    }
                ]
            });
            if (file is null) return;
            await export.ChooseAndExportAsync(format, _ => OpenDestinationAsync(file));
        }
        catch (OperationCanceledException)
        {
            // Closing the native picker is a normal cancellation.
        }
        catch (Exception)
        {
            export.ReportPickerFailure(format);
        }
    }

    private static async Task<ExportDestination?> OpenDestinationAsync(IStorageFile file)
    {
        var stream = await file.OpenWriteAsync();
        return new ExportDestination(stream, file.TryGetLocalPath());
    }

    private void RestoreWindowBounds(SongStash.Application.Settings.AppSettings? settings)
    {
        if (settings is not { RememberWindowSizeAndPosition: true } || settings.WindowWidth is not > 0 || settings.WindowHeight is not > 0)
            return;

        var width = settings.WindowWidth.Value;
        var height = settings.WindowHeight.Value;
        if (width < MinWidth || height < MinHeight || width > 5000 || height > 5000 ||
            !Screens.All.Any(screen => FitsScreen(width, height, screen)))
            return;

        Width = width;
        Height = height;
        if (settings.WindowLeft is not int left || settings.WindowTop is not int top)
            return;
        var screen = Screens.ScreenFromPoint(new PixelPoint(left, top));
        if (screen is not null && IsInsideWorkArea(left, top, width, height, screen))
            Position = new PixelPoint(left, top);
    }

    private PixelPoint? GetPositionIfFullyVisible()
    {
        var position = Position;
        var screen = Screens.ScreenFromPoint(position);
        return screen is not null && IsInsideWorkArea(position.X, position.Y, Width, Height, screen)
            ? position
            : null;
    }

    private static bool FitsScreen(double width, double height, Screen screen) =>
        width * screen.Scaling <= screen.WorkingArea.Width && height * screen.Scaling <= screen.WorkingArea.Height;

    private static bool IsInsideWorkArea(int left, int top, double width, double height, Screen screen)
    {
        var area = screen.WorkingArea;
        var pixelWidth = (int)Math.Ceiling(width * screen.Scaling);
        var pixelHeight = (int)Math.Ceiling(height * screen.Scaling);
        return left >= area.X && top >= area.Y &&
               left + pixelWidth <= area.Right && top + pixelHeight <= area.Bottom;
    }
}
