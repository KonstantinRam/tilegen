using TileGen.Core;
using Xunit;
using Xunit.Abstractions;

namespace TileGen.Tests;

public class TileabilityTests
{
    private readonly ITestOutputHelper _output;

    public TileabilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// A seam pushes the edge mean far above the interior mean, while a wrapping tile stays at or below 1.
    /// The shipped presets measure well below 1, because the wrapped edge straddles a lattice line of every octave,
    /// where the fade curve of the value noise is flat, so the threshold leaves room without hiding a seam.
    /// </summary>
    public const double MaxRatio = 1.5;

    [Theory]
    [MemberData(nameof(ShippedPresets.IdsAndKinds), MemberType = typeof(ShippedPresets))]
    public void WrappedEdges_DifferNoMoreThanInterior(string presetId, TileKind kind)
    {
        var preset = ShippedPresets.Load(presetId);
        var request = TileRequest.WithDefaults(preset, kind, ShippedPresets.TestSeed, ShippedPresets.TestSize);

        byte[] pixels = Generator.Render(request);
        double ratio = SeamRatio(pixels, ShippedPresets.TestSize);
        _output.WriteLine($"{presetId} {kind}: seam ratio {ratio:0.000}");

        Assert.True(ratio < MaxRatio, $"{presetId} {kind}: seam ratio {ratio:0.000} is not under {MaxRatio}.");
    }

    /// <summary>Mean absolute difference across the wrapped edges divided by the mean absolute difference between interior neighbors.</summary>
    public static double SeamRatio(byte[] pixels, int size)
    {
        double edgeSum = 0.0;
        long edgeCount = 0;
        double interiorSum = 0.0;
        long interiorCount = 0;

        for (int y = 0; y < size; y++)
        {
            for (int c = 0; c < 3; c++)
            {
                edgeSum += Math.Abs(At(y, size - 1, c) - At(y, 0, c));
                edgeCount++;
                for (int x = 0; x < size - 1; x++)
                {
                    interiorSum += Math.Abs(At(y, x + 1, c) - At(y, x, c));
                    interiorCount++;
                }
            }
        }
        for (int x = 0; x < size; x++)
        {
            for (int c = 0; c < 3; c++)
            {
                edgeSum += Math.Abs(At(size - 1, x, c) - At(0, x, c));
                edgeCount++;
                for (int y = 0; y < size - 1; y++)
                {
                    interiorSum += Math.Abs(At(y + 1, x, c) - At(y, x, c));
                    interiorCount++;
                }
            }
        }

        double edgeMean = edgeSum / edgeCount;
        double interiorMean = interiorSum / interiorCount;
        // A flat tile has no interior differences and no seam, so it counts as wrapping.
        return interiorMean == 0.0 ? 0.0 : edgeMean / interiorMean;

        int At(int y, int x, int c) => pixels[(y * size + x) * 3 + c];
    }
}
