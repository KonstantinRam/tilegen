using TileGen.Core;

namespace TileGen.IO;

/// <summary>
/// Builds the review image: the floor variants in a 4 by 4 wrapped grid on the left and the wall variants on the right,
/// with the variants mixed, so that a seam or a repeat shows at a glance.
/// </summary>
public static class ContactSheet
{
    public const int GridCells = 4;
    private const int Gutter = 16;
    private const byte GutterGray = 96;

    /// <summary>The shuffle seed is fixed, so two sheets of the same set put the same variant in the same cell.</summary>
    private const ulong ArrangementSeed = 0x53484545544UL;

    public static (byte[] Rgb, int Width, int Height) Build(IReadOnlyList<byte[]> floorTiles, IReadOnlyList<byte[]> wallTiles, int size)
    {
        int grid = GridCells * size;
        int width = grid * 2 + Gutter;
        int height = grid;
        var sheet = new byte[width * height * 3];
        Array.Fill(sheet, GutterGray);
        int[] arrangement = Arrangement(floorTiles.Count);
        Blit(sheet, width, 0, floorTiles, arrangement, size);
        Blit(sheet, width, grid + Gutter, wallTiles, Arrangement(wallTiles.Count), size);
        return (sheet, width, height);
    }

    /// <summary>Every variant appears at least once when there are at most 16, and the order looks mixed rather than striped.</summary>
    public static int[] Arrangement(int variantCount)
    {
        if (variantCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(variantCount));
        }
        int cells = GridCells * GridCells;
        var order = new int[cells];
        for (int i = 0; i < cells; i++)
        {
            order[i] = i % variantCount;
        }
        for (int i = cells - 1; i > 0; i--)
        {
            int j = (int)(Hashing.Hash64(ArrangementSeed, (ulong)i) % (ulong)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }

    private static void Blit(byte[] sheet, int sheetWidth, int originX, IReadOnlyList<byte[]> tiles, int[] arrangement, int size)
    {
        for (int cell = 0; cell < arrangement.Length; cell++)
        {
            byte[] tile = tiles[arrangement[cell]];
            int cx = originX + (cell % GridCells) * size;
            int cy = (cell / GridCells) * size;
            for (int y = 0; y < size; y++)
            {
                Array.Copy(tile, y * size * 3, sheet, ((cy + y) * sheetWidth + cx) * 3, size * 3);
            }
        }
    }
}
