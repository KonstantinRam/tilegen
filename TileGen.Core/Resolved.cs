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
    double EdgeDarkening,
    double Transparency,
    bool CellColors);

internal abstract record ResolvedStructure;

/// <summary>The field family with its scale already converted to an integer cell count, so the lattice wraps.</summary>
internal sealed record ResolvedField(int Cells, int Octaves, double Gain, double Warp, bool Ridged) : ResolvedStructure;

/// <summary>The cells family. EdgeWidth is the full width of the border between two cells, as a fraction of the tile.</summary>
internal sealed record ResolvedCells(int Count, double Jitter, double EdgeWidth, double EdgeHeight) : ResolvedStructure;

/// <summary>The pattern family. MortarWidth is the full width of the gap between two cells, as a fraction of the tile.</summary>
internal sealed record ResolvedPattern(int Rows, int Columns, double RowOffset, double MortarWidth, double MortarHeight) : ResolvedStructure;

/// <summary>The blocks family. Columns is the average block count per course, and CornerRadius is a fraction of the tile.</summary>
internal sealed record ResolvedBlocks(int Rows, int Columns, double SizeVariation, double TallChance, double MortarWidth, double MortarHeight, double CornerRadius) : ResolvedStructure;

/// <summary>The timber family. BeamWidth, PegSize and PlankWidth are fractions of the tile, and BeamWidth 0 means no frame.</summary>
internal sealed record ResolvedTimber(int Rows, int Columns, double BeamWidth, double BraceChance, double Grain, double PegSize, bool Planks, double PlankWidth) : ResolvedStructure;

/// <summary>
/// The scatter family. Size is the stone radius in lattice cells. The Shared values are the authored ones, which the
/// stones along the tile edges use so that every variant of a set agrees there.
/// </summary>
internal sealed record ResolvedScatter(
    int Count,
    double Density,
    double Size,
    double SizeVariation,
    double Jitter,
    int Layers,
    double GroundHeight,
    double Warp,
    double Stars,
    double SharedDensity,
    double SharedSize,
    double SharedSizeVariation) : ResolvedStructure;

/// <summary>
/// The crystal family. Size is the shard length in lattice cells and Aspect the width as a share of the length. The
/// Shared values are the authored ones, which the clusters along the tile edges use so that every variant of a set agrees there.
/// </summary>
internal sealed record ResolvedCrystal(
    int Count,
    double Density,
    int Shards,
    double Size,
    double Aspect,
    double SizeVariation,
    double Facet,
    double SharedDensity,
    double SharedSize,
    double SharedAspect,
    double SharedSizeVariation) : ResolvedStructure;

