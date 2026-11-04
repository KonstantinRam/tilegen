using Xunit;
using TileGen.Core;
using TileGen.IO;

namespace TileGen.Tests;

/// <summary>
/// The scatter family shares its ground and every stone that touches a tile edge across the variants of a set, with
/// the edge stones at the authored density and size (ADR-015). The swamp preset keeps its jitter here on purpose, so
/// the test also shows that a ranged density or size never reaches an edge.
/// </summary>
public class ScatterTests
{
    [Theory]
    [InlineData(TileKind.Floor)]
    [InlineData(TileKind.Wall)]
    public void TileEdges_AgreeAcrossVariants_AndTilesDiffer(TileKind kind)
    {
        var preset = ShippedPresets.Load("swamp");
        Assert.IsType<ScatterStructure>(preset.Get(kind).Base.Structure);
        var variants = Variants.Resolve(preset, ShippedPresets.TestSeed, 2);
        int size = ShippedPresets.TestSize;
        byte[] first = Render(preset, kind, variants[0], size);
        byte[] second = Render(preset, kind, variants[1], size);

        Assert.Equal(Column(first, size, 0), Column(second, size, 0));
        Assert.Equal(Column(first, size, size - 1), Column(second, size, size - 1));
        Assert.Equal(Row(first, size, 0), Row(second, size, 0));
        Assert.Equal(Row(first, size, size - 1), Row(second, size, size - 1));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Writer_RoundTripsEveryScatterField()
    {
        var preset = ShippedPresets.Load("swamp");
        var again = PresetJson.Parse(PresetJson.Write(preset));
        Assert.Equal(preset.Floor.Base.Structure, again.Floor.Base.Structure);
        Assert.Equal(preset.Wall.Base.Structure, again.Wall.Base.Structure);
    }

    private static byte[] Render(Preset preset, TileKind kind, Variant variant, int size)
    {
        var parameters = kind == TileKind.Floor ? variant.Floor : variant.Wall;
        return Generator.Render(new TileRequest(preset, kind, parameters, 0.0, variant.Seed, size, ShippedPresets.TestSeed));
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
