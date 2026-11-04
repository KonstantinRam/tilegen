namespace TileGen.Core;

/// <summary>
/// The coverage mask of the garbage material. Along the tile edges the mask follows a field that every variant of the
/// set shares, so garbage continues across the seam between two different variants, and inside it follows the
/// variant's own field. The crossover runs along an irregular curve near the edge rather than a straight band, so a
/// blob cut by the crossover keeps a noise-shaped edge. The variant's threshold is chosen so that exactly
/// round(coverage * pixels) pixels count as covered, which keeps the slider honest (ADR-004 and ADR-012).
/// </summary>
internal static class GarbageMask
{
    /// <summary>The mask only needs blob shapes, and the schema has no octave field for it, so three octaves are fixed here.</summary>
    private const int Octaves = 3;

    /// <summary>The edge term never fully owns a pixel, so every pixel keeps a finite limit for the threshold search.</summary>
    private const double MinOwnWeight = 1e-9;

    /// <summary>
    /// The crossover sits this fraction of a shared mask cell from the edge on average, bounded so it stays a few pixels
    /// wide at 256 and never dominates the tile. The cut field moves it between half and one and a half times that, so a
    /// shared blob that neither neighbor continues ends as a half blob with a ragged edge rather than a thin strip.
    /// </summary>
    private const double CrossoverPerCell = 0.5;
    private const double MinCrossover = 0.02;
    private const double MaxCrossover = 0.2;
    private const double MinReachFactor = 0.5;
    private const double ReachSpread = 1.0;

    public readonly record struct Settings(
        double OwnScale,
        double OwnSoftness,
        double SharedScale,
        double SharedSoftness,
        double Coverage,
        ulong OwnSeed,
        ulong SharedSeed);

    /// <summary>
    /// The mask of one tile. Weights holds one blend weight per pixel in row-major order, where a pixel whose weight is
    /// at least 0.5 counts as covered and exactly round(coverage * pixels) pixels satisfy that unless two pixels tie
    /// exactly. Covers answers for a whole feature by the pixel under its center (ADR-019).
    /// </summary>
    public sealed class Result
    {
        private readonly int _size;
        private readonly bool[] _sharedCovered;

        internal Result(int size, double[] weights, bool[] sharedCovered)
        {
            _size = size;
            Weights = weights;
            _sharedCovered = sharedCovered;
        }

        public double[] Weights { get; }

        /// <summary>
        /// Whether a feature centered at the given tile coordinates is drawn. A feature that every variant shares follows
        /// the shared field alone, because its pixels reach the seam where the variants must agree, and a variant's own
        /// feature follows the variant's mask under its center, so either kind is whole or absent instead of cut.
        /// </summary>
        public bool Covers(double u, double v, bool shared)
        {
            int x = Math.Clamp((int)Math.Floor(u * _size), 0, _size - 1);
            int y = Math.Clamp((int)Math.Floor(v * _size), 0, _size - 1);
            int i = y * _size + x;
            return shared ? _sharedCovered[i] : Weights[i] >= 0.5;
        }
    }

