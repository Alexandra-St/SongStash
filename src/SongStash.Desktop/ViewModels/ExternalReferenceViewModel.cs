using SongStash.Domain.ExternalReferences;

namespace SongStash.Desktop.ViewModels;

public sealed class ExternalReferenceViewModel(ExternalReference reference)
{
    public string Provider => reference.Provider switch
    {
        ExternalReferenceProvider.Shazam => "Shazam",
        ExternalReferenceProvider.AppleMusic => "Apple Music",
        ExternalReferenceProvider.Isrc => "ISRC",
        _ => reference.Provider.ToString()
    };
    public string? ExternalId => reference.ExternalId;
    public string? Url => reference.Url?.AbsoluteUri;
}
