using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

/// <summary>English and Russian user-facing copy for the import workflow.</summary>
public sealed record ImportUiText
{
    public required string ImportTitle { get; init; }
    public required string NavigationImport { get; init; }
    public required string ImportSubtitle { get; init; }
    public required string SelectSource { get; init; }
    public required string PreviewStep { get; init; }
    public required string ImportStep { get; init; }
    public required string ImportingStep { get; init; }
    public required string ResultsStep { get; init; }
    public required string AvailableSource { get; init; }
    public required string PreviewTitle { get; init; }
    public required string PreviewSubtitle { get; init; }
    public required string Total { get; init; }
    public required string NewSongs { get; init; }
    public required string ExistingSongs { get; init; }
    public required string IncompleteItems { get; init; }
    public required string ReviewItems { get; init; }
    public required string TitleColumn { get; init; }
    public required string ArtistColumn { get; init; }
    public required string AlbumColumn { get; init; }
    public required string FoundAtColumn { get; init; }
    public required string StatusColumn { get; init; }
    public required string SelectionHelp { get; init; }
    public required string ImportComplete { get; init; }
    public required string Back { get; init; }
    public required string ImportSelected { get; init; }
    public required string Importing { get; init; }
    public required string ImportingDetails { get; init; }
    public required string CancelImport { get; init; }
    public required string ImportCancelled { get; init; }
    public required string ImportRunFailed { get; init; }
    public required string ImportRunFailedDetails { get; init; }
    public required string CancelledDetails { get; init; }
    public required string CancelDetails { get; init; }
    public required string SafeFailureMessage { get; init; }
    public required string SafeAttentionMessage { get; init; }
    public required string ResultsTitle { get; init; }
    public required string ResultsSubtitle { get; init; }
    public required string AddedSongs { get; init; }
    public required string AddedFindEvents { get; init; }
    public required string SkippedDuplicates { get; init; }
    public required string NeedsAttention { get; init; }
    public required string Failed { get; init; }
    public required string FailedItems { get; init; }
    public required string DuplicateOnlyMessage { get; init; }
    public required string EmptyResultMessage { get; init; }
    public required string BackToLibrary { get; init; }
    public required string ImportMore { get; init; }
    public required string ArtistNotProvided { get; init; }
    public required string UntitledSong { get; init; }
    public required string NoIssues { get; init; }
    public required string NoFailedItems { get; init; }
    public required string NoAttentionItems { get; init; }
    public required string ResultsFooter { get; init; }
    public required string CancelledFooter { get; init; }
    public required string OnlySongMetadata { get; init; }
    public required string ShazamSourceTitle { get; init; }
    public required string ShazamSourceDescription { get; init; }
    public required string FileSourceTitle { get; init; }
    public required string FileSourceDescription { get; init; }
    public required string ChooseFile { get; init; }
    public required string FileTypeLabel { get; init; }
    public required string ReadingFile { get; init; }
    public required string FileUnavailable { get; init; }
    public required string UnsupportedFile { get; init; }
    public required string FileReadFailed { get; init; }
    public required string FileCardHint { get; init; }
    public required string ReadHistory { get; init; }
    public required string ReadingHistory { get; init; }
    public required string UnknownDate { get; init; }
    public required string New { get; init; }
    public required string AlreadyInLibrary { get; init; }
    public required string Incomplete { get; init; }
    public required string NeedsReview { get; init; }
    public required string SourceUnavailable { get; init; }
    public required string UnsupportedSchema { get; init; }
    public required string ReadFailed { get; init; }
    public required string Cancelled { get; init; }
    public required string CancelReading { get; init; }
    public required string ChooseOneSource { get; init; }
    public required string ImportRunsLocally { get; init; }
    public required string SelectedItemsFormat { get; init; }
    public required string CandidatesFoundFormat { get; init; }
    public required string NewSongsCountFormat { get; init; }
    public required string FindEventsCountFormat { get; init; }
    public required string FailedCountFormat { get; init; }
    public required string PreviewFailedDiagnostic { get; init; }
    public required string SourceReadFailedDiagnostic { get; init; }
    public required string OperationFailedDiagnostic { get; init; }
    public required string FileNotFoundDiagnostic { get; init; }
    public required string FileAccessDeniedDiagnostic { get; init; }
    public required string CsvEmptyDiagnostic { get; init; }
    public required string CsvMalformedDiagnostic { get; init; }
    public required string CsvHeaderMissingDiagnostic { get; init; }
    public required string CsvColumnsMissingDiagnostic { get; init; }
    public required string CsvEncodingDiagnostic { get; init; }
    public required string JsonShapeDiagnostic { get; init; }
    public required string JsonRowDiagnostic { get; init; }
    public required string JsonMalformedDiagnostic { get; init; }
    public required string EmptyFileDiagnostic { get; init; }
    public required string DatePrecisionDiagnostic { get; init; }
    public required string InvalidDateDiagnostic { get; init; }
    public required string IncompleteRowDiagnostic { get; init; }
    public required string InvalidSourceDiagnostic { get; init; }
    public required string InvalidScalarDiagnostic { get; init; }
    public required string InvalidUrlDiagnostic { get; init; }
    public required string InvalidReferenceDiagnostic { get; init; }
    public required string InvalidShazamUrlDiagnostic { get; init; }
    public required string CompleteTitleArtist { get; init; }
    public required string ReviewMatches { get; init; }
    public required string CannotImportAutomatically { get; init; }
    public required string MatchedSongUnavailable { get; init; }
    public required string InsufficientEventIdentity { get; init; }

