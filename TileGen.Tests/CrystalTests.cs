using Xunit;
using TileGen.Core;
using TileGen.IO;

namespace TileGen.Tests;

/// <summary>
/// The crystal family shares every cluster that could reach a tile edge across the variants of a set, with the
/// edge clusters at the authored density and size (ADR-018), so the wall of the crystal cave, whose base is crystal,
/// must agree on all four edges with the shipped jitter on while the tiles differ.
/// </summary>
public class CrystalTests
{
    [Fact]
    public void TileEdges_AgreeAcrossVariants_AndTilesDiffer()
    {
        var preset = ShippedPresets.Load("crystal_cave");
        Assert.IsType<CrystalStructure>(preset.Wall.Base.Structure);
        var variants = Variants.Resolve(preset, ShippedPresets.TestSeed, 2);
        int size = ShippedPresets.TestSize;
        byte[] first = Generator.Render(new TileRequest(preset, TileKind.Wall, variants[0].Wall, 0.0, variants[0].Seed, size, ShippedPresets.TestSeed));
        byte[] second = Generator.Render(new TileRequest(preset, TileKind.Wall, variants[1].Wall, 0.0, variants[1].Seed, size, ShippedPresets.TestSeed));

        Assert.Equal(Column(first, size, 0), Column(second, size, 0));
        Assert.Equal(Column(first, size, size - 1), Column(second, size, size - 1));
        Assert.Equal(Row(first, size, 0), Row(second, size, 0));
        Assert.Equal(Row(first, size, size - 1), Row(second, size, size - 1));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Writer_RoundTripsEveryCrystalField()
    {
        var preset = ShippedPresets.Load("crystal_cave");
        var again = PresetJson.Parse(PresetJson.Write(preset));
        Assert.Equal(preset.Wall.Base.Structure, again.Wall.Base.Structure);
        Assert.Equal(preset.Floor.Garbage.Structure, again.Floor.Garbage.Structure);
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
