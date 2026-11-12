using SongStash.Application.Importing;

namespace SongStash.Desktop.ViewModels;

public sealed class ImportOutcomeRowViewModel(ImportItemResult item, Func<ImportUiText>? getStrings = null)
{
    private ImportUiText Strings => getStrings?.Invoke() ?? ImportUiText.For(SongStash.Application.Settings.AppLanguage.English);
    public string Title => string.IsNullOrWhiteSpace(item.PreviewItem.Candidate.Title)
        ? Strings.UntitledSong
        : item.PreviewItem.Candidate.Title;

    public string Artist => string.IsNullOrWhiteSpace(item.PreviewItem.Candidate.Artist)
        ? Strings.ArtistNotProvided
        : item.PreviewItem.Candidate.Artist;

    public string StatusLabel => item.PreviewItem.Status switch
    {
        ImportPreviewStatus.Incomplete => Strings.Incomplete,
        ImportPreviewStatus.NeedsReview => Strings.NeedsReview,
        ImportPreviewStatus.AlreadyInLibrary => Strings.AlreadyInLibrary,
        _ => Strings.New
    };

    public string Message => GetSafeMessage(item.Message, item.Status);

    private string GetSafeMessage(string? message, ImportItemOutcomeStatus status)
    {
        var fallback = status == ImportItemOutcomeStatus.Failed
            ? Strings.SafeFailureMessage
            : Strings.SafeAttentionMessage;
        if (string.IsNullOrWhiteSpace(message))
        {
            return fallback;
        }

        var localized = message.Trim() switch
        {
            "Complete the title and artist before importing this item." => Strings.CompleteTitleArtist,
            "Review the possible song matches before importing this item." => Strings.ReviewMatches,
            "This preview item cannot be imported automatically." => Strings.CannotImportAutomatically,
            "The matched song is no longer available. Refresh the preview." => Strings.MatchedSongUnavailable,
            "There is not enough event identity data to import safely." => Strings.InsufficientEventIdentity,
            _ => null
        };
        if (localized is not null) return localized;

        var candidate = message.Trim();
        if (candidate.Contains('\n') || candidate.Contains('\r') ||
            candidate.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("SQLite", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("System.", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("Data Source", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains(" at ", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("/Users/", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("\\", StringComparison.Ordinal))
        {
            return fallback;
        }

        return candidate.Length <= 180 ? candidate : string.Concat(candidate.AsSpan(0, 177), "…");
    }
}