    public static Result Compute(int size, Settings s, CancellationToken cancellation = default)
    {
        int count = size * size;
        var weights = new double[count];
        var sharedCovered = new bool[count];
        if (s.Coverage <= 0.0)
        {
            return new Result(size, weights, sharedCovered);
        }
        if (s.Coverage >= 1.0)
        {
            Array.Fill(weights, 1.0);
            Array.Fill(sharedCovered, true);
            return new Result(size, weights, sharedCovered);
        }
        int covered = (int)Math.Round(s.Coverage * count);
        if (covered <= 0)
        {
            return new Result(size, weights, sharedCovered);
        }
        if (covered >= count)
        {
            Array.Fill(weights, 1.0);
            Array.Fill(sharedCovered, true);
            return new Result(size, weights, sharedCovered);
        }

        var options = new ParallelOptions { CancellationToken = cancellation };
        double[] own = NormalizedField(size, s.OwnScale, Octaves, s.OwnSeed, options);
        double[] shared = NormalizedField(size, s.SharedScale, Octaves, s.SharedSeed, options);
        // The cut field is finer than the mask and belongs to the variant, so the crossover curve differs between variants.
        double[] cut = NormalizedField(size, s.SharedScale * 0.5, 2, Hashing.Hash64(s.OwnSeed, 0x435554UL), options);

        // The shared field and the coverage are the same for every variant of the set, so this threshold is too.
        var sortedShared = (double[])shared.Clone();
        Array.Sort(sortedShared);
        double sharedThreshold = sortedShared[count - covered];

        // The edge distance counts from the pixel's outer edge, so the outermost ring is decided by the shared field alone.
        double crossover = Math.Clamp(CrossoverPerCell * s.SharedScale, MinCrossover, MaxCrossover);
        var distance = new double[size];
        for (int i = 0; i < size; i++)
        {
            distance[i] = Math.Min(i, size - 1 - i) / (double)size;
        }

        // A pixel is covered when the threshold offset is at or below its limit, so the k-th largest limit is the exact offset.
        var edge = new double[count];
        var limits = new double[count];
        Parallel.For(0, size, options, y =>
        {
            int row = y * size;
            for (int x = 0; x < size; x++)
            {
                int i = row + x;
                double reach = crossover * (MinReachFactor + ReachSpread * cut[i]);
                double e = 1.0 - Noise.SmoothStep(Math.Min(distance[x], distance[y]) / reach);
                edge[i] = e;
                sharedCovered[i] = shared[i] >= sharedThreshold;
                double ownWeight = Math.Max(1.0 - e, MinOwnWeight);
                limits[i] = own[i] + e * (shared[i] - sharedThreshold) / ownWeight;
            }
        });
        var sortedLimits = (double[])limits.Clone();
        Array.Sort(sortedLimits);
        double offset = sortedLimits[count - covered];

        Parallel.For(0, size, options, y =>
        {
            int row = y * size;
            for (int x = 0; x < size; x++)
            {
                int i = row + x;
                double e = edge[i];
                double ownWeight = Math.Max(1.0 - e, MinOwnWeight);
                // Multiplying by a positive weight keeps the sign of the comparison, so the covered count stays exact.
                double d = ownWeight * (limits[i] - offset);
                double softness = s.OwnSoftness + e * (s.SharedSoftness - s.OwnSoftness);
                weights[i] = softness > 0.0 ? Noise.SmoothStep(d / softness + 0.5) : (d >= 0.0 ? 1.0 : 0.0);
            }
        });
        return new Result(size, weights, sharedCovered);
    }

    /// <summary>The measured coverage, defined as the fraction of pixels whose blend weight is at least 0.5.</summary>
    public static double Measure(double[] weights)
    {
        int covered = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] >= 0.5)
            {
                covered++;
            }
        }
        return (double)covered / weights.Length;
    }

    /// <summary>Fractal noise scaled to the range 0 to 1 over the tile, so that softness means the same on every field.</summary>
    private static double[] NormalizedField(int size, double scale, int octaveCount, ulong seed, ParallelOptions options)
    {
        int cells = TileResolver.CellsFromScale(scale);
        int octaves = Noise.UsableOctaves(cells, octaveCount, size);
        int count = size * size;
        var field = new double[count];
        var rowMin = new double[size];
        var rowMax = new double[size];
        double inv = 1.0 / size;
        Parallel.For(0, size, options, y =>
        {
            double v = (y + 0.5) * inv;
            double min = double.MaxValue;
            double max = double.MinValue;
            int row = y * size;
            for (int x = 0; x < size; x++)
            {
                double f = Noise.Fbm((x + 0.5) * inv, v, cells, octaves, 0.5, seed, false);
                field[row + x] = f;
                if (f < min)
                {
                    min = f;
                }
                if (f > max)
                {
                    max = f;
                }
            }
            rowMin[y] = min;
            rowMax[y] = max;
        });
        double fieldMin = rowMin.Min();
        double range = rowMax.Max() - fieldMin;
        if (range > 0.0)
        {
            Parallel.For(0, size, options, y =>
            {
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    field[row + x] = (field[row + x] - fieldMin) / range;
                }
            });
        }
        return field;
    }
}
