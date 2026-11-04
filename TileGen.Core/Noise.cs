namespace TileGen.Core;

/// <summary>
/// Tileable value noise and fractal sums over it. Coordinates are in tile units, so 1.0 is one tile width,
/// and every lattice lookup is taken modulo the integer cell count, which is what makes the result wrap.
/// Only addition, multiplication, floor and comparisons appear here, because the bytes must match on every machine.
/// </summary>
internal static class Noise
{
    /// <summary>Value noise in the range 0 to 1 with the given number of lattice cells per tile.</summary>
    public static double Value(double u, double v, int cells, ulong seed)
    {
        double fx = u * cells;
        double fy = v * cells;
        double flx = Math.Floor(fx);
        double fly = Math.Floor(fy);
        int x0 = Wrap((int)flx, cells);
        int y0 = Wrap((int)fly, cells);
        int x1 = x0 + 1 == cells ? 0 : x0 + 1;
        int y1 = y0 + 1 == cells ? 0 : y0 + 1;
        double sx = Fade(fx - flx);
        double sy = Fade(fy - fly);

        double a = Hashing.Unit(Hashing.Lattice(seed, x0, y0));
        double b = Hashing.Unit(Hashing.Lattice(seed, x1, y0));
        double c = Hashing.Unit(Hashing.Lattice(seed, x0, y1));
        double d = Hashing.Unit(Hashing.Lattice(seed, x1, y1));

        double top = a + (b - a) * sx;
        double bottom = c + (d - c) * sx;
        return top + (bottom - top) * sy;
    }

    /// <summary>
    /// Value noise with its own cell count per axis, which stretches the noise along the axis with fewer cells. Both
    /// counts are integers per tile, so the result still wraps. The isotropic overload keeps its own body, so that
    /// every export made before this overload existed renders the same bytes.
    /// </summary>
    public static double Value(double u, double v, int cellsX, int cellsY, ulong seed)
    {
        double fx = u * cellsX;
        double fy = v * cellsY;
        double flx = Math.Floor(fx);
        double fly = Math.Floor(fy);
        int x0 = Wrap((int)flx, cellsX);
        int y0 = Wrap((int)fly, cellsY);
        int x1 = x0 + 1 == cellsX ? 0 : x0 + 1;
        int y1 = y0 + 1 == cellsY ? 0 : y0 + 1;
        double sx = Fade(fx - flx);
        double sy = Fade(fy - fly);

        double a = Hashing.Unit(Hashing.Lattice(seed, x0, y0));
        double b = Hashing.Unit(Hashing.Lattice(seed, x1, y0));
        double c = Hashing.Unit(Hashing.Lattice(seed, x0, y1));
        double d = Hashing.Unit(Hashing.Lattice(seed, x1, y1));

        double top = a + (b - a) * sx;
        double bottom = c + (d - c) * sx;
        return top + (bottom - top) * sy;
    }

    /// <summary>
    /// Fractal Brownian motion over value noise. The caller passes the number of octaves to keep, because dropping
    /// octaves finer than one pixel depends on the tile size and is decided once per tile.
    /// </summary>
    public static double Fbm(double u, double v, int cells, int octaves, double gain, ulong seed, bool ridged)
    {
        double sum = 0.0;
        double amplitude = 1.0;
        double total = 0.0;
        int octaveCells = cells;
        for (int o = 0; o < octaves; o++)
        {
            double n = Value(u, v, octaveCells, Hashing.Hash64(seed, (ulong)o));
            if (ridged)
            {
                // Folding the noise around its middle turns level sets into ridges, and squaring sharpens them into cracks.
                double r = 1.0 - Math.Abs(2.0 * n - 1.0);
                n = r * r;
            }
            sum += amplitude * n;
            total += amplitude;
            amplitude *= gain;
            octaveCells *= 2;
        }
        return sum / total;
    }

    /// <summary>Counts the octaves whose lattice cell is at least one pixel wide. The first octave is always kept so the field is never empty.</summary>
    public static int UsableOctaves(int cells, int octaves, int size)
    {
        int kept = 0;
        long octaveCells = cells;
        for (int o = 0; o < octaves; o++)
        {
            if (octaveCells > size && kept > 0)
            {
                break;
            }
            kept++;
            octaveCells *= 2;
        }
        return kept;
    }

    /// <summary>The quintic fade of Perlin's improved noise, written as a polynomial so that no transcendental call is needed.</summary>
    public static double Fade(double t)
    {
        return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
    }

    /// <summary>The cubic smoothstep from 0 to 1, used where a softer edge is wanted than a straight ramp.</summary>
    public static double SmoothStep(double t)
    {
        if (t <= 0.0)
        {
            return 0.0;
        }
        if (t >= 1.0)
        {
            return 1.0;
        }
        return t * t * (3.0 - 2.0 * t);
    }

    /// <summary>
    /// A square root from a bit-level first guess and four Newton steps. It uses only integer shifts and basic double
    /// arithmetic, so it gives the same bits on every machine, which is why it stands in for Math.Sqrt in the pixel path.
    /// </summary>
    public static double Sqrt(double x)
    {
        if (!(x > 0.0))
        {
            return 0.0;
        }
        long bits = BitConverter.DoubleToInt64Bits(x);
        double y = BitConverter.Int64BitsToDouble(0x1FF7A3BEA91D9B1BL + (bits >> 1));
        y = 0.5 * (y + x / y);
        y = 0.5 * (y + x / y);
        y = 0.5 * (y + x / y);
        y = 0.5 * (y + x / y);
        return y;
    }

    /// <summary>Non-negative modulo, because warped coordinates can be negative and a negative lattice index would break the wrap.</summary>
    public static int Wrap(int i, int n)
    {
        int m = i % n;
        return m < 0 ? m + n : m;
    }
}
