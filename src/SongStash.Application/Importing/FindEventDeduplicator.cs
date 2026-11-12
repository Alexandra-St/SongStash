using SongStash.Domain.Finds;

namespace SongStash.Application.Importing;

/// <summary>Checks whether a candidate event already exists for its matched song.</summary>
public sealed class FindEventDeduplicator
{
    public FindEventDeduplicationResult Evaluate(
        ImportCandidate candidate,
        IEnumerable<FindEvent> existingSongEvents)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existingSongEvents);

        var events = existingSongEvents.ToArray();
        var sourceItemId = NormalizeId(candidate.SourceItemId);
        if (sourceItemId is not null)
        {
            var matches = events
                .Where(existing =>
                    existing.Source == candidate.Source &&
                    string.Equals(NormalizeId(existing.SourceItemId), sourceItemId, StringComparison.Ordinal))
                .Select(existing => existing.Id)
                .ToArray();

            return matches.Length > 0
                ? FindEventDeduplicationResult.Duplicate(matches)
                : FindEventDeduplicationResult.New();
        }

        if (candidate.FoundAt is not { } foundAt)
        {
            return FindEventDeduplicationResult.Indeterminate();
        }

        var foundAtUtcTicks = foundAt.UtcDateTime.Ticks;
        var timestampMatches = events
            .Where(existing =>
                existing.Source == candidate.Source &&
                existing.FoundAt is { } existingFoundAt &&
                existingFoundAt.UtcDateTime.Ticks == foundAtUtcTicks)
            .Select(existing => existing.Id)
            .ToArray();

        return timestampMatches.Length > 0
            ? FindEventDeduplicationResult.Duplicate(timestampMatches)
            : FindEventDeduplicationResult.New();
    }

    private static string? NormalizeId(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
