using SongStash.Application.Settings;

namespace SongStash.Desktop.Services;

public sealed record PetVisualAssetPaths(
    string BasePreview,
    string Bottom,
    string Middle,
    string Top,
    string? Down = null,
    string? TopBackground = null);

public static class PetVisualAssetCatalog
{
    public static PetVisualAssetPaths? Resolve(SongStashPet pet)
    {
        var folder = pet switch
        {
            SongStashPet.PinkCat => "PinkCat",
            SongStashPet.Salem => "Salem",
            SongStashPet.Ghost => "Ghost",
            SongStashPet.Bat => "Bat",
            _ => null
        };

        if (folder is null) return null;
        var root = $"avares://SongStash.Desktop/Assets/Pets/{folder}/{folder}";
        return new PetVisualAssetPaths(
            $"{root}_BasePreview.png",
            $"{root}_Bottom.png",
            $"{root}_Middle.png",
            $"{root}_Top.png",
            pet == SongStashPet.PinkCat ? "avares://SongStash.Desktop/Assets/Pets/PinkCat/PinkCat_Down.png" : null,
            pet == SongStashPet.PinkCat ? "avares://SongStash.Desktop/Assets/Pets/PinkCat/PinkCat_TopBackground.png" : null);
    }
}
