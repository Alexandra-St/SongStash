using SongStash.Application.Settings;
using SongStash.Desktop.Services;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class PetVisualAssetCatalogTests
{
    [Theory]
    [InlineData(SongStashPet.PinkCat, "PinkCat")]
    [InlineData(SongStashPet.Salem, "Salem")]
    [InlineData(SongStashPet.Ghost, "Ghost")]
    [InlineData(SongStashPet.Bat, "Bat")]
    public void ResolvesAllPetAssetsFromPetOnly(SongStashPet pet, string folder)
    {
        var assets = PetVisualAssetCatalog.Resolve(pet);

        Assert.NotNull(assets);
        var root = $"avares://SongStash.Desktop/Assets/Pets/{folder}/{folder}";
        Assert.Equal($"{root}_BasePreview.png", assets.BasePreview);
        Assert.Equal($"{root}_Bottom.png", assets.Bottom);
        Assert.Equal($"{root}_Middle.png", assets.Middle);
        Assert.Equal($"{root}_Top.png", assets.Top);
    }

    [Fact]
    public void NoneResolvesToNoPetAssets()
    {
        Assert.Null(PetVisualAssetCatalog.Resolve(SongStashPet.None));
    }
}
