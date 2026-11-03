using SkiaSharp;
using TileGen.Core;

namespace TileGen.IO;

/// <summary>
/// The two things an image may give a preset (docs/DECISIONS.md ADR-007): a ramp of dominant colors sorted by
/// luminance, and a base seed hashed from the file bytes. Structure never transfers, so nothing else is read.
/// </summary>
public static class PaletteExtractor
{
    private const int SampleSize = 96;
    private const int Iterations = 16;

    public sealed record Result(IReadOnlyList<RampStop> Ramp, ulong Seed);

    public static Result Extract(string path, int stops)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return Extract(bytes, stops);
    }

    public static Result Extract(byte[] imageBytes, int stops)
    {
        if (stops < 1 || stops > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(stops), "A ramp from an image has between 1 and 16 stops.");
        }
        ulong seed = Hashing.Bytes(imageBytes);
        using var decoded = SKBitmap.Decode(imageBytes) ?? throw new ArgumentException("The file is not an image that can be decoded.");
        using var small = Shrink(decoded);

        var samples = new List<(double R, double G, double B)>(small.Width * small.Height);
        for (int y = 0; y < small.Height; y++)
        {
            for (int x = 0; x < small.Width; x++)
            {
                SKColor c = small.GetPixel(x, y);
                if (c.Alpha < 128)
                {
                    continue;
                }
                samples.Add((c.Red, c.Green, c.Blue));
            }
        }
        if (samples.Count == 0)
        {
            throw new ArgumentException("The image has no opaque pixels to take colors from.");
        }

        var centers = KMeans(samples, stops);
        centers.Sort((a, b) => Luminance(a).CompareTo(Luminance(b)));
        var ramp = new List<RampStop>(stops);
        for (int i = 0; i < centers.Count; i++)
        {
            double t = centers.Count == 1 ? 0.0 : (double)i / (centers.Count - 1);
            ramp.Add(new RampStop(t, new Rgb(ToByte(centers[i].R), ToByte(centers[i].G), ToByte(centers[i].B))));
        }
        return new Result(ramp, seed);
    }

    /// <summary>Starts the clusters at luminance quantiles and refines them, so the same image always gives the same ramp.</summary>
    private static List<(double R, double G, double B)> KMeans(List<(double R, double G, double B)> samples, int k)
    {
        k = Math.Min(k, samples.Count);
        var byLuminance = samples.OrderBy(Luminance).ToList();
        var centers = new List<(double R, double G, double B)>(k);
        for (int i = 0; i < k; i++)
        {
            int index = (int)((i + 0.5) * byLuminance.Count / k);
            centers.Add(byLuminance[Math.Min(index, byLuminance.Count - 1)]);
        }
        var sums = new (double R, double G, double B, int N)[k];
        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Array.Clear(sums);
            foreach (var s in samples)
            {
                int best = 0;
                double bestDistance = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double dr = s.R - centers[c].R;
                    double dg = s.G - centers[c].G;
                    double db = s.B - centers[c].B;
                    double d = dr * dr + dg * dg + db * db;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = c;
                    }
                }
                sums[best] = (sums[best].R + s.R, sums[best].G + s.G, sums[best].B + s.B, sums[best].N + 1);
            }
            for (int c = 0; c < k; c++)
            {
                if (sums[c].N > 0)
                {
                    centers[c] = (sums[c].R / sums[c].N, sums[c].G / sums[c].N, sums[c].B / sums[c].N);
                }
            }
        }
        return centers;
    }

    private static SKBitmap Shrink(SKBitmap source)
    {
        if (source.Width <= SampleSize && source.Height <= SampleSize)
        {
            return source.Copy();
        }
        double scale = Math.Min((double)SampleSize / source.Width, (double)SampleSize / source.Height);
        var info = new SKImageInfo(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)));
        return source.Resize(info, SKFilterQuality.Medium) ?? source.Copy();
    }

    private static double Luminance((double R, double G, double B) c)
    {
        return 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
    }

    private static byte ToByte(double v)
    {
        return (byte)Math.Clamp(Math.Round(v), 0, 255);
    }
}
