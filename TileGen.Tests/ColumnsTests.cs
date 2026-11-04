using Xunit;
using TileGen.Core;
using TileGen.IO;

namespace TileGen.Tests;

/// <summary>
/// The columns family nests the structure behind its columns (ADR-016), so its parameters must reach the loader, the
/// enumeration, the edit and the writer through the behind path, and the columns must agree across variants and edges.
/// </summary>
public class ColumnsTests
{
    [Fact]
    public void TileEdges_AgreeAcrossVariants_AndTilesDiffer()
    {
        var preset = ShippedPresets.Load("temple") with { Jitter = 0.0 };
        Assert.IsType<ColumnsStructure>(preset.Wall.Base.Structure);
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
    public void BehindParameters_AreEnumerated_Edited_AndWritten()
    {
        var preset = ShippedPresets.Load("temple");
        var descriptors = TileTypeParameters.Enumerate(preset.Wall).ToList();
        var rows = descriptors.Single(d => d.Path == "base.structure.behind.rows");
        Assert.False(rows.CanRange);
        Assert.StartsWith("Behind: ", rows.Label);
        Assert.Contains(descriptors, d => d.Path == "base.structure.behind.mortarWidth" && d.CanRange);
        Assert.Contains(descriptors, d => d.Path == "base.structure.count" && !d.CanRange);

        var edited = TileTypeParameters.With(preset.Wall, "base.structure.behind.rows", new Param(6));
        var columns = Assert.IsType<ColumnsStructure>(edited.Base.Structure);
        var behind = Assert.IsType<BlocksStructure>(columns.Behind);
        Assert.Equal(6.0, behind.Rows.Value);
        Assert.Equal(((ColumnsStructure)preset.Wall.Base.Structure).Count, columns.Count);

        var again = PresetJson.Parse(PresetJson.Write(preset));
        Assert.Equal(preset.Wall.Base.Structure, again.Wall.Base.Structure);
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
