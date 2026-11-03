using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TileGen.App.ViewModels;

namespace TileGen.App.Views;

/// <summary>Turns rendered tiles into bitmaps once each. The cache is weak, so dropped tiles free their bitmaps.</summary>
internal static class TileBitmaps
{
    private static readonly ConditionalWeakTable<TileImage, WriteableBitmap> Cache = new();

    public static WriteableBitmap Get(TileImage tile)
    {
        return Cache.GetValue(tile, Create);
    }

    private static WriteableBitmap Create(TileImage tile)
    {
        var bitmap = new WriteableBitmap(new PixelSize(tile.Size, tile.Size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var buffer = bitmap.Lock();
        var row = new byte[tile.Size * 4];
        for (int y = 0; y < tile.Size; y++)
        {
            int src = y * tile.Size * 3;
            for (int x = 0; x < tile.Size; x++)
            {
                row[x * 4] = tile.Rgb[src + x * 3 + 2];
                row[x * 4 + 1] = tile.Rgb[src + x * 3 + 1];
                row[x * 4 + 2] = tile.Rgb[src + x * 3];
                row[x * 4 + 3] = 255;
            }
            Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
        }
        return bitmap;
    }
}
