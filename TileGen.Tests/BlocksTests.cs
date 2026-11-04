using Xunit;
using TileGen.Core;
using TileGen.IO;

namespace TileGen.Tests;

/// <summary>
/// The blocks family shares its course heights and its edge blocks across the variants of a set (ADR-013), and the
/// loader keeps the parameters that shape them fixed. These tests pin both promises.
/// </summary>
public class BlocksTests
{
    [Theory]
    [InlineData(TileKind.Floor)]
    [InlineData(TileKind.Wall)]
    public void VerticalEdges_AgreeAcrossVariants_AndInteriorDiffers(TileKind kind)
    {
        // Jitter is off so that both variants render with the same mortar width and corner radius. Everything else at the
        // edge, including the detail grain of the cut block, comes from the set seed and has to agree without help.
        var preset = ShippedPresets.Load("dungeon") with { Jitter = 0.0 };
        Assert.IsType<BlocksStructure>(preset.Get(kind).Base.Structure);
        var variants = Variants.Resolve(preset, ShippedPresets.TestSeed, 2);
        int size = ShippedPresets.TestSize;
        byte[] first = Render(preset, kind, variants[0], size);
        byte[] second = Render(preset, kind, variants[1], size);

        Assert.Equal(Column(first, size, 0), Column(second, size, 0));
        Assert.Equal(Column(first, size, size - 1), Column(second, size, size - 1));
        Assert.Equal(Row(first, size, 0), Row(second, size, 0));
        Assert.Equal(Row(first, size, size - 1), Row(second, size, size - 1));
        Assert.NotEqual(Column(first, size, size / 2), Column(second, size, size / 2));
    }

    [Fact]
    public void Loader_RejectsARangeOnALayoutParameter()
    {
        string json = PresetJson.Write(ShippedPresets.Load("dungeon"))
            .Replace("\"rows\": 5", "\"rows\": { \"value\": 5, \"min\": 4, \"max\": 6 }");
        Assert.Contains("\"min\": 4", json);
        var error = Assert.Throws<PresetFormatException>(() => PresetJson.Parse(json));
        Assert.Contains("structure.rows", error.Message);
    }

    [Fact]
    public void Writer_RoundTripsEveryBlocksField()
    {
        var preset = ShippedPresets.Load("dungeon");
        var again = PresetJson.Parse(PresetJson.Write(preset));
        Assert.Equal(preset.Wall.Base.Structure, again.Wall.Base.Structure);
        Assert.Equal(preset.Floor.Base.Structure, again.Floor.Base.Structure);
    }

    private static byte[] Render(Preset preset, TileKind kind, Variant variant, int size)
    {
        var parameters = kind == TileKind.Floor ? variant.Floor : variant.Wall;
        return Generator.Render(new TileRequest(preset, kind, parameters, 0.0, variant.Seed, size, ShippedPresets.TestSeed));
    }

    private static byte[] Row(byte[] pixels, int size, int y)
    {
        var row = new byte[size * 3];
        Array.Copy(pixels, y * size * 3, row, 0, size * 3);
        return row;
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
}
