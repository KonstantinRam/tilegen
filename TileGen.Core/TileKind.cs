namespace TileGen.Core;

/// <summary>The two tile types a set contains. The numeric values salt the per-tile seed, so they must not change.</summary>
public enum TileKind
{
    Floor = 0,
    Wall = 1,
}
