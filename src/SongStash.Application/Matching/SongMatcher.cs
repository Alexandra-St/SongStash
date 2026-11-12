using System.Text;
using SongStash.Application.Importing;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Songs;

namespace SongStash.Application.Matching;

/// <summary>Conservative in-memory matching of an import candidate to existing songs.</summary>
public sealed class SongMatcher
{
    private static readonly ExternalReferenceProvider[] ProviderPriority =
    [
        ExternalReferenceProvider.Isrc,
        ExternalReferenceProvider.Shazam,
        ExternalReferenceProvider.AppleMusic
    ];

    public SongMatchResult Match(
        ImportCandidate candidate,
        IEnumerable<Song> songs,
        IEnumerable<ExternalReference> externalReferences)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(songs);
        ArgumentNullException.ThrowIfNull(externalReferences);

        var songList = songs.DistinctBy(song => song.Id).ToArray();
        var songsById = songList.GroupBy(song => song.Id).ToDictionary(group => group.Key, group => group.First());
        var candidateReferences = GetCandidateIds(candidate).ToArray();
        var existingReferences = externalReferences
            .Where(reference => songsById.ContainsKey(reference.SongId) && !string.IsNullOrWhiteSpace(reference.ExternalId))
            .ToArray();

        var externalMatches = new HashSet<Guid>();
        foreach (var candidateReference in candidateReferences)
        {
            foreach (var existing in existingReferences)
            {
                if (candidateReference.Provider == existing.Provider &&
                    IdentifiersEqual(candidateReference.Provider, candidateReference.Id, existing.ExternalId!))
                {
                    externalMatches.Add(existing.SongId);
                }
            }
        }

        if (externalMatches.Count > 1)
        {
            return SongMatchResult.NeedsReview(OrderSongs(songList, externalMatches));
        }

        if (externalMatches.Count == 1)
        {
            var matchId = externalMatches.Single();
            return SongMatchResult.Exact(songsById[matchId], SongMatchReason.ExternalId);
        }

        var normalizedTitle = Normalize(candidate.Title);
        var normalizedArtist = Normalize(candidate.Artist);
        if (normalizedTitle.Length == 0 || normalizedArtist.Length == 0)
        {
            return SongMatchResult.NoMatch();
        }

        var textMatches = songList
            .Where(song =>
                TextEquals(Normalize(song.Title), normalizedTitle) &&
                TextEquals(Normalize(song.Artist), normalizedArtist))
            .ToArray();

        return textMatches.Length switch
        {
            0 => SongMatchResult.NoMatch(),
            1 => SongMatchResult.Exact(textMatches[0], SongMatchReason.ArtistAndTitle),
            _ => SongMatchResult.NeedsReview(textMatches)
        };
    }

    private static IEnumerable<CandidateId> GetCandidateIds(ImportCandidate candidate)
    {
        foreach (var provider in ProviderPriority)
        {
            foreach (var reference in candidate.ExternalReferences.Where(reference => reference.Provider == provider))
            {
                var value = reference.Value.Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                // URL-only candidates do not become an ID match merely because URL is stored in Value.
                if (reference.Url is not null &&
                    Uri.TryCreate(value, UriKind.Absolute, out var valueUri) &&
                    Uri.Compare(valueUri, reference.Url, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped,
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    continue;
                }

                yield return new CandidateId(provider, value);
            }
        }
    }

    private static bool IdentifiersEqual(ExternalReferenceProvider provider, string candidateId, string existingId)
    {
        var comparison = provider == ExternalReferenceProvider.Isrc
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(candidateId.Trim(), existingId.Trim(), comparison);
    }

    private static IReadOnlyList<Song> OrderSongs(IEnumerable<Song> songs, IEnumerable<Guid> matchingIds)
    {
        var byId = songs.ToDictionary(song => song.Id);
        return matchingIds.Select(id => byId[id])
            .OrderBy(song => song.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(song => song.Artist, StringComparer.OrdinalIgnoreCase)
            .ThenBy(song => song.Id)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        var canonical = value.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(canonical.Length);
        var pendingSpace = false;

        foreach (var character in canonical)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character switch
            {
                '\u2018' or '\u2019' or '\u201B' => '\'',
                '\u201C' or '\u201D' or '\u201F' => '"',
                '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2212' => '-',
                _ => character
            });
        }

        return builder.ToString();
    }

    private static bool TextEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed record CandidateId(ExternalReferenceProvider Provider, string Id);
}
