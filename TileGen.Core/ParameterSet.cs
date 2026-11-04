namespace TileGen.Core;

/// <summary>
/// One numeric parameter of a tile type, addressed by its path, for example base.structure.scale. The absolute bounds
/// are the slider range in the GUI and the range a locked parameter receives when it is unlocked. CanRange is false
/// for the few parameters that must stay equal across the variants of a set, which the GUI shows without a lock.
/// </summary>
public readonly record struct ParameterDescriptor(string Path, string Label, Param Param, bool IsInteger, double AbsoluteMin, double AbsoluteMax, bool CanRange = true);

/// <summary>
/// Lists every numeric parameter of a tile type with its path. The paths are the keys of a resolved parameter set,
/// the keys in settings.json and the rows of the GUI, so this list is the one place that knows which parameters exist.
/// </summary>
public static class TileTypeParameters
{
    public const string GarbageScale = "garbageScale";
    public const string GarbageSoftness = "garbageSoftness";
    public const string BasePrefix = "base";
    public const string GarbagePrefix = "garbage";

    /// <summary>The field of the columns family that holds the structure behind the columns, whose parameters nest under it.</summary>
    public const string BehindField = "behind";

    private const double MinScale = 1.0 / 64.0;
    private const double MaxScale = 0.5;

    public static IEnumerable<ParameterDescriptor> Enumerate(TileType type)
    {
        yield return new ParameterDescriptor(GarbageScale, "Mask scale", type.GarbageScale, false, MinScale, MaxScale);
        yield return new ParameterDescriptor(GarbageSoftness, "Mask softness", type.GarbageSoftness, false, 0.0, 0.5);
        foreach (var d in EnumerateMaterial(BasePrefix, type.Base))
        {
            yield return d;
        }
        foreach (var d in EnumerateMaterial(GarbagePrefix, type.Garbage))
        {
            yield return d;
        }
    }

    /// <summary>The parameters of one material only, which is what one tab of the GUI shows.</summary>
    public static IEnumerable<ParameterDescriptor> EnumerateMaterial(string prefix, Material material)
    {
        foreach (var d in EnumerateStructure(prefix + ".structure", "", material.Structure))
        {
            yield return d;
        }
        yield return new ParameterDescriptor(DetailPath(prefix, "scale"), "Detail scale", material.Detail.Scale, false, MinScale, MaxScale);
        yield return new ParameterDescriptor(DetailPath(prefix, "octaves"), "Detail octaves", material.Detail.Octaves, true, 1, 8);
        yield return new ParameterDescriptor(DetailPath(prefix, "strength"), "Detail strength", material.Detail.Strength, false, 0.0, 1.0);
        yield return new ParameterDescriptor(MaterialPath(prefix, "tint"), "Tint", material.Tint, false, 0.0, 1.0);
        yield return new ParameterDescriptor(MaterialPath(prefix, "contrast"), "Contrast", material.Contrast, false, 0.0, 3.0);
        yield return new ParameterDescriptor(MaterialPath(prefix, "edgeDarkening"), "Edge darkening", material.EdgeDarkening, false, 0.0, 1.0);
        // The haze of a transparent garbage reaches the tile edges, so every variant of a set keeps the same transparency.
        yield return new ParameterDescriptor(MaterialPath(prefix, "transparency"), "Transparency", material.Transparency, false, 0.0, 1.0, false);
    }