    public static ImportUiText For(AppLanguage language) => language == AppLanguage.Russian ? Russian : English;

    public string SelectedItems(int count) => string.Format(SelectedItemsFormat, count);
    public string CandidatesFound(int count) => string.Format(CandidatesFoundFormat, count);
    public string NewSongsCount(int count) => string.Format(NewSongsCountFormat, count);
    public string FindEventsCount(int count) => string.Format(FindEventsCountFormat, count);
    public string FailedCount(int count) => string.Format(FailedCountFormat, count);

    public string Diagnostic(string code, string fallback) => code switch
    {
        "import.preview.failed" => PreviewFailedDiagnostic,
        "import.read.failed" => SourceReadFailedDiagnostic,
        "import.operation.failed" => OperationFailedDiagnostic,
        "file.not_found" => FileNotFoundDiagnostic,
        "file.access_denied" => FileAccessDeniedDiagnostic,
        "csv.empty" => CsvEmptyDiagnostic,
        "csv.malformed" => CsvMalformedDiagnostic,
        "csv.headers.missing" => CsvHeaderMissingDiagnostic,
        "csv.columns.missing" => CsvColumnsMissingDiagnostic,
        "csv.encoding.invalid" => CsvEncodingDiagnostic,
        "json.shape.unsupported" => JsonShapeDiagnostic,
        "json.row.invalid" => JsonRowDiagnostic,
        "json.malformed" => JsonMalformedDiagnostic,
        "file.empty" => EmptyFileDiagnostic,
        "date.precision.unsupported" => DatePrecisionDiagnostic,
        "date.invalid" => InvalidDateDiagnostic,
        "row.incomplete" => IncompleteRowDiagnostic,
        "source.invalid" => InvalidSourceDiagnostic,
        "row.value.invalid" => InvalidScalarDiagnostic,
        "artwork.invalid" => InvalidUrlDiagnostic,
        "reference.invalid" => InvalidReferenceDiagnostic,
        "reference.url.invalid" => InvalidShazamUrlDiagnostic,
        "file.read_failed" => FileReadFailed,
        _ => fallback
    };

