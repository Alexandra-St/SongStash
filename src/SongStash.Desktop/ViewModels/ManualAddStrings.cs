using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed record ManualAddStrings(
    string Title, string TitleField, string Artist, string Album, string Genre, string Notes, string ArtworkUrl, string InvalidArtworkUrl, string DateFound,
    string UseToday, string Time, string Day, string Month, string Year, string InvalidDate, string Save, string Cancel, string MatchTitle, string ExactMatchMessage,
    string ReviewMatchMessage, string Attach, string KeepSeparate, string TitleRequired,
    string ArtistRequired, string SaveFailed)
{
    public static ManualAddStrings For(AppLanguage language) => language == AppLanguage.Russian
        ? new("Добавить песню", "Название", "Исполнитель", "Альбом", "Жанр", "Заметки", "URL обложки", "Введите корректный URL обложки.", "Дата находки",
            "Сегодня", "Время", "День", "Месяц", "Год", "Выберите допустимую дату.", "Сохранить", "Отмена", "Возможные совпадения",
            "Эта песня уже есть. Добавьте находку к ней или сохраните отдельную версию.",
            "Найдены похожие песни. Выберите песню или явно сохраните отдельную версию.",
            "Добавить находку", "Сохранить отдельно", "Введите название песни.", "Введите исполнителя.",
            "Не удалось сохранить песню. Попробуйте ещё раз.")
        : new("Add Manually", "Title", "Artist", "Album", "Genre", "Notes", "Artwork URL", "Enter a valid artwork URL.", "Date Found",
            "Use today", "Time", "Day", "Month", "Year", "Choose a valid date.", "Save", "Cancel", "Possible matches",
            "This song is already in your library. Add a find to it or keep a separate version.",
            "Possible matches were found. Choose a song or explicitly keep a separate version.",
            "Add find to this song", "Keep as separate song", "Enter a song title.", "Enter an artist.",
            "The song could not be saved. Please try again.");
}
