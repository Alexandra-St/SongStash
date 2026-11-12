namespace SongStash.Application.Importing;

public sealed record FindEventDeduplicationResult(
    FindEventDeduplicationStatus Status,
    IReadOnlyList<Guid> MatchedEventIds)
{
    public static FindEventDeduplicationResult New() => new(FindEventDeduplicationStatus.New, []);

    public static FindEventDeduplicationResult Indeterminate() =>
        new(FindEventDeduplicationStatus.Indeterminate, []);

    public static FindEventDeduplicationResult Duplicate(IEnumerable<Guid> matchedEventIds) =>
        new(FindEventDeduplicationStatus.Duplicate, matchedEventIds.Distinct().ToArray());
}
