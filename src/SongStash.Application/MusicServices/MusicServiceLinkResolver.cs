using SongStash.Application.Settings;
using SongStash.Domain.ExternalReferences;

namespace SongStash.Application.MusicServices;

/// <summary>Builds public web links for opening a song in an external music service.</summary>
public sealed class MusicServiceLinkResolver
{
    public Uri Resolve(string title, string? artist, IEnumerable<ExternalReference> references, MusicService service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(references);

        if (service == MusicService.AppleMusic)
        {
            var direct = references.FirstOrDefault(reference =>
                reference.Provider == ExternalReferenceProvider.AppleMusic && IsAppleMusicUrl(reference.Url))?.Url;
            if (direct is not null) return direct;
        }

        var query = string.IsNullOrWhiteSpace(artist) ? title.Trim() : $"{artist.Trim()} {title.Trim()}";
        var encoded = Uri.EscapeDataString(query);
        var url = service switch
        {
            MusicService.YouTubeMusic => $"https://music.youtube.com/search?q={encoded}",
            MusicService.YouTube => $"https://www.youtube.com/results?search_query={encoded}",
            MusicService.Spotify => $"https://open.spotify.com/search/{encoded}",
            MusicService.AppleMusic => $"https://music.apple.com/us/search?term={encoded}",
            _ => throw new ArgumentOutOfRangeException(nameof(service))
        };
        return new Uri(url, UriKind.Absolute);
    }

    private static bool IsAppleMusicUrl(Uri? uri) => uri is { IsAbsoluteUri: true, Scheme: "https" } &&
        (uri.Host.Equals("music.apple.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".music.apple.com", StringComparison.OrdinalIgnoreCase));
}
