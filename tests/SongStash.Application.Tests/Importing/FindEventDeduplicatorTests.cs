using SongStash.Application.Importing;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using Xunit;

namespace SongStash.Application.Tests.Importing;

public sealed class FindEventDeduplicatorTests
{
    private readonly FindEventDeduplicator _deduplicator = new();
    private static readonly DateTimeOffset ImportedAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SameSourceAndSourceItemIdIsDuplicate()
    {
        var existing = Event(FindSource.ShazamApple, sourceItemId: "event-1");

        var result = Evaluate(Candidate(FindSource.ShazamApple, sourceItemId: "event-1"), existing);

        Assert.Equal(FindEventDeduplicationStatus.Duplicate, result.Status);
        Assert.Equal([existing.Id], result.MatchedEventIds);
    }

    [Fact]
    public void DifferentSourceItemIdIsNewEvenWhenFoundAtMatches()
    {
        var time = ImportedAt.AddMinutes(-1);
        var existing = Event(FindSource.ShazamApple, foundAt: time, sourceItemId: "event-a");

        var result = Evaluate(Candidate(FindSource.ShazamApple, foundAt: time, sourceItemId: "event-b"), existing);

        Assert.Equal(FindEventDeduplicationStatus.New, result.Status);
    }

    [Fact]
    public void SameIdFromDifferentSourceIsNotDuplicate()
    {
        var existing = Event(FindSource.ShazamAndroid, sourceItemId: "event-1");

        var result = Evaluate(Candidate(FindSource.ShazamApple, sourceItemId: "event-1"), existing);

        Assert.Equal(FindEventDeduplicationStatus.New, result.Status);
    }

    [Fact]
    public void SourceItemIdOuterWhitespaceIsTrimmed()
    {
        var existing = Event(FindSource.ShazamApple, sourceItemId: " event-1 ");

        var result = Evaluate(Candidate(FindSource.ShazamApple, sourceItemId: "  event-1"), existing);

        Assert.Equal(FindEventDeduplicationStatus.Duplicate, result.Status);
    }

    [Fact]
    public void SourceItemIdTakesPriorityOverDifferentFoundAt()
    {
        var existing = Event(FindSource.ShazamApple,
            foundAt: ImportedAt.AddMinutes(-5), sourceItemId: "event-1");

        var result = Evaluate(Candidate(FindSource.ShazamApple,
            foundAt: ImportedAt, sourceItemId: "event-1"), existing);

        Assert.Equal(FindEventDeduplicationStatus.Duplicate, result.Status);
    }

    [Fact]
    public void SameSourceAndFoundAtIsDuplicateWhenSourceItemIdIsMissing()
    {
        var time = ImportedAt.AddMinutes(-1);
        var existing = Event(FindSource.ShazamApple, foundAt: time, sourceItemId: " ");

        var result = Evaluate(Candidate(FindSource.ShazamApple, foundAt: time), existing);

        Assert.Equal(FindEventDeduplicationStatus.Duplicate, result.Status);
    }

    [Fact]
    public void DifferentFoundAtIsNewWhenSourceItemIdIsMissing()
    {
        var existing = Event(FindSource.ShazamApple, foundAt: ImportedAt.AddMinutes(-1));

        var result = Evaluate(Candidate(FindSource.ShazamApple, foundAt: ImportedAt), existing);

        Assert.Equal(FindEventDeduplicationStatus.New, result.Status);
    }

    [Fact]
    public void SameFoundAtFromDifferentSourceIsNotDuplicate()
    {
        var existing = Event(FindSource.ShazamAndroid, foundAt: ImportedAt);

        var result = Evaluate(Candidate(FindSource.ShazamApple, foundAt: ImportedAt), existing);

        Assert.Equal(FindEventDeduplicationStatus.New, result.Status);
    }

    [Fact]
    public void MissingSourceItemIdAndFoundAtIsIndeterminate()
    {
        var result = Evaluate(Candidate(FindSource.ShazamApple), Event(FindSource.ShazamApple));

        Assert.Equal(FindEventDeduplicationStatus.Indeterminate, result.Status);
        Assert.Empty(result.MatchedEventIds);
    }

    [Fact]
    public void ExistingEventWithoutFoundAtDoesNotMatchTimestampFallback()
    {
        var existing = Event(FindSource.ShazamApple, foundAt: null);

        var result = Evaluate(Candidate(FindSource.ShazamApple, foundAt: ImportedAt), existing);

        Assert.Equal(FindEventDeduplicationStatus.New, result.Status);
    }

