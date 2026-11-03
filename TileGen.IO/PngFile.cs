using System.Runtime.InteropServices;
using SkiaSharp;

namespace TileGen.IO;

/// <summary>Encodes an RGB buffer as an opaque 8-bit PNG. The buffer is opaque, so the encoder writes a PNG without an alpha channel.</summary>
public static class PngFile
{
    public static byte[] Encode(byte[] rgb, int width, int height)
    {
        if (rgb.Length != width * height * 3)
        {
            throw new ArgumentException($"Expected {width * height * 3} bytes for {width} by {height} RGB, got {rgb.Length}.");
        }
        var info = new SKImageInfo(width, height, SKColorType.Rgb888x, SKAlphaType.Opaque);
        using var bitmap = new SKBitmap(info);
        var padded = new byte[width * height * 4];
        for (int i = 0, j = 0; i < rgb.Length; i += 3, j += 4)
        {
            padded[j] = rgb[i];
            padded[j + 1] = rgb[i + 1];
            padded[j + 2] = rgb[i + 2];
            padded[j + 3] = 255;
        }
        Marshal.Copy(padded, 0, bitmap.GetPixels(), padded.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("PNG encoding failed.");
        return data.ToArray();
    }

    public static void Write(string path, byte[] rgb, int width, int height)
    {
        File.WriteAllBytes(path, Encode(rgb, width, height));
    }
}
