namespace TileGen.Core;

/// <summary>One variant of a set: its index from 1, its seed and the resolved parameters of both tile types.</summary>
public sealed record Variant(int Index, ulong Seed, ParameterSet Floor, ParameterSet Wall);

/// <summary>
/// Derives variant seeds and jittered parameter sets from a base seed. Hashing the index keeps the first variants
/// stable when the count grows, and hashing the parameter path keeps every draw stable when a parameter is added.
/// </summary>
public static class Variants
{
    /// <summary>Variant indices count from 1, because the exported files count from 01.</summary>
    public static ulong SeedFor(ulong baseSeed, int index)
    {
        if (index < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Variant indices start at 1.");
        }
        return Hashing.Hash64(baseSeed, (ulong)index);
    }

    /// <summary>The variants of a set in order. Variant i depends only on the base seed and i, so a longer list starts with the shorter one.</summary>
    public static IReadOnlyList<Variant> Resolve(Preset preset, ulong baseSeed, int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "A set has at least one variant.");
        }
        var list = new List<Variant>(count);
        for (int index = 1; index <= count; index++)
        {
            ulong seed = SeedFor(baseSeed, index);
            list.Add(new Variant(
                index,
                seed,
                Jitter(preset.Floor, TileKind.Floor, seed, preset.Jitter),
                Jitter(preset.Wall, TileKind.Wall, seed, preset.Jitter)));
        }
        return list;
    }

    /// <summary>
    /// Draws every ranged parameter uniformly from its range and leaves the rest alone. This is the "randomize
    /// parameters" action, so it changes the authored values, and the variants are derived from the result as usual.
    /// </summary>
    public static Preset Randomize(Preset preset, ulong seed)
    {
        return preset with
        {
            Floor = RandomizeType(preset.Floor, Hashing.Hash64(seed, 0x52414E44UL + (ulong)TileKind.Floor)),
            Wall = RandomizeType(preset.Wall, Hashing.Hash64(seed, 0x52414E44UL + (ulong)TileKind.Wall)),
        };
    }

    private static TileType RandomizeType(TileType type, ulong seed)
    {
        foreach (var d in TileTypeParameters.Enumerate(type).ToList())
        {
            if (!d.Param.HasRange)
            {
                continue;
            }
            double min = d.Param.Min!.Value;
            double max = d.Param.Max!.Value;
            double value = min + (max - min) * Hashing.Unit(Hashing.Hash64(seed, Hashing.String(d.Path)));
            if (d.IsInteger)
            {
                value = Math.Clamp(Math.Round(value), min, max);
            }
            type = TileTypeParameters.With(type, d.Path, new Param(value, min, max));
        }
        return type;
    }

    /// <summary>
    /// Moves every ranged parameter by up to jitter times its range, drawn from the variant seed, and leaves
    /// unranged parameters at their authored value. Integer parameters are rounded so that the stored set reads as integers.
    /// </summary>
    public static ParameterSet Jitter(TileType type, TileKind kind, ulong variantSeed, double jitter)
    {
        if (jitter < 0.0 || jitter > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(jitter), "Jitter is a fraction from 0 to 1.");
        }
        ulong kindSeed = Hashing.Hash64(variantSeed, 0x4B494E44UL + (ulong)kind);
        var values = new List<KeyValuePair<string, double>>();
        foreach (var d in TileTypeParameters.Enumerate(type))
        {
            double value = d.Param.Value;
            if (d.Param.HasRange && jitter > 0.0)
            {
                double min = d.Param.Min!.Value;
                double max = d.Param.Max!.Value;
                double u = 2.0 * Hashing.Unit(Hashing.Hash64(kindSeed, Hashing.String(d.Path))) - 1.0;
                value = Math.Clamp(value + u * jitter * (max - min), min, max);
                if (d.IsInteger)
                {
                    value = Math.Round(value);
                }
            }
            values.Add(new KeyValuePair<string, double>(d.Path, value));
        }
        return new ParameterSet(values);
    }
}
