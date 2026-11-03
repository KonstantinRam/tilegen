namespace TileGen.Core;

/// <summary>
/// One numeric parameter of a tile type, addressed by its path, for example base.structure.scale. The absolute bounds
/// are the slider range in the GUI and the range a locked parameter receives when it is unlocked.
/// </summary>
public readonly record struct ParameterDescriptor(string Path, string Label, Param Param, bool IsInteger, double AbsoluteMin, double AbsoluteMax);

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
        switch (material.Structure)
        {
            case FieldStructure f:
                yield return new ParameterDescriptor(StructurePath(prefix, "scale"), "Scale", f.Scale, false, MinScale, MaxScale);
                yield return new ParameterDescriptor(StructurePath(prefix, "octaves"), "Octaves", f.Octaves, true, 1, 8);
                yield return new ParameterDescriptor(StructurePath(prefix, "gain"), "Gain", f.Gain, false, 0.0, 1.0);
                yield return new ParameterDescriptor(StructurePath(prefix, "warp"), "Warp", f.Warp, false, 0.0, 1.0);
                break;
            case CellsStructure c:
                yield return new ParameterDescriptor(StructurePath(prefix, "count"), "Cell count", c.Count, true, 1, 64);
                yield return new ParameterDescriptor(StructurePath(prefix, "jitter"), "Cell jitter", c.Jitter, false, 0.0, 1.0);
                yield return new ParameterDescriptor(StructurePath(prefix, "edgeWidth"), "Edge width", c.EdgeWidth, false, 0.0, 0.1);
                yield return new ParameterDescriptor(StructurePath(prefix, "edgeHeight"), "Edge height", c.EdgeHeight, false, 0.0, 1.0);
                break;
            case PatternStructure p:
                yield return new ParameterDescriptor(StructurePath(prefix, "rows"), "Rows", p.Rows, true, 1, 32);
                yield return new ParameterDescriptor(StructurePath(prefix, "columns"), "Columns", p.Columns, true, 1, 32);
                yield return new ParameterDescriptor(StructurePath(prefix, "rowOffset"), "Row offset", p.RowOffset, false, 0.0, 1.0);
                yield return new ParameterDescriptor(StructurePath(prefix, "mortarWidth"), "Mortar width", p.MortarWidth, false, 0.0, 0.1);
                yield return new ParameterDescriptor(StructurePath(prefix, "mortarHeight"), "Mortar height", p.MortarHeight, false, 0.0, 1.0);
                break;
            default:
                throw new NotSupportedException($"Unknown structure family '{material.Structure.Family}'.");
        }
        yield return new ParameterDescriptor(DetailPath(prefix, "scale"), "Detail scale", material.Detail.Scale, false, MinScale, MaxScale);
        yield return new ParameterDescriptor(DetailPath(prefix, "octaves"), "Detail octaves", material.Detail.Octaves, true, 1, 8);
        yield return new ParameterDescriptor(DetailPath(prefix, "strength"), "Detail strength", material.Detail.Strength, false, 0.0, 1.0);
        yield return new ParameterDescriptor(MaterialPath(prefix, "tint"), "Tint", material.Tint, false, 0.0, 1.0);
        yield return new ParameterDescriptor(MaterialPath(prefix, "contrast"), "Contrast", material.Contrast, false, 0.0, 3.0);
        yield return new ParameterDescriptor(MaterialPath(prefix, "edgeDarkening"), "Edge darkening", material.EdgeDarkening, false, 0.0, 1.0);
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
                _ => throw new ArgumentException($"Unknown parameter path '{path}'."),
            };
        }
        if (parts.Length != 3)
        {
            throw new ArgumentException($"Unknown parameter path '{path}'.");
        }
        if (parts[1] == "detail")
        {
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
        Structure structure = material.Structure switch
        {
            FieldStructure f => parts[2] switch
            {
                "scale" => f with { Scale = value },
                "octaves" => f with { Octaves = value },
                "gain" => f with { Gain = value },
                "warp" => f with { Warp = value },
                _ => throw new ArgumentException($"The field family has no parameter '{path}'."),
            },
            CellsStructure c => parts[2] switch
            {
                "count" => c with { Count = value },
                "jitter" => c with { Jitter = value },
                "edgeWidth" => c with { EdgeWidth = value },
                "edgeHeight" => c with { EdgeHeight = value },
                _ => throw new ArgumentException($"The cells family has no parameter '{path}'."),
            },
            PatternStructure p => parts[2] switch
            {
                "rows" => p with { Rows = value },
                "columns" => p with { Columns = value },
                "rowOffset" => p with { RowOffset = value },
                "mortarWidth" => p with { MortarWidth = value },
                "mortarHeight" => p with { MortarHeight = value },
                _ => throw new ArgumentException($"The pattern family has no parameter '{path}'."),
            },
            _ => throw new NotSupportedException($"Unknown structure family '{material.Structure.Family}'."),
        };
        return material with { Structure = structure };
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