    /// <summary>
    /// The parameters of one structure under a path such as base.structure. The structure behind columns nests under
    /// base.structure.behind with its labels prefixed, so a nested parameter is one more row and nothing else.
    /// </summary>
    public static IEnumerable<ParameterDescriptor> EnumerateStructure(string structurePath, string labelPrefix, Structure structure)
    {
        ParameterDescriptor D(string field, string label, Param param, bool isInteger, double min, double max, bool canRange = true)
        {
            return new ParameterDescriptor(structurePath + "." + field, labelPrefix + label, param, isInteger, min, max, canRange);
        }

        switch (structure)
        {
            case FieldStructure f:
                yield return D("scale", "Scale", f.Scale, false, MinScale, MaxScale);
                yield return D("octaves", "Octaves", f.Octaves, true, 1, 8);
                yield return D("gain", "Gain", f.Gain, false, 0.0, 1.0);
                yield return D("warp", "Warp", f.Warp, false, 0.0, 1.0);
                break;
            case CellsStructure c:
                yield return D("count", "Cell count", c.Count, true, 1, 64);
                yield return D("jitter", "Cell jitter", c.Jitter, false, 0.0, 1.0);
                yield return D("edgeWidth", "Edge width", c.EdgeWidth, false, 0.0, 0.1);
                yield return D("edgeHeight", "Edge height", c.EdgeHeight, false, 0.0, 1.0);
                break;
            case PatternStructure p:
                yield return D("rows", "Rows", p.Rows, true, 1, 32);
                yield return D("columns", "Columns", p.Columns, true, 1, 32);
                yield return D("rowOffset", "Row offset", p.RowOffset, false, 0.0, 1.0);
                yield return D("mortarWidth", "Mortar width", p.MortarWidth, false, 0.0, 0.1);
                yield return D("mortarHeight", "Mortar height", p.MortarHeight, false, 0.0, 1.0);
                break;
            case BlocksStructure b:
                // The first four shape the layout that every variant of a set shares along the tile edges, so they never range.
                yield return D("rows", "Rows", b.Rows, true, 1, 32, false);
                yield return D("columns", "Columns", b.Columns, true, 1, 32, false);
                yield return D("sizeVariation", "Size variation", b.SizeVariation, false, 0.0, 1.0, false);
                yield return D("tallChance", "Tall chance", b.TallChance, false, 0.0, 1.0, false);
                yield return D("mortarWidth", "Mortar width", b.MortarWidth, false, 0.0, 0.1);
                yield return D("mortarHeight", "Mortar height", b.MortarHeight, false, 0.0, 1.0);
                yield return D("cornerRadius", "Corner radius", b.CornerRadius, false, 0.0, 0.1);
                break;
            case TimberStructure t:
                // The frame is identical in every variant of a set, so only the brace chance, which draws per panel, may range.
                yield return D("rows", "Rows", t.Rows, true, 1, 8, false);
                yield return D("columns", "Columns", t.Columns, true, 1, 8, false);
                yield return D("beamWidth", "Beam width", t.BeamWidth, false, 0.0, 0.25, false);
                yield return D("braceChance", "Brace chance", t.BraceChance, false, 0.0, 1.0);
                yield return D("grain", "Grain", t.Grain, false, 0.0, 1.0, false);
                yield return D("pegSize", "Peg size", t.PegSize, false, 0.0, 0.05, false);
                yield return D("plankWidth", "Plank width", t.PlankWidth, false, MinScale, MaxScale, false);
                break;
            case ScatterStructure sc:
                // The lattice, the jitter, the layer count, the ground and the warp are shared by every variant of a set, so they
                // never range. Density, size and size variation may range, because the stones along the tile edges use the authored values.
                yield return D("count", "Count", sc.Count, true, 1, 32, false);
                yield return D("density", "Density", sc.Density, false, 0.0, 1.0);
                yield return D("size", "Size", sc.Size, false, 0.05, 1.0);
                yield return D("sizeVariation", "Size variation", sc.SizeVariation, false, 0.0, 1.0);
                yield return D("jitter", "Jitter", sc.Jitter, false, 0.0, 1.0, false);
                yield return D("layers", "Layers", sc.Layers, true, 1, 3, false);
                yield return D("groundHeight", "Ground height", sc.GroundHeight, false, 0.0, 1.0, false);
                yield return D("warp", "Warp", sc.Warp, false, 0.0, 1.0, false);
                yield return D("stars", "Stars", sc.Stars, false, 0.0, 1.0, false);
                break;
            case ColumnsStructure co:
                // A column runs from the top edge to the bottom edge of the tile, so every column is the same in every variant.
                yield return D("count", "Columns", co.Count, true, 1, 8, false);
                yield return D("width", "Column width", co.Width, false, 0.02, 0.5, false);
                yield return D("capital", "Capital", co.Capital, false, 0.0, 0.4, false);
                yield return D("base", "Base", co.Base, false, 0.0, 0.4, false);
                yield return D("flare", "Flare", co.Flare, false, 0.0, 1.0, false);
                yield return D("flutes", "Flutes", co.Flutes, true, 0, 24, false);
                foreach (var d in EnumerateStructure(structurePath + "." + BehindField, labelPrefix + "Behind: ", co.Behind))
                {
                    yield return d;
                }
                break;
            case CrystalStructure cr:
                // The lattice, the shard count and the facet shading are shared by every variant of a set, so they never range.
                // Density, size, aspect and size variation may range, because the clusters along the edges use the authored values.
                yield return D("count", "Count", cr.Count, true, 1, 32, false);
                yield return D("density", "Density", cr.Density, false, 0.0, 1.0);
                yield return D("shards", "Shards", cr.Shards, true, 1, 6, false);
                yield return D("size", "Size", cr.Size, false, 0.1, 1.0);
                yield return D("aspect", "Aspect", cr.Aspect, false, 0.05, 1.0);
                yield return D("sizeVariation", "Size variation", cr.SizeVariation, false, 0.0, 1.0);
                yield return D("facet", "Facet", cr.Facet, false, 0.0, 1.0, false);
                break;
            default:
                throw new NotSupportedException($"Unknown structure family '{structure.Family}'.");
        }
    }

