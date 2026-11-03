namespace TileGen.Core;

/// <summary>
/// Integer hashing is the only source of randomness in the generator, because integer arithmetic gives the same
/// bits on every machine while System.Random and floating point hashes do not (docs/DECISIONS.md ADR-008).
/// </summary>
public static class Hashing
{
    private const ulong Golden = 0x9E3779B97F4A7C15UL;
    private const ulong LatticeX = 0xD6E8FEB86659FD93UL;
    private const ulong LatticeY = 0xA0761D6478BD642FUL;

    /// <summary>The splitmix64 finalizer, which spreads every input bit over the whole output.</summary>
    public static ulong Mix(ulong x)
    {
        x ^= x >> 30;
        x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27;
        x *= 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return x;
    }

    /// <summary>Derives a child seed from a parent seed and an index. Variant i uses Hash64(baseSeed, i) by contract.</summary>
    public static ulong Hash64(ulong seed, ulong value)
    {
        return Mix(Mix(seed) ^ (value * Golden + 0x632BE59BD9B4E019UL));
    }

    /// <summary>Hashes integer lattice coordinates and a seed. Callers pass coordinates already taken modulo the cell count.</summary>
    public static ulong Lattice(ulong seed, int ix, int iy)
    {
        ulong coords = (ulong)(uint)ix * LatticeX + (ulong)(uint)iy * LatticeY;
        return Mix(seed ^ Mix(coords));
    }

    /// <summary>FNV-1a over the UTF-16 code units of a string, used to give every parameter path its own jitter draw.</summary>
    public static ulong String(string text)
    {
        ulong h = 0xCBF29CE484222325UL;
        foreach (char c in text)
        {
            h ^= c;
            h *= 0x100000001B3UL;
        }
        return h;
    }

    /// <summary>FNV-1a over raw bytes, used to turn an image file into a base seed.</summary>
    public static ulong Bytes(ReadOnlySpan<byte> data)
    {
        ulong h = 0xCBF29CE484222325UL;
        foreach (byte b in data)
        {
            h ^= b;
            h *= 0x100000001B3UL;
        }
        return h;
    }

    /// <summary>Maps a hash to a double in the range 0 inclusive to 1 exclusive using its top 53 bits, which is exact.</summary>
    public static double Unit(ulong hash)
    {
        return (hash >> 11) * (1.0 / 9007199254740992.0);
    }
}