    private static readonly ImportUiText English = new()
    {
        ImportTitle = "Import Music", NavigationImport = "Import", ImportSubtitle = "Bring your recognition history into your collection.",
        SelectSource = "Select Source", PreviewStep = "Preview", ImportStep = "Import", ImportingStep = "Importing", ResultsStep = "Results", AvailableSource = "Local source",
        PreviewTitle = "Import Preview", PreviewSubtitle = "Review the songs found and choose which ones to import.",
        Total = "TOTAL", NewSongs = "NEW SONGS", ExistingSongs = "ALREADY IN LIBRARY", IncompleteItems = "INCOMPLETE", ReviewItems = "NEEDS REVIEW",
        TitleColumn = "TITLE", ArtistColumn = "ARTIST", AlbumColumn = "ALBUM", FoundAtColumn = "DATE FOUND", StatusColumn = "STATUS",
        SelectionHelp = "Incomplete and review items need attention before they can be imported.", ImportComplete = "Import complete",
        Back = "Back", ImportSelected = "Import Selected", Importing = "Importing selected songs…",
        ImportingDetails = "Each song and its find events are being saved to your library.", CancelImport = "Cancel import",
        ImportCancelled = "Import cancelled", ImportRunFailed = "Import could not be completed",
        ImportRunFailedDetails = "The import stopped unexpectedly. Any items already saved remain in your library.",
        CancelledDetails = "Items saved before cancellation remain in your library. Partial counts are unavailable for this run.",
        CancelDetails = "Items saved before cancellation will remain in your library.", SafeFailureMessage = "Could not save this item. You can try importing it again.",
        SafeAttentionMessage = "This item needs attention before it can be imported.", ResultsTitle = "Import Results",
        ResultsSubtitle = "Review what was added and which items need attention.", AddedSongs = "Added Songs", AddedFindEvents = "Added Find Events",
        SkippedDuplicates = "Skipped Duplicates", NeedsAttention = "Needs Attention", Failed = "Failed", FailedItems = "Failed items",
        DuplicateOnlyMessage = "These recognition events were already in your library.", EmptyResultMessage = "No items were processed in this import.",
        BackToLibrary = "Back to Library", ImportMore = "Import More", ArtistNotProvided = "Artist not provided", UntitledSong = "Untitled song",
        NoIssues = "No failed or attention items.", NoFailedItems = "No failed items.", NoAttentionItems = "No attention items.", ResultsFooter = "The changes are saved locally in your library.",
        CancelledFooter = "You can return to your library or start another import.", OnlySongMetadata = "Only song information is read. Your audio files stay untouched.",
        ShazamSourceTitle = "Shazam on this Mac", ShazamSourceDescription = "Read the recognition history stored by Music Recognition on this Mac.",
        FileSourceTitle = "CSV / JSON File", FileSourceDescription = "Import music metadata from a UTF-8 CSV file or a simple JSON file.",
        ChooseFile = "Choose CSV or JSON file", FileTypeLabel = "CSV or JSON", ReadingFile = "Reading your file…", FileUnavailable = "The selected file could not be found or accessed.",
        UnsupportedFile = "This file format or structure is not supported.", FileReadFailed = "The selected file could not be read.",
        FileCardHint = "Supported fields: Title, Artist, Album, ArtworkUrl, FoundAt, Source, SourceItemId, and external IDs.",
        ReadHistory = "Read history", ReadingHistory = "Reading your Shazam history…", UnknownDate = "Unknown", New = "New",
        AlreadyInLibrary = "Already in Library", Incomplete = "Incomplete", NeedsReview = "Needs Review",
        SourceUnavailable = "Shazam history is unavailable on this Mac.",
        UnsupportedSchema = "SongStash found Shazam data, but this format version is not supported yet.",
        ReadFailed = "Shazam history could not be read.", Cancelled = "The operation was cancelled. Any items already saved remain in your library.",
        CancelReading = "Cancel reading", ChooseOneSource = "Choose one source to continue", ImportRunsLocally = "Import runs locally on this Mac.",
        SelectedItemsFormat = "{0} selected items", CandidatesFoundFormat = "{0} candidates found", NewSongsCountFormat = "{0} new songs",
        FindEventsCountFormat = "{0} find events", FailedCountFormat = "{0} failed", PreviewFailedDiagnostic = "The import preview could not be prepared.",
        SourceReadFailedDiagnostic = "The selected import source could not be read.", OperationFailedDiagnostic = "The import could not be completed.",
        FileNotFoundDiagnostic = "The selected file could not be found.", FileAccessDeniedDiagnostic = "SongStash does not have access to the selected file.",
        CsvEmptyDiagnostic = "The CSV file is empty.", CsvMalformedDiagnostic = "The CSV file has invalid quoting or row structure.",
        CsvHeaderMissingDiagnostic = "The CSV file does not contain a header row.", CsvColumnsMissingDiagnostic = "The CSV file must include Title and Artist columns.",
        CsvEncodingDiagnostic = "The CSV file is not valid UTF-8.", JsonShapeDiagnostic = "JSON must be an array of records or an object with a songs array.",
        JsonRowDiagnostic = "A non-object record was skipped.", JsonMalformedDiagnostic = "The JSON file is malformed or is not valid UTF-8.",
        EmptyFileDiagnostic = "The selected file is empty.", DatePrecisionDiagnostic = "A date without a time and timezone was not imported as a find event.",
        InvalidDateDiagnostic = "The found date was not a valid ISO-8601 timestamp with a timezone.",
        IncompleteRowDiagnostic = "The row is missing a title or artist.", InvalidSourceDiagnostic = "The source value was not recognized; Imported Unknown was used.",
        InvalidScalarDiagnostic = "A field value was not a supported scalar.", InvalidUrlDiagnostic = "A URL value was invalid and was skipped.",
        InvalidReferenceDiagnostic = "An empty external reference was skipped.", InvalidShazamUrlDiagnostic = "A Shazam URL was invalid and was skipped.",
        CompleteTitleArtist = "Complete the title and artist before importing this item.",
        ReviewMatches = "Review the possible song matches before importing this item.",
        CannotImportAutomatically = "This preview item cannot be imported automatically.",
        MatchedSongUnavailable = "The matched song is no longer available. Refresh the preview.",
        InsufficientEventIdentity = "There is not enough event identity data to import safely."
    };

