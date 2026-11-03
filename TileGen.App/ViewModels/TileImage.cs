using TileGen.Core;

namespace TileGen.App.ViewModels;

/// <summary>One rendered tile as the generator returns it. The view turns it into a bitmap, so the view models stay free of UI types.</summary>
public sealed record TileImage(byte[] Rgb, int Size);

/// <summary>Which variant sits in which cell of the room preview. A fixed seed keeps the arrangement stable while parameters change.</summary>
public static class RoomArrangement
{
    private const ulong Seed = 0x524F4F4DUL;

    public static int VariantAt(int row, int column, int variantCount)
    {
        if (variantCount < 1)
        {
            return 0;
        }
        return (int)(Hashing.Hash64(Seed, (ulong)row * 4096UL + (ulong)column) % (ulong)variantCount);
    }
}
