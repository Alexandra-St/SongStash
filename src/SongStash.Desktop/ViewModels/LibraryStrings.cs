using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed record LibraryStrings
{
    public required string NavigationLibrary { get; init; }
    public required string NavigationOverview { get; init; }
    public required string NavigationImport { get; init; }
    public required string Title { get; init; }
    public required string SongCountFormat { get; init; }
    public required string AddManually { get; init; }
    public required string SearchPlaceholder { get; init; }
    public required string RecentlyFound { get; init; }
    public required string OldestFound { get; init; }
    public required string TitleAscending { get; init; }
    public required string TitleDescending { get; init; }
    public required string ArtistAscending { get; init; }
    public required string ArtistDescending { get; init; }
    public required string Export { get; init; }
    public required string ExportCsv { get; init; }
    public required string ExportJson { get; init; }
    public required string ArtColumn { get; init; }
    public required string TitleColumn { get; init; }
    public required string ArtistColumn { get; init; }
    public required string FindsColumn { get; init; }
    public required string EmptyTitle { get; init; }
    public required string EmptyDescription { get; init; }
    public required string Import { get; init; }
    public required string NoSearchResults { get; init; }
    public required string DetailsDeleteSong { get; init; }
    public required string Delete { get; init; }
    public required string Cancel { get; init; }
    public required string DetailsHeading { get; init; }
    public required string GenreFormat { get; init; }
    public required string Favorite { get; init; }
    public required string AddToFavorites { get; init; }
    public required string RemoveFromFavorites { get; init; }
    public required string LinkedReferences { get; init; }
    public required string NoLinkedReferences { get; init; }
    public required string FindHistory { get; init; }
    public required string FindCountFormat { get; init; }
    public required string DeleteFind { get; init; }
    public required string ChooseSong { get; init; }
    public required string DetailsEmptyDescription { get; init; }
    public required string NotFoundYet { get; init; }
    public required string FoundOnce { get; init; }
    public required string FoundTimesFormat { get; init; }
    public required string SourceShazamApple { get; init; }
    public required string SourceShazamAndroid { get; init; }
    public required string SourceManual { get; init; }
    public required string SourceImported { get; init; }
    public required string DateUnknown { get; init; }
    public required string ImportedAtFormat { get; init; }
    public required string DeleteSongConfirmation { get; init; }
    public required string DeleteFindConfirmation { get; init; }
    public required string LoadFailed { get; init; }
    public required string FavoriteUpdateFailed { get; init; }
    public required string SongDeleteFailed { get; init; }
    public required string FindDeleteFailed { get; init; }
    public required string DetailsLoadFailed { get; init; }
    public required string OpenServiceFailed { get; init; }
    public required string OpenServiceFormat { get; init; }
    public required string CsvExportSuccessFormat { get; init; }
    public required string JsonExportSuccessFormat { get; init; }
    public required string CsvExportFailure { get; init; }
    public required string JsonExportFailure { get; init; }

    public string SongCount(int count)
    {
        if (SongCountFormat.Contains("{1}", StringComparison.Ordinal))
        {
            var value = Math.Abs(count) % 100;
            var lastDigit = value % 10;
            var noun = value is >= 11 and <= 14 ? "песен" : lastDigit switch
            {
                1 => "песня",
                >= 2 and <= 4 => "песни",
                _ => "песен"
            };
            return string.Format(SongCountFormat, count, noun);
        }
        return string.Format(SongCountFormat, count);
    }
    public string FindCount(int count) => string.Format(FindCountFormat, count);
    public string FoundTimes(int count) => string.Format(FoundTimesFormat, count);
    public string OpenService(string service) => string.Format(OpenServiceFormat, service);
    public string ExportSuccess(bool csv) => csv ? CsvExportSuccessFormat : JsonExportSuccessFormat;
    public string ExportFailure(bool csv) => csv ? CsvExportFailure : JsonExportFailure;

    public static LibraryStrings For(AppLanguage language) => language == AppLanguage.Russian ? Russian : English;

    private static readonly LibraryStrings English = new()
    {
        NavigationLibrary = "Library", NavigationOverview = "Overview", NavigationImport = "Import",
        Title = "Library", SongCountFormat = "{0} songs", AddManually = "Add Manually", SearchPlaceholder = "Search title or artist…",
        RecentlyFound = "Recently found", OldestFound = "Oldest found", TitleAscending = "Title A–Z", TitleDescending = "Title Z–A",
        ArtistAscending = "Artist A–Z", ArtistDescending = "Artist Z–A", Export = "Export", ExportCsv = "Export CSV", ExportJson = "Export JSON",
        ArtColumn = "ART", TitleColumn = "TITLE", ArtistColumn = "ARTIST", FindsColumn = "FINDS",
        EmptyTitle = "Your library is ready", EmptyDescription = "Import your Shazam history to bring your songs and every recognition into SongStash.",
        Import = "Import", NoSearchResults = "No songs match your search.", DetailsDeleteSong = "Delete Song", Delete = "Delete", Cancel = "Cancel",
        DetailsHeading = "DETAILS", GenreFormat = "Genre  ·  {0}", Favorite = "Favorite", AddToFavorites = "Add to favorites",
        RemoveFromFavorites = "Remove from favorites", LinkedReferences = "LINKED REFERENCES", NoLinkedReferences = "No linked references",
        FindHistory = "FIND HISTORY", FindCountFormat = "{0} finds", DeleteFind = "Delete find", ChooseSong = "Choose a song",
        DetailsEmptyDescription = "Details and find history will appear here.", NotFoundYet = "Not found yet", FoundOnce = "Found once",
        FoundTimesFormat = "Found {0} times", SourceShazamApple = "Shazam · Apple", SourceShazamAndroid = "Shazam · Android",
        SourceManual = "Manual", SourceImported = "Imported", DateUnknown = "Date unknown", ImportedAtFormat = "Imported {0}",
        DeleteSongConfirmation = "Delete this song and its find history?", DeleteFindConfirmation = "Delete this find? The song will remain in your library.",
        LoadFailed = "The library could not be loaded. Please try again.", FavoriteUpdateFailed = "Favorite could not be updated.",
        SongDeleteFailed = "Song could not be deleted.", FindDeleteFailed = "Find could not be deleted.", DetailsLoadFailed = "Song details could not be loaded.",
        OpenServiceFailed = "Could not open the selected music service.", OpenServiceFormat = "Open in {0}",
        CsvExportSuccessFormat = "CSV export completed successfully.", JsonExportSuccessFormat = "JSON export completed successfully.",
        CsvExportFailure = "CSV export failed. Check the destination and try again.", JsonExportFailure = "JSON export failed. Check the destination and try again."
    };

    private static readonly LibraryStrings Russian = new()
    {
        NavigationLibrary = "Библиотека", NavigationOverview = "Обзор", NavigationImport = "Импорт",
        Title = "Библиотека", SongCountFormat = "{0} {1}", AddManually = "Добавить вручную", SearchPlaceholder = "Поиск по названию или исполнителю…",
        RecentlyFound = "Сначала новые", OldestFound = "Сначала старые", TitleAscending = "Название А–Я", TitleDescending = "Название Я–А",
        ArtistAscending = "Исполнитель А–Я", ArtistDescending = "Исполнитель Я–А", Export = "Экспорт", ExportCsv = "Экспорт CSV", ExportJson = "Экспорт JSON",
        ArtColumn = "ОБЛОЖКА", TitleColumn = "НАЗВАНИЕ", ArtistColumn = "ИСПОЛНИТЕЛЬ", FindsColumn = "НАХОДКИ",
        EmptyTitle = "Ваша библиотека готова", EmptyDescription = "Импортируйте историю Shazam, чтобы добавить песни и все распознавания в SongStash.",
        Import = "Импорт", NoSearchResults = "По вашему запросу песни не найдены.", DetailsDeleteSong = "Удалить песню", Delete = "Удалить", Cancel = "Отмена",
        DetailsHeading = "СВЕДЕНИЯ", GenreFormat = "Жанр  ·  {0}", Favorite = "Избранное", AddToFavorites = "Добавить в избранное",
        RemoveFromFavorites = "Убрать из избранного", LinkedReferences = "СВЯЗАННЫЕ ССЫЛКИ", NoLinkedReferences = "Нет связанных ссылок",
        FindHistory = "ИСТОРИЯ НАХОДОК", FindCountFormat = "Находок: {0}", DeleteFind = "Удалить находку", ChooseSong = "Выберите песню",
        DetailsEmptyDescription = "Здесь появятся сведения о песне и история находок.", NotFoundYet = "Пока не найдено", FoundOnce = "1 находка",
        FoundTimesFormat = "Найдено: {0}", SourceShazamApple = "Shazam · Apple", SourceShazamAndroid = "Shazam · Android",
        SourceManual = "Вручную", SourceImported = "Импортировано", DateUnknown = "Дата неизвестна", ImportedAtFormat = "Импортировано {0}",
        DeleteSongConfirmation = "Удалить эту песню и историю её находок?", DeleteFindConfirmation = "Удалить эту находку? Песня останется в библиотеке.",
        LoadFailed = "Не удалось загрузить библиотеку. Попробуйте ещё раз.", FavoriteUpdateFailed = "Не удалось обновить избранное.",
        SongDeleteFailed = "Не удалось удалить песню.", FindDeleteFailed = "Не удалось удалить находку.", DetailsLoadFailed = "Не удалось загрузить сведения о песне.",
        OpenServiceFailed = "Не удалось открыть выбранный музыкальный сервис.", OpenServiceFormat = "Открыть в {0}",
        CsvExportSuccessFormat = "Экспорт CSV успешно завершён.", JsonExportSuccessFormat = "Экспорт JSON успешно завершён.",
        CsvExportFailure = "Не удалось экспортировать CSV. Проверьте место сохранения и попробуйте ещё раз.",
        JsonExportFailure = "Не удалось экспортировать JSON. Проверьте место сохранения и попробуйте ещё раз."
    };
}
