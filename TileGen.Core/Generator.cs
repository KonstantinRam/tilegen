namespace TileGen.Core;

/// <summary>
/// The one entry point of the generator. The GUI, the CLI and the tests all call Render, so a preview and an export
/// of the same request are the same bytes.
/// </summary>
public static class Generator
{
    /// <summary>Written into settings.json, so that an old export says which generator produced it.</summary>
    public const string Version = "0.1.0";

    public static readonly IReadOnlyList<int> SupportedSizes = new[] { 64, 128, 256, 512, 1024 };

    /// <summary>
    /// Renders one tile as a size by size buffer of RGB bytes in row-major order. The token lets a preview that is
    /// already stale stop between rows, and cancelling never changes the bytes of a render that completes.
    /// </summary>
    public static byte[] Render(TileRequest request, CancellationToken cancellation = default)
    {
        Validate(request);
        int size = request.Size;
        var type = TileResolver.Resolve(request.Preset.Get(request.Kind), request.Parameters);
        ulong tileSeed = TileSeed(request);
        ulong setKindSeed = SetKindSeed(request);
        GarbageMask.Result mask = GarbageMask.Compute(size, MaskSettings(request, type), cancellation);
        double[] weights = mask.Weights;
        double transparency = type.Garbage.Transparency;
        // A transparent garbage draws a stone or a crystal cluster whole or not at all, decided by the mask under its center (ADR-019).
        FeatureFilter? filter = transparency > 0.0 ? mask.Covers : null;
        var baseSampler = new MaterialSampler(type.Base, size, Hashing.Hash64(tileSeed, 1), Hashing.Hash64(setKindSeed, 1));
        var garbageSampler = new MaterialSampler(type.Garbage, size, Hashing.Hash64(tileSeed, 2), Hashing.Hash64(setKindSeed, 2), filter);
        bool byFeature = filter != null && garbageSampler.HasFeatures;

        var pixels = new byte[size * size * 3];
        double inv = 1.0 / size;
        var options = new ParallelOptions { CancellationToken = cancellation };
        // Rows run in parallel because every pixel depends only on its own coordinates, so scheduling cannot change the bytes.
        Parallel.For(0, size, options, y =>
        {
            double v = (y + 0.5) * inv;
            for (int x = 0; x < size; x++)
            {
                double u = (x + 0.5) * inv;
                double w = weights[y * size + x];
                double r;
                double g;
                double b;
                if (byFeature)
                {
                    baseSampler.Shade(u, v, out r, out g, out b);
                    garbageSampler.Shade(u, v, out double r1, out double g1, out double b1, out double presence);
                    // A feature that passed the filter is whole, so only the haze of a partly transparent garbage follows the mask per pixel.
                    w = w * (1.0 - transparency) + transparency * presence;
                    r += (r1 - r) * w;
                    g += (g1 - g) * w;
                    b += (b1 - b) * w;
                }
                else if (w <= 0.0)
                {
                    baseSampler.Shade(u, v, out r, out g, out b);
                }
                else if (w >= 1.0 && transparency <= 0.0)
                {
                    garbageSampler.Shade(u, v, out r, out g, out b);
                }
                else
                {
                    baseSampler.Shade(u, v, out double r0, out double g0, out double b0);
                    garbageSampler.Shade(u, v, out double r1, out double g1, out double b1, out double presence);
                    // The measured coverage counts the mask, so transparency changes what the dust looks like and not how much of the tile it takes.
                    w *= 1.0 - transparency * (1.0 - presence);
                    r = r0 + (r1 - r0) * w;
                    g = g0 + (g1 - g0) * w;
                    b = b0 + (b1 - b0) * w;
                }
                int i = (y * size + x) * 3;
                pixels[i] = ToByte(r);
                pixels[i + 1] = ToByte(g);
                pixels[i + 2] = ToByte(b);
            }
        });
        return pixels;
    }

    /// <summary>The garbage coverage the request would render, measured the way the tests and the exporter define it.</summary>
    public static double MeasureCoverage(TileRequest request, CancellationToken cancellation = default)
    {
        return GarbageMask.Measure(RenderCoverageWeights(request, cancellation));
    }

    /// <summary>The blend weight of the garbage material per pixel, which the tests read to check coverage and edge agreement.</summary>
    public static double[] RenderCoverageWeights(TileRequest request, CancellationToken cancellation = default)
    {
        Validate(request);
        var type = TileResolver.Resolve(request.Preset.Get(request.Kind), request.Parameters);
        return GarbageMask.Compute(request.Size, MaskSettings(request, type), cancellation).Weights;
    }

    /// <summary>
    /// The variant's own field uses its resolved scale and seed. The shared field uses the authored scale and the set
    /// seed, both identical across the variants of a set, which is what makes the edges agree.
    /// </summary>
    private static GarbageMask.Settings MaskSettings(TileRequest request, ResolvedTileType type)
    {
        var authored = request.Preset.Get(request.Kind);
        ulong tileSeed = TileSeed(request);
        ulong setKindSeed = SetKindSeed(request);
        return new GarbageMask.Settings(
            type.GarbageScale,
            type.GarbageSoftness,
            authored.GarbageScale.Value,
            Math.Max(0.0, authored.GarbageSoftness.Value),
            request.GarbageCoverage,
            Hashing.Hash64(tileSeed, 3),
            Hashing.Hash64(setKindSeed, 3));
    }

    /// <summary>The floor and the wall of one variant share a seed, so the kind salts it to keep equal materials from rendering equal tiles.</summary>
    private static ulong TileSeed(TileRequest request)
    {
        return Hashing.Hash64(request.Seed, (ulong)request.Kind + 1);
    }

    /// <summary>The set-level counterpart of the tile seed, which every variant of a set derives identically.</summary>
    private static ulong SetKindSeed(TileRequest request)
    {
        return Hashing.Hash64(request.SetSeed, (ulong)request.Kind + 1);
    }

    private static void Validate(TileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Preset);
        ArgumentNullException.ThrowIfNull(request.Parameters);
        if (!SupportedSizes.Contains(request.Size))
        {
            throw new ArgumentOutOfRangeException(nameof(request.Size), $"Size {request.Size} is not one of {string.Join(", ", SupportedSizes)}.");
        }
        if (double.IsNaN(request.GarbageCoverage) || request.GarbageCoverage < 0.0 || request.GarbageCoverage > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.GarbageCoverage), "Garbage coverage is a fraction from 0 to 1.");
        }
    }

    private static byte ToByte(double value)
    {
        if (value <= 0.0)
        {
            return 0;
        }
        if (value >= 255.0)
        {
            return 255;
        }
        return (byte)(value + 0.5);
    }
}