    public static string StructurePath(string prefix, string field) => prefix + ".structure." + field;
    public static string DetailPath(string prefix, string field) => prefix + ".detail." + field;
    public static string MaterialPath(string prefix, string field) => prefix + "." + field;

    /// <summary>Returns a copy of the tile type with one parameter replaced, which is how the GUI edits the immutable model.</summary>
    public static TileType With(TileType type, string path, Param value)
    {
        if (path == GarbageScale)
        {
            return type with { GarbageScale = value };
        }
        if (path == GarbageSoftness)
        {
            return type with { GarbageSoftness = value };
        }

        string[] parts = path.Split('.');
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Unknown parameter path '{path}'.");
        }
        Material material = parts[0] switch
        {
            BasePrefix => type.Base,
            GarbagePrefix => type.Garbage,
            _ => throw new ArgumentException($"Unknown parameter path '{path}'."),
        };
        Material changed = WithMaterial(material, parts, path, value);
        return parts[0] == BasePrefix ? type with { Base = changed } : type with { Garbage = changed };
    }

    private static Material WithMaterial(Material material, string[] parts, string path, Param value)
    {
        if (parts.Length == 2)
        {
            return parts[1] switch
            {
                "tint" => material with { Tint = value },
                "contrast" => material with { Contrast = value },
                "edgeDarkening" => material with { EdgeDarkening = value },
                "transparency" => material with { Transparency = value },
                _ => throw new ArgumentException($"Unknown parameter path '{path}'."),
            };
        }
        if (parts[1] == "detail")
        {
            if (parts.Length != 3)
            {
                throw new ArgumentException($"Unknown parameter path '{path}'.");
            }
            return material with
            {
                Detail = parts[2] switch
                {
                    "scale" => material.Detail with { Scale = value },
                    "octaves" => material.Detail with { Octaves = value },
                    "strength" => material.Detail with { Strength = value },
                    _ => throw new ArgumentException($"Unknown parameter path '{path}'."),
                },
            };
        }
        if (parts[1] != "structure")
        {
            throw new ArgumentException($"Unknown parameter path '{path}'.");
        }
        return material with { Structure = WithStructure(material.Structure, parts, 2, path, value) };
    }

    /// <summary>Replaces one field of a structure, descending into the structure behind columns when the path says behind.</summary>
    private static Structure WithStructure(Structure structure, string[] parts, int index, string path, Param value)
    {
        if (index >= parts.Length)
        {
            throw new ArgumentException($"Unknown parameter path '{path}'.");
        }
        string field = parts[index];
        if (structure is ColumnsStructure columns && field == BehindField)
        {
            return columns with { Behind = WithStructure(columns.Behind, parts, index + 1, path, value) };
        }
        if (index != parts.Length - 1)
        {
            throw new ArgumentException($"Unknown parameter path '{path}'.");
        }
        return structure switch
        {
            FieldStructure f => field switch
            {
                "scale" => f with { Scale = value },
                "octaves" => f with { Octaves = value },
                "gain" => f with { Gain = value },
                "warp" => f with { Warp = value },
                _ => throw new ArgumentException($"The field family has no parameter '{path}'."),
            },
            CellsStructure c => field switch
            {
                "count" => c with { Count = value },
                "jitter" => c with { Jitter = value },
                "edgeWidth" => c with { EdgeWidth = value },
                "edgeHeight" => c with { EdgeHeight = value },
                _ => throw new ArgumentException($"The cells family has no parameter '{path}'."),
            },
            PatternStructure p => field switch
            {
                "rows" => p with { Rows = value },
                "columns" => p with { Columns = value },
                "rowOffset" => p with { RowOffset = value },
                "mortarWidth" => p with { MortarWidth = value },
                "mortarHeight" => p with { MortarHeight = value },
                _ => throw new ArgumentException($"The pattern family has no parameter '{path}'."),
            },
            BlocksStructure b => field switch
            {
                "rows" => b with { Rows = value },
                "columns" => b with { Columns = value },
                "sizeVariation" => b with { SizeVariation = value },
                "tallChance" => b with { TallChance = value },
                "mortarWidth" => b with { MortarWidth = value },
                "mortarHeight" => b with { MortarHeight = value },
                "cornerRadius" => b with { CornerRadius = value },
                _ => throw new ArgumentException($"The blocks family has no parameter '{path}'."),
            },
            TimberStructure t => field switch
            {
                "rows" => t with { Rows = value },
                "columns" => t with { Columns = value },
                "beamWidth" => t with { BeamWidth = value },
                "braceChance" => t with { BraceChance = value },
                "grain" => t with { Grain = value },
                "pegSize" => t with { PegSize = value },
                "plankWidth" => t with { PlankWidth = value },
                _ => throw new ArgumentException($"The timber family has no parameter '{path}'."),
            },
            ScatterStructure sc => field switch
            {
                "count" => sc with { Count = value },
                "density" => sc with { Density = value },
                "size" => sc with { Size = value },
                "sizeVariation" => sc with { SizeVariation = value },
                "jitter" => sc with { Jitter = value },
                "layers" => sc with { Layers = value },
                "groundHeight" => sc with { GroundHeight = value },
                "warp" => sc with { Warp = value },
                "stars" => sc with { Stars = value },
                _ => throw new ArgumentException($"The scatter family has no parameter '{path}'."),
            },
            ColumnsStructure co => field switch
            {
                "count" => co with { Count = value },
                "width" => co with { Width = value },
                "capital" => co with { Capital = value },
                "base" => co with { Base = value },
                "flare" => co with { Flare = value },
                "flutes" => co with { Flutes = value },
                _ => throw new ArgumentException($"The columns family has no parameter '{path}'."),
            },
            CrystalStructure cr => field switch
            {
                "count" => cr with { Count = value },
                "density" => cr with { Density = value },
                "shards" => cr with { Shards = value },
                "size" => cr with { Size = value },
                "aspect" => cr with { Aspect = value },
                "sizeVariation" => cr with { SizeVariation = value },
                "facet" => cr with { Facet = value },
                _ => throw new ArgumentException($"The crystal family has no parameter '{path}'."),
            },
            _ => throw new NotSupportedException($"Unknown structure family '{structure.Family}'."),
        };
    }
}

/// <summary>
/// The resolved numeric parameters of one tile type for one variant, keyed by path. A variant stores this set in
/// settings.json, so a variant can be reproduced without running the jitter again.
/// </summary>
public sealed class ParameterSet
{
    private readonly SortedDictionary<string, double> _values;

    public static readonly ParameterSet Empty = new(Array.Empty<KeyValuePair<string, double>>());

    public ParameterSet(IEnumerable<KeyValuePair<string, double>> values)
    {
        _values = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            _values[pair.Key] = pair.Value;
        }
    }

    public IReadOnlyDictionary<string, double> Values => _values;

    /// <summary>Returns the resolved value, or the authored value when the set has no entry, which is what an unranged parameter resolves to anyway.</summary>
    public double Get(string path, double fallback)
    {
        return _values.TryGetValue(path, out double v) ? v : fallback;
    }

    /// <summary>Every parameter at its authored value, which is what variant rendering without jitter uses.</summary>
    public static ParameterSet Defaults(TileType type)
    {
        return new ParameterSet(TileTypeParameters.Enumerate(type)
            .Select(d => new KeyValuePair<string, double>(d.Path, d.Param.Value)));
    }
}
