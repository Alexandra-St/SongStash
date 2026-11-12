using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

/// <summary>Central user-facing strings for the Settings flow.</summary>
public sealed record SettingsStrings
{
    public required string NavigationSettings { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string AppearanceTab { get; init; }
    public required string GeneralTab { get; init; }
    public required string MusicServicesTab { get; init; }
    public required string BackupTab { get; init; }
    public required string ThemeTitle { get; init; }
    public required string ThemeDescription { get; init; }
    public required string Peachy { get; init; }
    public required string Sage { get; init; }
    public required string Midnight { get; init; }
    public required string Afterdark { get; init; }
    public required string PeachyDescription { get; init; }
    public required string SageDescription { get; init; }
    public required string MidnightDescription { get; init; }
    public required string AfterdarkDescription { get; init; }
    public required string SystemAppearance { get; init; }
    public required string SystemAppearanceDescription { get; init; }
    public required string FollowSystemTheme { get; init; }
    public required string LightTheme { get; init; }
    public required string DarkTheme { get; init; }
    public required string Preview { get; init; }
    public required string PreviewDescription { get; init; }
    public required string PreviewExample { get; init; }
    public required string PreviewLibrary { get; init; }
    public required string PreviewFavorites { get; init; }
    public required string PreviewSongTitle { get; init; }
    public required string PreviewArtist { get; init; }
    public required string PetTitle { get; init; }
    public required string PetDescription { get; init; }
    public required string PinkCat { get; init; }
    public required string Salem { get; init; }
    public required string Ghost { get; init; }
    public required string Bat { get; init; }
    public required string None { get; init; }
    public required string PetAnimation { get; init; }
    public required string ReduceAnimations { get; init; }
    public required string ReduceAnimationsDescription { get; init; }
    public required string Off { get; init; }
    public required string Subtle { get; init; }
    public required string Normal { get; init; }
    public required string GeneralTitle { get; init; }
    public required string Language { get; init; }
    public required string LanguageDescription { get; init; }
    public required string MoreLanguages { get; init; }
    public required string English { get; init; }
    public required string Russian { get; init; }
    public required string Startup { get; init; }
    public required string LaunchAtStartup { get; init; }
    public required string LaunchAtStartupDescription { get; init; }
    public required string Window { get; init; }
    public required string RememberWindow { get; init; }
    public required string RememberWindowDescription { get; init; }
    public required string StartPage { get; init; }
    public required string StartPageDescription { get; init; }
    public required string Overview { get; init; }
    public required string AllSongs { get; init; }
    public required string Notifications { get; init; }
    public required string NotificationsDescription { get; init; }
    public required string ImportComplete { get; init; }
    public required string ImportCompleteDescription { get; init; }
    public required string ImportIssues { get; init; }
    public required string ImportIssuesDescription { get; init; }
    public required string Confirmations { get; init; }
    public required string ConfirmDestructive { get; init; }
    public required string ConfirmDestructiveDescription { get; init; }
    public required string LocalSettingsNote { get; init; }
    public required string SettingsCorrupt { get; init; }
    public required string SaveFailed { get; init; }
    public required string PlaceholderServices { get; init; }
    public required string MusicServicesDescription { get; init; }
    public required string PreferredMusicService { get; init; }
    public required string ServiceOrder { get; init; }
    public required string MoveUp { get; init; }
    public required string MoveDown { get; init; }
    public required string Enabled { get; init; }
    public required string Preferred { get; init; }
    public required string SelectPreferredFirst { get; init; }
    public required string AtLeastOneMusicService { get; init; }
    public required string OpenMusicServiceFailed { get; init; }
    public required string MoreMusicServices { get; init; }
    public required string ManualBackupTitle { get; init; }
    public required string ManualBackupDescription { get; init; }
    public required string CreateBackup { get; init; }
    public required string AutomaticBackupTitle { get; init; }
    public required string BackupFrequency { get; init; }
    public required string Weekly { get; init; }
    public required string Monthly { get; init; }
    public required string BackupLocation { get; init; }
    public required string ChooseFolder { get; init; }
    public required string BackupFilesTitle { get; init; }
    public required string BackupFilesDescription { get; init; }
    public required string ChooseBackupFile { get; init; }
    public required string RestoreBackup { get; init; }
    public required string ShowInFolder { get; init; }
    public required string DeleteBackup { get; init; }
    public required string MergeBackup { get; init; }
    public required string ReplaceLibrary { get; init; }
    public required string RestoreApplicationSettings { get; init; }
    public required string ReplaceWarning { get; init; }
    public required string ConfirmReplace { get; init; }
    public required string ConfirmDeleteBackup { get; init; }
    public required string ConfirmDelete { get; init; }
    public required string Cancel { get; init; }
    public required string NoBackups { get; init; }
    public required string BackupCounts { get; init; }
    public required string AutomaticBackupDescription { get; init; }
    public required string SettingsIncluded { get; init; }
    public required string BackupCompatible { get; init; }
    public required string PlaceholderBackup { get; init; }
    public required string SelectedTheme { get; init; }
    public required string SelectedPet { get; init; }

    public static SettingsStrings For(AppLanguage language) => language == AppLanguage.Russian ? RussianStrings : EnglishStrings;

    private static readonly SettingsStrings EnglishStrings = new()
    {
        NavigationSettings = "Settings", Title = "Settings", Subtitle = "Customize the appearance and behavior of SongStash.",
        AppearanceTab = "Appearance", GeneralTab = "General", MusicServicesTab = "Music Services", BackupTab = "Backup",
        ThemeTitle = "Color Theme", ThemeDescription = "Choose a color theme for the app.",
        Peachy = "Peachy", Sage = "Sage", Midnight = "Midnight", Afterdark = "Afterdark",
        PeachyDescription = "Soft and warm", SageDescription = "Calm and natural", MidnightDescription = "Cool and modern", AfterdarkDescription = "Deep and cozy",
        SystemAppearance = "System Appearance", SystemAppearanceDescription = "Automatically switch themes based on your system settings.",
        FollowSystemTheme = "Follow system theme", LightTheme = "Light theme", DarkTheme = "Dark theme",
        Preview = "Preview", PreviewDescription = "This is how SongStash will look with your current settings.", PreviewExample = "Your music, together",
        PreviewLibrary = "Library", PreviewFavorites = "Favorites", PreviewSongTitle = "A sample song", PreviewArtist = "Sample artist",
        PetTitle = "Pet", PetDescription = "Choose a pet to keep you company while you explore your music.",
        PinkCat = "Pink Cat", Salem = "Salem", Ghost = "Ghost", Bat = "Bat", None = "None",
        PetAnimation = "Pet animation level", ReduceAnimations = "Reduce animations", ReduceAnimationsDescription = "Saved for the pet animation features when they become available.",
        Off = "Off", Subtle = "Subtle", Normal = "Normal", GeneralTitle = "General",
        Language = "Language", LanguageDescription = "Choose the language used throughout SongStash.",
        MoreLanguages = "More languages may be added in future versions.", English = "English", Russian = "Русский",
        Startup = "Startup", LaunchAtStartup = "Launch SongStash at startup", LaunchAtStartupDescription = "Preference saved locally; operating-system startup integration will be added later.",
        Window = "Window", RememberWindow = "Remember window size and position", RememberWindowDescription = "Restore SongStash where you left it the next time you open the app.",
        StartPage = "Default start page", StartPageDescription = "Choose which page SongStash opens to.", Overview = "Overview", AllSongs = "All Songs",
        Notifications = "Notifications", NotificationsDescription = "Choose which important events SongStash can notify you about.",
        ImportComplete = "Import completed", ImportCompleteDescription = "Saved locally; notification delivery will be added later.",
        ImportIssues = "Import issues", ImportIssuesDescription = "Saved locally; notification delivery will be added later.",
        Confirmations = "Confirmations", ConfirmDestructive = "Confirm destructive actions", ConfirmDestructiveDescription = "Ask before permanently deleting songs or clearing library data.",
        LocalSettingsNote = "General settings are stored locally on this device.",
        SettingsCorrupt = "Settings could not be read. Defaults are active; the original file was left unchanged.",
        SaveFailed = "Settings could not be saved. Check that the settings folder is writable.",
        PlaceholderServices = "Music service preferences will be available in a later update.",
        MusicServicesDescription = "Choose where songs open, and which services appear in the song details menu.",
        PreferredMusicService = "Preferred service", ServiceOrder = "Available services and order", MoveUp = "Move up", MoveDown = "Move down",
        Enabled = "Enabled", Preferred = "Preferred", SelectPreferredFirst = "Choose another preferred service before disabling this one.",
        AtLeastOneMusicService = "At least one music service must remain enabled.", OpenMusicServiceFailed = "Could not open the selected music service.", MoreMusicServices = "More…",
        ManualBackupTitle = "Create backup", ManualBackupDescription = "Save your complete SongStash library and application settings in a restorable archive.",
        CreateBackup = "Create Backup", AutomaticBackupTitle = "Automatic backups", BackupFrequency = "Frequency", Weekly = "Weekly", Monthly = "Monthly",
        BackupLocation = "Backup location", ChooseFolder = "Choose folder…", BackupFilesTitle = "Backup files", BackupFilesDescription = "Manage and restore local SongStash backups.",
        ChooseBackupFile = "Choose Backup File…", RestoreBackup = "Restore…", ShowInFolder = "Show in Folder", DeleteBackup = "Delete",
        MergeBackup = "Merge with current library", ReplaceLibrary = "Replace current library", RestoreApplicationSettings = "Also restore application settings",
        ReplaceWarning = "This will replace the current library. Confirm to continue.", ConfirmReplace = "Confirm Replace", ConfirmDeleteBackup = "Delete this backup file?",
        ConfirmDelete = "Confirm Delete", Cancel = "Cancel", NoBackups = "No valid backups found in this folder.", BackupCounts = "{0} songs · {1} find events",
        AutomaticBackupDescription = "A due backup is created once when SongStash starts.", SettingsIncluded = "Application settings included", BackupCompatible = "Compatible with this SongStash version",
        PlaceholderBackup = "Backup and restore options will be available in a later update.", SelectedTheme = "Selected", SelectedPet = "Selected"
    };

    private static readonly SettingsStrings RussianStrings = new()
    {
        NavigationSettings = "Настройки", Title = "Настройки", Subtitle = "Настройте внешний вид и поведение SongStash.",
        AppearanceTab = "Внешний вид", GeneralTab = "Общие", MusicServicesTab = "Музыкальные сервисы", BackupTab = "Резервное копирование",
        ThemeTitle = "Цветовая тема", ThemeDescription = "Выберите цветовую тему приложения.",
        Peachy = "Персиковая", Sage = "Шалфей", Midnight = "Полночь", Afterdark = "Сумерки",
        PeachyDescription = "Мягкая и тёплая", SageDescription = "Спокойная и природная", MidnightDescription = "Прохладная и современная", AfterdarkDescription = "Глубокая и уютная",
        SystemAppearance = "Системная тема", SystemAppearanceDescription = "Автоматически переключать тему в соответствии с настройками системы.",
        FollowSystemTheme = "Следовать системной теме", LightTheme = "Светлая тема", DarkTheme = "Тёмная тема",
        Preview = "Предпросмотр", PreviewDescription = "Так будет выглядеть SongStash с текущими настройками.", PreviewExample = "Вся музыка вместе",
        PreviewLibrary = "Библиотека", PreviewFavorites = "Избранное", PreviewSongTitle = "Пример песни", PreviewArtist = "Пример исполнителя",
        PetTitle = "Питомец", PetDescription = "Выберите питомца для музыкальных открытий.",
        PinkCat = "Розовый кот", Salem = "Салем", Ghost = "Призрак", Bat = "Летучая мышь", None = "Нет",
        PetAnimation = "Уровень анимации питомца", ReduceAnimations = "Уменьшить анимацию", ReduceAnimationsDescription = "Настройка сохранится и будет применена, когда появится анимация питомца.",
        Off = "Выкл.", Subtle = "Незаметная", Normal = "Обычная", GeneralTitle = "Общие",
        Language = "Язык", LanguageDescription = "Выберите язык SongStash.", MoreLanguages = "Другие языки могут появиться в будущих версиях.",
        English = "English", Russian = "Русский", Startup = "Запуск", LaunchAtStartup = "Запускать SongStash при входе в систему.",
        LaunchAtStartupDescription = "Настройка сохранена; интеграция с запуском системы появится позже.", Window = "Окно",
        RememberWindow = "Запоминать размер и положение окна", RememberWindowDescription = "При следующем запуске восстановить прежнее положение SongStash.",
        StartPage = "Начальная страница", StartPageDescription = "Выберите страницу, которая будет открываться при запуске SongStash.", Overview = "Обзор", AllSongs = "Все песни",
        Notifications = "Уведомления", NotificationsDescription = "Выберите события, о которых SongStash сможет сообщать.",
        ImportComplete = "Импорт завершён", ImportCompleteDescription = "Настройка сохранена; уведомления появятся позже.",
        ImportIssues = "Проблемы импорта", ImportIssuesDescription = "Настройка сохранена; уведомления появятся позже.",
        Confirmations = "Подтверждения", ConfirmDestructive = "Подтверждать опасные действия", ConfirmDestructiveDescription = "Спрашивать перед удалением песен или очисткой библиотеки.",
        LocalSettingsNote = "Общие настройки хранятся локально на этом устройстве.",
        SettingsCorrupt = "Не удалось прочитать настройки. Используются значения по умолчанию; исходный файл сохранён.",
        SaveFailed = "Не удалось сохранить настройки. Проверьте доступ к папке настроек.",
        PlaceholderServices = "Настройки музыкальных сервисов появятся в одном из будущих обновлений.",
        MusicServicesDescription = "Выберите, где открывать песни и какие сервисы показывать в карточке песни.",
        PreferredMusicService = "Предпочитаемый сервис", ServiceOrder = "Доступные сервисы и порядок", MoveUp = "Выше", MoveDown = "Ниже",
        Enabled = "Включён", Preferred = "Предпочитаемый", SelectPreferredFirst = "Сначала выберите другой предпочитаемый сервис.",
        AtLeastOneMusicService = "Должен оставаться включённым хотя бы один музыкальный сервис.", OpenMusicServiceFailed = "Не удалось открыть выбранный музыкальный сервис.", MoreMusicServices = "Ещё…",
        ManualBackupTitle = "Создать резервную копию", ManualBackupDescription = "Сохраните полную библиотеку SongStash и настройки в архиве для восстановления.",
        CreateBackup = "Создать копию", AutomaticBackupTitle = "Автоматические копии", BackupFrequency = "Периодичность", Weekly = "Еженедельно", Monthly = "Ежемесячно",
        BackupLocation = "Папка для копий", ChooseFolder = "Выбрать папку…", BackupFilesTitle = "Файлы резервных копий", BackupFilesDescription = "Управляйте локальными копиями SongStash и восстанавливайте их.",
        ChooseBackupFile = "Выбрать файл копии…", RestoreBackup = "Восстановить…", ShowInFolder = "Показать в папке", DeleteBackup = "Удалить",
        MergeBackup = "Объединить с текущей библиотекой", ReplaceLibrary = "Заменить текущую библиотеку", RestoreApplicationSettings = "Также восстановить настройки приложения",
        ReplaceWarning = "Текущая библиотека будет заменена. Подтвердите действие.", ConfirmReplace = "Подтвердить замену", ConfirmDeleteBackup = "Удалить этот файл резервной копии?",
        ConfirmDelete = "Подтвердить удаление", Cancel = "Отмена", NoBackups = "В этой папке нет действительных копий.", BackupCounts = "Песен: {0} · находок: {1}",
        AutomaticBackupDescription = "Просроченная копия создаётся один раз при запуске SongStash.", SettingsIncluded = "Настройки приложения включены", BackupCompatible = "Совместимо с этой версией SongStash",
        PlaceholderBackup = "Настройки резервного копирования появятся в одном из будущих обновлений.", SelectedTheme = "Выбрано", SelectedPet = "Выбран"
    };
}
