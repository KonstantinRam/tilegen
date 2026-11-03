using TileGen.Core;
using Xunit;

namespace TileGen.Tests;

public class CoverageTests
{
    public const double Tolerance = 0.01;

    [Theory]
    [MemberData(nameof(ShippedPresets.IdsKindsAndCoverages), MemberType = typeof(ShippedPresets))]
    public void MeasuredGarbageCoverage_MatchesRequested(string presetId, TileKind kind, double coverage)
    {
        var preset = ShippedPresets.Load(presetId);
        var defaults = TileRequest.WithDefaults(preset, kind, ShippedPresets.TestSeed, ShippedPresets.TestSize);
        var request = defaults with { GarbageCoverage = coverage };

        double measured = Generator.MeasureCoverage(request);

        Assert.True(Math.Abs(measured - coverage) <= Tolerance,
            $"{presetId} {kind}: requested {coverage:0.000}, measured {measured:0.000}.");
    }
}

public class EdgeAgreementTests
{
    /// <summary>At the outermost pixel ring the variant term has a weight near zero, so only rounding separates two variants there.</summary>
    public const double MaxEdgeDifference = 0.02;

    [Theory]
    [MemberData(nameof(ShippedPresets.IdsAndKinds), MemberType = typeof(ShippedPresets))]
    public void GarbageMask_AgreesAlongEdges_BetweenVariantsOfOneSet(string presetId, TileKind kind)
    {
        var preset = ShippedPresets.Load(presetId);
        var variants = Variants.Resolve(preset, ShippedPresets.TestSeed, 2);
        int size = ShippedPresets.TestSize;
        double[] first = Weights(preset, kind, variants[0], size);
        double[] second = Weights(preset, kind, variants[1], size);

        double edgeDifference = 0.0;
        for (int i = 0; i < size; i++)
        {
            edgeDifference = Math.Max(edgeDifference, Math.Abs(first[i] - second[i]));
            edgeDifference = Math.Max(edgeDifference, Math.Abs(first[(size - 1) * size + i] - second[(size - 1) * size + i]));
            edgeDifference = Math.Max(edgeDifference, Math.Abs(first[i * size] - second[i * size]));
            edgeDifference = Math.Max(edgeDifference, Math.Abs(first[i * size + size - 1] - second[i * size + size - 1]));
        }
        double centerDifference = 0.0;
        for (int y = size / 4; y < 3 * size / 4; y++)
        {
            for (int x = size / 4; x < 3 * size / 4; x++)
            {
                centerDifference = Math.Max(centerDifference, Math.Abs(first[y * size + x] - second[y * size + x]));
            }
        }

        Assert.True(edgeDifference < MaxEdgeDifference, $"{presetId} {kind}: variants differ by {edgeDifference:0.000} along the edges.");
        Assert.True(centerDifference > 0.5, $"{presetId} {kind}: variants should differ in the interior, but the largest difference is {centerDifference:0.000}.");
    }

    private static double[] Weights(Preset preset, TileKind kind, Variant variant, int size)
    {
        var request = new TileRequest(preset, kind, kind == TileKind.Floor ? variant.Floor : variant.Wall, 0.5, variant.Seed, size, ShippedPresets.TestSeed);
        return Generator.RenderCoverageWeights(request);
    }
}