/// <summary>The columns family. Width, Capital and Base are fractions of the tile, and Behind is the resolved structure between the columns.</summary>
internal sealed record ResolvedColumns(int Count, double Width, double Capital, double Base, double Flare, int Flutes, ResolvedStructure Behind) : ResolvedStructure;

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
        double D(string field, Param p) => set.Get(TileTypeParameters.DetailPath(prefix, field), p.Value);
        double M(string field, Param p) => set.Get(TileTypeParameters.MaterialPath(prefix, field), p.Value);

        ResolvedStructure structure = ResolveStructure(prefix + ".structure", material.Structure, set);

        Ramp.Validate(material.Ramp);
        return new ResolvedMaterial(
            structure,
            material.Ramp,
            Positive(D("scale", material.Detail.Scale), TileTypeParameters.DetailPath(prefix, "scale")),
            ClampOctaves(D("octaves", material.Detail.Octaves)),
            Math.Max(0.0, D("strength", material.Detail.Strength)),
            Math.Clamp(M("tint", material.Tint), 0.0, 1.0),
            Math.Max(0.0, M("contrast", material.Contrast)),
            Math.Clamp(M("edgeDarkening", material.EdgeDarkening), 0.0, 1.0),
            Math.Clamp(M("transparency", material.Transparency), 0.0, 1.0),
            material.CellColors);
    }

    /// <summary>Resolves one structure under its path, and the structure behind columns under the path behind, so the keys match the descriptors.</summary>
    private static ResolvedStructure ResolveStructure(string structurePath, Structure structure, ParameterSet set)
    {
        double S(string field, Param p) => set.Get(structurePath + "." + field, p.Value);

        return structure switch
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
            BlocksStructure b => new ResolvedBlocks(
                AtLeastOne(S("rows", b.Rows)),
                AtLeastOne(S("columns", b.Columns)),
                Math.Clamp(S("sizeVariation", b.SizeVariation), 0.0, 1.0),
                Math.Clamp(S("tallChance", b.TallChance), 0.0, 1.0),
                Math.Max(0.0, S("mortarWidth", b.MortarWidth)),
                Math.Clamp(S("mortarHeight", b.MortarHeight), 0.0, 1.0),
                Math.Max(0.0, S("cornerRadius", b.CornerRadius))),
            TimberStructure t => new ResolvedTimber(
                AtLeastOne(S("rows", t.Rows)),
                AtLeastOne(S("columns", t.Columns)),
                Math.Max(0.0, S("beamWidth", t.BeamWidth)),
                Math.Clamp(S("braceChance", t.BraceChance), 0.0, 1.0),
                Math.Clamp(S("grain", t.Grain), 0.0, 1.0),
                Math.Max(0.0, S("pegSize", t.PegSize)),
                t.Planks,
                Positive(S("plankWidth", t.PlankWidth), structurePath + ".plankWidth")),
            ScatterStructure sc => new ResolvedScatter(
                AtLeastOne(S("count", sc.Count)),
                Math.Clamp(S("density", sc.Density), 0.0, 1.0),
                Math.Clamp(S("size", sc.Size), 0.05, 1.0),
                Math.Clamp(S("sizeVariation", sc.SizeVariation), 0.0, 1.0),
                Math.Clamp(S("jitter", sc.Jitter), 0.0, 1.0),
                Math.Clamp((int)Math.Round(S("layers", sc.Layers)), 1, ScatterSampler.MaxLayers),
                Math.Clamp(S("groundHeight", sc.GroundHeight), 0.0, 1.0),
                Math.Clamp(S("warp", sc.Warp), 0.0, 1.0),
                Math.Clamp(S("stars", sc.Stars), 0.0, 1.0),
                Math.Clamp(sc.Density.Value, 0.0, 1.0),
                Math.Clamp(sc.Size.Value, 0.05, 1.0),
                Math.Clamp(sc.SizeVariation.Value, 0.0, 1.0)),
            ColumnsStructure co => new ResolvedColumns(
                AtLeastOne(S("count", co.Count)),
                Math.Max(0.02, S("width", co.Width)),
                Math.Clamp(S("capital", co.Capital), 0.0, 0.5),
                Math.Clamp(S("base", co.Base), 0.0, 0.5),
                Math.Clamp(S("flare", co.Flare), 0.0, 1.0),
                Math.Clamp((int)Math.Round(S("flutes", co.Flutes)), 0, 48),
                ResolveStructure(structurePath + "." + TileTypeParameters.BehindField, co.Behind, set)),
            CrystalStructure cr => new ResolvedCrystal(
                AtLeastOne(S("count", cr.Count)),
                Math.Clamp(S("density", cr.Density), 0.0, 1.0),
                Math.Clamp((int)Math.Round(S("shards", cr.Shards)), 1, CrystalSampler.MaxShards),
                Math.Clamp(S("size", cr.Size), 0.1, 1.0),
                Math.Clamp(S("aspect", cr.Aspect), 0.05, 0.6),
                Math.Clamp(S("sizeVariation", cr.SizeVariation), 0.0, 1.0),
                Math.Clamp(S("facet", cr.Facet), 0.0, 1.0),
                Math.Clamp(cr.Density.Value, 0.0, 1.0),
                Math.Clamp(cr.Size.Value, 0.1, 1.0),
                Math.Clamp(cr.Aspect.Value, 0.05, 0.6),
                Math.Clamp(cr.SizeVariation.Value, 0.0, 1.0)),
            _ => throw new NotSupportedException($"Unknown structure family '{structure.Family}'."),
        };
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
