using Xunit;
using TileGen.Core;
using TileGen.IO;

namespace TileGen.Tests;

/// <summary>
/// A transparent garbage lets the base show through where its structure has no feature (ADR-017), and the measured
/// coverage still counts the mask, so the slider keeps its meaning.
/// </summary>
public class TransparencyTests
{
    [Fact]
    public void FullyTransparentEmptyGarbage_LeavesTheBaseUntouched()
    {
        var preset = ShippedPresets.Load("enchanted");
        var scatter = Assert.IsType<ScatterStructure>(preset.Floor.Garbage.Structure);
        // No stones at all, so the presence is 0 everywhere and transparency 1 must hand every pixel to the base.
        var empty = preset with
        {
            Floor = preset.Floor with { Garbage = preset.Floor.Garbage with { Structure = scatter with { Density = 0.0 }, Transparency = 1.0 } },
        };
        int size = ShippedPresets.TestSize;
        byte[] bare = Generator.Render(TileRequest.WithDefaults(empty, TileKind.Floor, ShippedPresets.TestSeed, size) with { GarbageCoverage = 0.0 });
        byte[] covered = Generator.Render(TileRequest.WithDefaults(empty, TileKind.Floor, ShippedPresets.TestSeed, size) with { GarbageCoverage = 1.0 });
        Assert.Equal(bare, covered);

        var opaque = empty with
        {
            Floor = empty.Floor with { Garbage = empty.Floor.Garbage with { Transparency = 0.0 } },
        };
        byte[] painted = Generator.Render(TileRequest.WithDefaults(opaque, TileKind.Floor, ShippedPresets.TestSeed, size) with { GarbageCoverage = 1.0 });
        Assert.NotEqual(bare, painted);
    }

    [Fact]
    public void MeasuredCoverage_IgnoresTransparency()
    {
        var preset = ShippedPresets.Load("enchanted");
        var request = TileRequest.WithDefaults(preset, TileKind.Floor, ShippedPresets.TestSeed, ShippedPresets.TestSize) with { GarbageCoverage = 0.3 };
        Assert.InRange(Generator.MeasureCoverage(request), 0.29, 0.31);
    }

    [Fact]
    public void Writer_RoundTripsTransparencyCellColorsAndStars()
    {
        var preset = ShippedPresets.Load("enchanted");
        string written = PresetJson.Write(preset);
        var again = PresetJson.Parse(written);
        // A material holds its ramp as a list, so the written text is compared instead of the record.
        Assert.Equal(written, PresetJson.Write(again));
        Assert.True(again.Floor.Garbage.CellColors);
        Assert.True(again.Floor.Garbage.Transparency.Value > 0.0);
        Assert.True(((ScatterStructure)again.Floor.Garbage.Structure).Stars.Value > 0.0);
    }
}
