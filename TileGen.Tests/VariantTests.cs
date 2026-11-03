using TileGen.Core;
using Xunit;

namespace TileGen.Tests;

public class VariantTests
{
    [Theory]
    [InlineData("lava_cave")]
    [InlineData("greek_ruins")]
    public void FirstVariants_StayTheSame_WhenTheCountGrows(string presetId)
    {
        var preset = ShippedPresets.Load(presetId);

        var four = Variants.Resolve(preset, ShippedPresets.TestSeed, 4);
        var seven = Variants.Resolve(preset, ShippedPresets.TestSeed, 7);

        for (int i = 0; i < four.Count; i++)
        {
            Assert.Equal(four[i].Index, seven[i].Index);
            Assert.Equal(four[i].Seed, seven[i].Seed);
            Assert.Equal(four[i].Floor.Values, seven[i].Floor.Values);
            Assert.Equal(four[i].Wall.Values, seven[i].Wall.Values);
        }
        Assert.Equal(7, seven.Select(v => v.Seed).Distinct().Count());
    }
}
