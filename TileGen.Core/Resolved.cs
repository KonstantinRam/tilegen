namespace TileGen.Core;

/// <summary>A tile type with every parameter resolved to a plain number, which is what the rasterizer reads.</summary>
internal sealed record ResolvedTileType(
    ResolvedMaterial Base,
    ResolvedMaterial Garbage,
    double GarbageScale,
    double GarbageSoftness);

internal sealed record ResolvedMaterial(
    ResolvedStructure Structure,
    IReadOnlyList<RampStop> Ramp,
    double DetailScale,
    int DetailOctaves,
    double DetailStrength,
    double Tint,
    double Contrast,
    double EdgeDarkening);

internal abstract record ResolvedStructure;

/// <summary>The field family with its scale already converted to an integer cell count, so the lattice wraps.</summary>
internal sealed record ResolvedField(int Cells, int Octaves, double Gain, double Warp, bool Ridged) : ResolvedStructure;

/// <summary>The cells family. EdgeWidth is the full width of the border between two cells, as a fraction of the tile.</summary>
internal sealed record ResolvedCells(int Count, double Jitter, double EdgeWidth, double EdgeHeight) : ResolvedStructure;

/// <summary>The pattern family. MortarWidth is the full width of the gap between two cells, as a fraction of the tile.</summary>
internal sealed record ResolvedPattern(int Rows, int Columns, double RowOffset, double MortarWidth, double MortarHeight) : ResolvedStructure;

/// <summary>Turns an authored tile type plus a parameter set into resolved numbers. Runs once per tile, so rounding here is allowed.</summary>
internal static class TileResolver
{
    public const int MaxOctaves = 8;

    public static ResolvedTileType Resolve(TileType type, ParameterSet set)
    {
        return new ResolvedTileType(
            ResolveMaterial(TileTypeParameters.BasePrefix, type.Base, set),
            ResolveMaterial(TileTypeParameters.GarbagePrefix, type.Garbage, set),
            Positive(set.Get(TileTypeParameters.GarbageScale, type.GarbageScale.Value), TileTypeParameters.GarbageScale),
            Math.Max(0.0, set.Get(TileTypeParameters.GarbageSoftness, type.GarbageSoftness.Value)));
    }

    /// <summary>Converts a scale given as a fraction of the tile to an integer number of lattice cells per tile.</summary>
    public static int CellsFromScale(double scale)
    {
        if (!(scale > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "A scale must be a positive fraction of the tile.");
        }
        return Math.Max(1, (int)Math.Round(1.0 / scale));
    }

    public static int ClampOctaves(double octaves)
    {
        return Math.Clamp((int)Math.Round(octaves), 1, MaxOctaves);
    }

    private static int AtLeastOne(double count)
    {
        return Math.Max(1, (int)Math.Round(count));
    }

    private static ResolvedMaterial ResolveMaterial(string prefix, Material material, ParameterSet set)
    {
        double S(string field, Param p) => set.Get(TileTypeParameters.StructurePath(prefix, field), p.Value);
        double D(string field, Param p) => set.Get(TileTypeParameters.DetailPath(prefix, field), p.Value);
        double M(string field, Param p) => set.Get(TileTypeParameters.MaterialPath(prefix, field), p.Value);

        ResolvedStructure structure = material.Structure switch
        {
            FieldStructure f => new ResolvedField(
                CellsFromScale(S("scale", f.Scale)),
                ClampOctaves(S("octaves", f.Octaves)),
                Math.Clamp(S("gain", f.Gain), 0.0, 1.0),
                Math.Clamp(S("warp", f.Warp), 0.0, 1.0),
                f.Ridged),
            CellsStructure c => new ResolvedCells(
                AtLeastOne(S("count", c.Count)),
                Math.Clamp(S("jitter", c.Jitter), 0.0, 1.0),
                Math.Max(0.0, S("edgeWidth", c.EdgeWidth)),
                Math.Clamp(S("edgeHeight", c.EdgeHeight), 0.0, 1.0)),
            PatternStructure p => new ResolvedPattern(
                AtLeastOne(S("rows", p.Rows)),
                AtLeastOne(S("columns", p.Columns)),
                Math.Clamp(S("rowOffset", p.RowOffset), 0.0, 1.0),
                Math.Max(0.0, S("mortarWidth", p.MortarWidth)),
                Math.Clamp(S("mortarHeight", p.MortarHeight), 0.0, 1.0)),
            _ => throw new NotSupportedException($"Unknown structure family '{material.Structure.Family}'."),
        };

        Ramp.Validate(material.Ramp);
        return new ResolvedMaterial(
            structure,
            material.Ramp,
            Positive(D("scale", material.Detail.Scale), TileTypeParameters.DetailPath(prefix, "scale")),
            ClampOctaves(D("octaves", material.Detail.Octaves)),
            Math.Max(0.0, D("strength", material.Detail.Strength)),
            Math.Clamp(M("tint", material.Tint), 0.0, 1.0),
            Math.Max(0.0, M("contrast", material.Contrast)),
            Math.Clamp(M("edgeDarkening", material.EdgeDarkening), 0.0, 1.0));
    }

    private static double Positive(double value, string path)
    {
        if (!(value > 0.0))
        {
            throw new ArgumentOutOfRangeException(path, $"{path} must be a positive fraction of the tile, got {value}.");
        }
        return value;
    }
}
