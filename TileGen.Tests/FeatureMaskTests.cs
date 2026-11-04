using Xunit;
using TileGen.Core;

namespace TileGen.Tests;

/// <summary>
/// A transparent garbage draws every stone and every crystal cluster whole or not at all, decided by the mask under its
/// center (ADR-019). A cluster that can reach a tile edge follows the shared field, so two variants still agree along
/// the seam, and the measured coverage keeps counting the mask.
/// </summary>
public class FeatureMaskTests
{
    [Fact]
    public void TransparentCluster_IsWholeOrAbsent()
    {
        var preset = ShippedPresets.Load("crystal_cave");
        var crystal = Assert.IsType<CrystalStructure>(preset.Floor.Garbage.Structure);
        // One lattice cell holds one cluster that spans most of the tile, which a mask cutting per pixel would show in part.
        var single = preset with
        {
            Floor = preset.Floor with
            {
                Garbage = preset.Floor.Garbage with { Structure = crystal with { Count = 1, Density = 1.0, Size = 0.6, SizeVariation = 0.0 }, Transparency = 1.0 },
                GarbageSoftness = 0.0,
            },
        };
        int size = ShippedPresets.TestSize;
        bool shown = false;
        bool hidden = false;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var request = TileRequest.WithDefaults(single, TileKind.Floor, seed, size);
            byte[] bare = Generator.Render(request with { GarbageCoverage = 0.0 });
            byte[] full = Generator.Render(request with { GarbageCoverage = 1.0 });
            byte[] half = Generator.Render(request with { GarbageCoverage = 0.5 });
            Assert.NotEqual(bare, full);
            if (half.SequenceEqual(full))
            {
                shown = true;
            }
            else
            {
                Assert.Equal(bare, half);
                hidden = true;
            }
        }
        Assert.True(shown, "No seed showed the cluster at half coverage.");
        Assert.True(hidden, "No seed hid the cluster at half coverage.");
    }

    [Fact]
    public void TransparentCrystalGarbage_AgreesAlongTheEdges_AndTilesDiffer()
    {
        var preset = ShippedPresets.Load("crystal_cave");
        Assert.IsType<CrystalStructure>(preset.Floor.Garbage.Structure);
        AssertFloorEdgesAgree(preset);
    }

    [Fact]
    public void TransparentScatterGarbage_AgreesAlongTheEdges_AndTilesDiffer()
    {
        var preset = ShippedPresets.Load("dragon_lair");
        Assert.IsType<ScatterStructure>(preset.Floor.Garbage.Structure);
        AssertFloorEdgesAgree(preset);
    }

    /// <summary>The floor base becomes one flat color, so only the garbage can make the edges of two variants differ.</summary>
    private static void AssertFloorEdgesAgree(Preset preset)
    {
        Assert.Equal(1.0, preset.Floor.Garbage.Transparency.Value);
        var flat = preset with
        {
            Floor = preset.Floor with
            {
                Base = preset.Floor.Base with
                {
                    Ramp = new[] { new RampStop(0.0, new Rgb(20, 24, 40)), new RampStop(1.0, new Rgb(20, 24, 40)) },
                    Detail = preset.Floor.Base.Detail with { Strength = 0.0 },
                },
            },
        };
        var variants = Variants.Resolve(flat, ShippedPresets.TestSeed, 2);
        int size = ShippedPresets.TestSize;
        byte[] first = Generator.Render(new TileRequest(flat, TileKind.Floor, variants[0].Floor, flat.Floor.GarbageCoverage, variants[0].Seed, size, ShippedPresets.TestSeed));
        byte[] second = Generator.Render(new TileRequest(flat, TileKind.Floor, variants[1].Floor, flat.Floor.GarbageCoverage, variants[1].Seed, size, ShippedPresets.TestSeed));

        Assert.Equal(Column(first, size, 0), Column(second, size, 0));
        Assert.Equal(Column(first, size, size - 1), Column(second, size, size - 1));
        Assert.Equal(Row(first, size, 0), Row(second, size, 0));
        Assert.Equal(Row(first, size, size - 1), Row(second, size, size - 1));
        Assert.NotEqual(first, second);
    }

    private static byte[] Column(byte[] pixels, int size, int x)
    {
        var column = new byte[size * 3];
        for (int y = 0; y < size; y++)
        {
            Array.Copy(pixels, (y * size + x) * 3, column, y * 3, 3);
        }
        return column;
    }

    private static byte[] Row(byte[] pixels, int size, int y)
    {
        var row = new byte[size * 3];
        Array.Copy(pixels, y * size * 3, row, 0, size * 3);
        return row;
    }
}
