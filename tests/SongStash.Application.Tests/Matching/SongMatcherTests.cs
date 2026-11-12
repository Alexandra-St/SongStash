using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using Xunit;

namespace SongStash.Application.Tests.Matching;

public sealed class SongMatcherTests
{
    private readonly SongMatcher _matcher = new();

    [Theory]
    [InlineData(ExternalReferenceProvider.Isrc, "USAAA0000001")]
    [InlineData(ExternalReferenceProvider.Shazam, "track-key")]
    [InlineData(ExternalReferenceProvider.AppleMusic, "123456")]
    public void MatchingExternalIdProducesExactMatch(ExternalReferenceProvider provider, string id)
    {
        var song = new Song("Stored title", "Stored artist");
        var reference = new ExternalReference(song.Id, provider, id);
        var candidate = Candidate("Completely different", "Artist",
            new ExternalReferenceCandidate(provider, id));

        var result = _matcher.Match(candidate, [song], [reference]);

        Assert.Equal(SongMatchStatus.ExactMatch, result.Status);
        Assert.Equal(SongMatchReason.ExternalId, result.Reason);
        Assert.Same(song, result.MatchedSong);
    }

    [Fact]
    public void ExternalIdsAreComparedOnlyWithinSameProvider()
    {
        var song = new Song("Unrelated title", "Unrelated artist");
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.AppleMusic, "same-id");
        var candidate = Candidate("Different title", "Different artist",
            new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "same-id"));

        var result = _matcher.Match(candidate, [song], [reference]);

        Assert.Equal(SongMatchStatus.NoMatch, result.Status);
    }

    [Fact]
    public void ArtistAndTitleMatchWithoutExternalIds()
    {
        var song = new Song("Blue Song", "The Artist");

        var result = _matcher.Match(Candidate("Blue Song", "The Artist"), [song], []);

        Assert.Equal(SongMatchStatus.ExactMatch, result.Status);
        Assert.Equal(SongMatchReason.ArtistAndTitle, result.Reason);
        Assert.Same(song, result.MatchedSong);
    }

    [Fact]
    public void CaseDifferencesDoNotPreventTextMatch()
    {
        var song = new Song("Blue Song", "The Artist");

        var result = _matcher.Match(Candidate("blue song", "THE ARTIST"), [song], []);

        Assert.Equal(SongMatchStatus.ExactMatch, result.Status);
    }

    [Fact]
    public void LeadingTrailingAndRepeatedWhitespaceDoNotPreventTextMatch()
    {
        var song = new Song("Blue Song", "The Artist");

        var result = _matcher.Match(Candidate("  Blue   Song  ", " The\t Artist "), [song], []);

        Assert.Equal(SongMatchStatus.ExactMatch, result.Status);
    }

    [Fact]
    public void CanonicallyEquivalentUnicodeTextMatches()
    {
        var song = new Song("Café Song", "Björk");

        var result = _matcher.Match(Candidate("Cafe\u0301 Song", "Bjo\u0308rk"), [song], []);

        Assert.Equal(SongMatchStatus.ExactMatch, result.Status);
    }

    [Theory]
    [InlineData("Song (Live)")]
    [InlineData("Song Remix")]
    [InlineData("Song (Acoustic)")]
    [InlineData("Song (Remastered)")]
    public void VersionMarkersAreNotDiscarded(string candidateTitle)
    {
        var song = new Song("Song", "Artist");

        var result = _matcher.Match(Candidate(candidateTitle, "Artist"), [song], []);

        Assert.Equal(SongMatchStatus.NoMatch, result.Status);
    }

    [Fact]
    public void DifferentArtistDoesNotAutomaticallyMatch()
    {
        var song = new Song("Song", "Artist feat. Guest");

        var result = _matcher.Match(Candidate("Song", "Artist"), [song], []);

        Assert.Equal(SongMatchStatus.NoMatch, result.Status);
    }

    [Fact]
    public void MultipleTextMatchesRequireReview()
    {
        var first = new Song("Song", "Artist");
        var second = new Song("Song", "Artist");

        var result = _matcher.Match(Candidate("Song", "Artist"), [first, second], []);

        Assert.Equal(SongMatchStatus.NeedsReview, result.Status);
        Assert.Equal(SongMatchReason.Ambiguous, result.Reason);
        Assert.Null(result.MatchedSong);
        Assert.Equal(2, result.PossibleMatches.Count);
    }

    [Fact]
    public void ExternalIdMatchTakesPriorityOverTextMatch()
    {
        var externalMatch = new Song("Stored name", "Stored artist");
        var textMatch = new Song("Candidate name", "Candidate artist");
        var reference = new ExternalReference(externalMatch.Id, ExternalReferenceProvider.Isrc, "USAAA0000001");
        var candidate = Candidate("Candidate name", "Candidate artist",
            new ExternalReferenceCandidate(ExternalReferenceProvider.Isrc, "USAAA0000001"));

        var result = _matcher.Match(candidate, [textMatch, externalMatch], [reference]);

        Assert.Equal(SongMatchStatus.ExactMatch, result.Status);
        Assert.Equal(SongMatchReason.ExternalId, result.Reason);
        Assert.Same(externalMatch, result.MatchedSong);
    }

    [Fact]
    public void ConflictingExternalIdsRequireReview()
    {
        var isrcSong = new Song("First", "Artist");
        var shazamSong = new Song("Second", "Artist");
        var references = new[]
        {
            new ExternalReference(isrcSong.Id, ExternalReferenceProvider.Isrc, "USAAA0000001"),
            new ExternalReference(shazamSong.Id, ExternalReferenceProvider.Shazam, "track-key")
        };
        var candidate = Candidate("Unrelated", "Artist",
            new ExternalReferenceCandidate(ExternalReferenceProvider.Isrc, "USAAA0000001"),
            new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key"));

        var result = _matcher.Match(candidate, [isrcSong, shazamSong], references);

        Assert.Equal(SongMatchStatus.NeedsReview, result.Status);
        Assert.Equal(2, result.PossibleMatches.Count);
    }

    [Fact]
    public void SourceItemIdDoesNotParticipateInSongMatching()
    {
        var song = new Song("Existing song", "Artist");
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "recognition-event-id");
        var candidate = Candidate("Different song", "Artist") with { SourceItemId = "recognition-event-id" };

        var result = _matcher.Match(candidate, [song], [reference]);

        Assert.Equal(SongMatchStatus.NoMatch, result.Status);
    }

    [Fact]
    public void UrlAloneIsNotUsedAsAnExternalId()
    {
        var url = new Uri("https://example.test/track");
        var song = new Song("Existing", "Artist");
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, url: url);
        var candidate = Candidate("Different", "Artist",
            new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, url.AbsoluteUri, url));

        var result = _matcher.Match(candidate, [song], [reference]);

        Assert.Equal(SongMatchStatus.NoMatch, result.Status);
    }

    [Fact]
    public void NoSafeMatchReturnsNoMatch()
    {
        var song = new Song("Stored title", "Stored artist");

        var result = _matcher.Match(Candidate("Other title", "Other artist"), [song], []);

        Assert.Equal(SongMatchStatus.NoMatch, result.Status);
        Assert.Null(result.MatchedSong);
        Assert.Empty(result.PossibleMatches);
    }

    private static ImportCandidate Candidate(
        string title,
        string artist,
        params ExternalReferenceCandidate[] references) =>
        new(title, artist, FindSource.ImportedUnknown)
        {
            ExternalReferences = references
        };
}