    [Fact]
    public void TwoRepeatRecognitionsWithDifferentIdsAndTimesRemainDistinct()
    {
        var eventA = Event(FindSource.ShazamApple,
            foundAt: ImportedAt.AddMinutes(-8), sourceItemId: "recognition-a");
        var eventB = Event(FindSource.ShazamApple,
            foundAt: ImportedAt, sourceItemId: "recognition-b");

        var candidateA = Candidate(FindSource.ShazamApple,
            foundAt: eventA.FoundAt, sourceItemId: eventA.SourceItemId);
        var candidateB = Candidate(FindSource.ShazamApple,
            foundAt: eventB.FoundAt, sourceItemId: eventB.SourceItemId);

        Assert.Equal(FindEventDeduplicationStatus.New, Evaluate(candidateA, eventB).Status);
        Assert.Equal(FindEventDeduplicationStatus.New, Evaluate(candidateB, eventA).Status);
    }

    [Fact]
    public void ExternalReferencesDoNotParticipateInEventDeduplication()
    {
        var candidate = Candidate(FindSource.ShazamApple) with
        {
            ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "same-track-key")]
        };
        var existing = Event(FindSource.ShazamApple);

        var result = Evaluate(candidate, existing);

        Assert.Equal(FindEventDeduplicationStatus.Indeterminate, result.Status);
    }

    [Fact]
    public void MultipleMatchingEventsAreAllReturned()
    {
        var first = Event(FindSource.ShazamApple, sourceItemId: "event-1");
        var second = Event(FindSource.ShazamApple, sourceItemId: " event-1 ");

        var result = Evaluate(Candidate(FindSource.ShazamApple, sourceItemId: "event-1"), first, second);

        Assert.Equal(FindEventDeduplicationStatus.Duplicate, result.Status);
        Assert.Equal(new[] { first.Id, second.Id }, result.MatchedEventIds);
    }

    [Fact]
    public void EmptyEventsWithSourceItemIdIsNew()
    {
        var result = Evaluate(Candidate(FindSource.ShazamApple, sourceItemId: "event-1"));

        Assert.Equal(FindEventDeduplicationStatus.New, result.Status);
    }

    [Fact]
    public void EmptyEventsWithFoundAtIsNew()
    {
        var result = Evaluate(Candidate(FindSource.ShazamApple, foundAt: ImportedAt));

        Assert.Equal(FindEventDeduplicationStatus.New, result.Status);
    }

    [Fact]
    public void EmptyEventsWithoutEventIdentityIsIndeterminate()
    {
        var result = Evaluate(Candidate(FindSource.ShazamApple));

        Assert.Equal(FindEventDeduplicationStatus.Indeterminate, result.Status);
    }

    [Fact]
    public void FoundAtOffsetsRepresentingSameInstantMatch()
    {
        var candidateTime = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.FromHours(2));
        var eventTime = candidateTime.ToUniversalTime();
        var existing = Event(FindSource.ShazamApple, foundAt: eventTime);

        var result = Evaluate(Candidate(FindSource.ShazamApple, foundAt: candidateTime), existing);

        Assert.Equal(FindEventDeduplicationStatus.Duplicate, result.Status);
    }

    [Fact]
    public void CandidateSourceItemIdIsNotComparedToFindEventId()
    {
        var existing = Event(FindSource.ShazamApple);
        var candidate = Candidate(FindSource.ShazamApple, sourceItemId: existing.Id.ToString());

        var result = Evaluate(candidate, existing);

        Assert.Equal(FindEventDeduplicationStatus.New, result.Status);
    }

    private FindEventDeduplicationResult Evaluate(ImportCandidate candidate, params FindEvent[] events) =>
        _deduplicator.Evaluate(candidate, events);

    private static FindEvent Event(
        FindSource source,
        DateTimeOffset? foundAt = null,
        string? sourceItemId = null) =>
        new(Guid.NewGuid(), source, ImportedAt, foundAt, sourceItemId);

    private static ImportCandidate Candidate(
        FindSource source,
        DateTimeOffset? foundAt = null,
        string? sourceItemId = null) =>
        new("Title", "Artist", source)
        {
            FoundAt = foundAt,
            SourceItemId = sourceItemId
        };
}
