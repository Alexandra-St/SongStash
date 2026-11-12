using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed record OverviewStrings(
    string Title, string Subtitle, string TotalSongs, string TotalFinds, string Favorites, string Artists, string Genres,
    string RecentlyFound, string TopArtists, string TopGenres, string RecentAlbums, string PopularTags,
    string NoFavorites, string NoRecentFinds, string NoArtists, string NoGenres, string NoAlbums, string TagsUnavailable,
    string EmptyTitle, string EmptyDescription, string LoadError, string SongsUnit, string DateUnknown)
{
    public static OverviewStrings For(AppLanguage language) => language == AppLanguage.Russian
        ? new("Обзор", "Ваша музыкальная коллекция с первого взгляда", "Песни", "Находки", "Избранное", "Артисты", "Жанры",
            "Недавно найдены", "Популярные артисты", "Популярные жанры", "Недавно найденные альбомы", "Популярные теги",
            "В избранном пока пусто.", "Пока нет находок с известной датой.", "Артистов пока нет.", "Жанры пока не добавлены.",
            "Альбомов с датированными находками пока нет.", "Связи песен с тегами пока не поддерживаются.",
            "Архив пока пуст", "Импортируйте историю распознаваний, чтобы увидеть свои музыкальные находки.",
            "Не удалось загрузить обзор.", "песен", "Дата неизвестна")
        : new("Overview", "Your music collection at a glance", "Songs", "Finds", "Favorites", "Artists", "Genres",
            "Recently Found", "Top Artists", "Top Genres", "Recently Discovered Albums", "Popular Tags",
            "Your favorites will appear here.", "No finds with a known date yet.", "No artists yet.", "No genres have been added yet.",
            "No albums with dated finds yet.", "Song-to-tag links are not available yet.",
            "Your archive is ready", "Import your recognition history to see your musical discoveries here.",
            "Overview could not be loaded.", "songs", "Date unknown");
}