    private static readonly ImportUiText Russian = new()
    {
        ImportTitle = "Импорт музыки", NavigationImport = "Импорт", ImportSubtitle = "Добавьте в коллекцию историю распознаваний.",
        SelectSource = "Выбор источника", PreviewStep = "Просмотр", ImportStep = "Импорт", ImportingStep = "Импорт", ResultsStep = "Результаты", AvailableSource = "Доступен",
        PreviewTitle = "Просмотр импорта", PreviewSubtitle = "Проверьте найденные песни и выберите, какие импортировать.",
        Total = "ВСЕГО", NewSongs = "НОВЫЕ ПЕСНИ", ExistingSongs = "УЖЕ В БИБЛИОТЕКЕ", IncompleteItems = "НЕПОЛНЫЕ", ReviewItems = "ТРЕБУЮТ ПРОВЕРКИ",
        TitleColumn = "НАЗВАНИЕ", ArtistColumn = "ИСПОЛНИТЕЛЬ", AlbumColumn = "АЛЬБОМ", FoundAtColumn = "ДАТА НАХОДКИ", StatusColumn = "СТАТУС",
        SelectionHelp = "Перед импортом проверьте неполные записи и записи, требующие внимания.", ImportComplete = "Импорт завершён",
        Back = "Назад", ImportSelected = "Импортировать выбранное", Importing = "Импорт выбранных песен…",
        ImportingDetails = "Песни и сведения об их находках сохраняются в библиотеке.", CancelImport = "Отменить импорт",
        ImportCancelled = "Импорт отменён", ImportRunFailed = "Не удалось завершить импорт",
        ImportRunFailedDetails = "Импорт неожиданно остановился. Уже сохранённые записи останутся в библиотеке.",
        CancelledDetails = "Записи, сохранённые до отмены, останутся в библиотеке. Частичные итоги недоступны.",
        CancelDetails = "Записи, сохранённые до отмены, останутся в библиотеке.", SafeFailureMessage = "Не удалось сохранить запись. Попробуйте импортировать её ещё раз.",
        SafeAttentionMessage = "Перед импортом эту запись нужно проверить.", ResultsTitle = "Результаты импорта",
        ResultsSubtitle = "Проверьте добавленные записи и те, которым нужно внимание.", AddedSongs = "Добавлено песен", AddedFindEvents = "Добавлено находок",
        SkippedDuplicates = "Пропущено повторов", NeedsAttention = "Требуют внимания", Failed = "Ошибки", FailedItems = "Не удалось импортировать",
        DuplicateOnlyMessage = "Эти распознавания уже есть в библиотеке.", EmptyResultMessage = "В этом импорте нет обработанных записей.",
        BackToLibrary = "К библиотеке", ImportMore = "Импортировать ещё", ArtistNotProvided = "Исполнитель не указан", UntitledSong = "Без названия",
        NoIssues = "Нет ошибок и записей, требующих внимания.", NoFailedItems = "Нет записей с ошибками.", NoAttentionItems = "Нет записей, требующих внимания.", ResultsFooter = "Изменения сохранены в локальной библиотеке.",
        CancelledFooter = "Можно вернуться в библиотеку или начать новый импорт.", OnlySongMetadata = "Импортируются только сведения о песнях. Аудиофайлы не затрагиваются.",
        ShazamSourceTitle = "Shazam на этом Mac", ShazamSourceDescription = "Прочитать историю распознаваний Music Recognition на этом Mac.",
        FileSourceTitle = "Файл CSV / JSON", FileSourceDescription = "Импортировать метаданные из файла UTF-8 CSV или простого JSON.",
        ChooseFile = "Выбрать файл CSV или JSON", FileTypeLabel = "CSV или JSON", ReadingFile = "Чтение файла…", FileUnavailable = "Не удалось найти выбранный файл или получить к нему доступ.",
        UnsupportedFile = "Формат или структура файла не поддерживается.", FileReadFailed = "Не удалось прочитать выбранный файл.",
        FileCardHint = "Поддерживаемые поля: Title, Artist, Album, ArtworkUrl, FoundAt, Source, SourceItemId и внешние идентификаторы.",
        ReadHistory = "Прочитать историю", ReadingHistory = "Чтение истории Shazam…", UnknownDate = "Неизвестно", New = "Новая",
        AlreadyInLibrary = "Уже в библиотеке", Incomplete = "Неполная", NeedsReview = "Требует проверки",
        SourceUnavailable = "История Shazam недоступна на этом Mac.",
        UnsupportedSchema = "Найдены данные Shazam, но эта версия формата пока не поддерживается.",
        ReadFailed = "Не удалось прочитать историю Shazam.", Cancelled = "Операция отменена. Уже сохранённые записи останутся в библиотеке.",
        CancelReading = "Отменить чтение", ChooseOneSource = "Выберите источник, чтобы продолжить", ImportRunsLocally = "Импорт выполняется локально на этом Mac.",
        SelectedItemsFormat = "Выбрано записей: {0}", CandidatesFoundFormat = "Найдено записей: {0}", NewSongsCountFormat = "Новых песен: {0}",
        FindEventsCountFormat = "Находок: {0}", FailedCountFormat = "Ошибок: {0}", PreviewFailedDiagnostic = "Не удалось подготовить просмотр импорта.",
        SourceReadFailedDiagnostic = "Не удалось прочитать выбранный источник импорта.", OperationFailedDiagnostic = "Не удалось завершить импорт.",
        FileNotFoundDiagnostic = "Не удалось найти выбранный файл.", FileAccessDeniedDiagnostic = "SongStash не удалось получить доступ к выбранному файлу.",
        CsvEmptyDiagnostic = "Файл CSV пуст.", CsvMalformedDiagnostic = "В CSV неверно оформлены кавычки или строки.",
        CsvHeaderMissingDiagnostic = "В CSV отсутствует строка заголовков.", CsvColumnsMissingDiagnostic = "В CSV должны быть столбцы Title и Artist.",
        CsvEncodingDiagnostic = "Файл CSV не является корректным UTF-8.", JsonShapeDiagnostic = "JSON должен быть массивом записей или объектом с массивом songs.",
        JsonRowDiagnostic = "Запись не в виде объекта пропущена.", JsonMalformedDiagnostic = "Файл JSON повреждён или имеет неверную кодировку UTF-8.",
        EmptyFileDiagnostic = "Выбранный файл пуст.", DatePrecisionDiagnostic = "Дата без времени и часового пояса не импортирована как находка.",
        InvalidDateDiagnostic = "Дата находки должна быть временной меткой ISO-8601 с часовым поясом.",
        IncompleteRowDiagnostic = "В строке не указано название или исполнитель.", InvalidSourceDiagnostic = "Источник не распознан; использовано значение «Импортировано».",
        InvalidScalarDiagnostic = "Значение поля имеет неподдерживаемый формат.", InvalidUrlDiagnostic = "Некорректный URL пропущен.",
        InvalidReferenceDiagnostic = "Пустая внешняя ссылка пропущена.", InvalidShazamUrlDiagnostic = "Некорректная ссылка Shazam пропущена.",
        CompleteTitleArtist = "Перед импортом укажите название и исполнителя.",
        ReviewMatches = "Перед импортом проверьте возможные совпадения песен.",
        CannotImportAutomatically = "Эту запись нельзя импортировать автоматически.",
        MatchedSongUnavailable = "Найденная песня больше недоступна. Обновите просмотр импорта.",
        InsufficientEventIdentity = "Недостаточно данных о распознавании для безопасного импорта."
    };
}
