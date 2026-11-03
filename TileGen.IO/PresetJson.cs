using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TileGen.Core;

namespace TileGen.IO;

/// <summary>Thrown when a preset file does not follow docs/PRESET_SCHEMA.md. The message names the JSON path, so the author can find the field.</summary>
public sealed class PresetFormatException : Exception
{
    public PresetFormatException(string path, string message)
        : base($"{path}: {message}")
    {
    }
}

/// <summary>
/// Reads and writes the preset format of docs/PRESET_SCHEMA.md. The same code serializes the preset snapshot
/// inside settings.json, so an export and a preset file agree on every field name.
/// </summary>
public static class PresetJson
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static Preset Parse(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new PresetFormatException("$", "the file is not valid JSON: " + e.Message);
        }
        if (root is not JsonObject obj)
        {
            throw new PresetFormatException("$", "the top level must be an object");
        }
        return FromNode(obj);
    }

    public static string Write(Preset preset)
    {
        return ToNode(preset).ToJsonString(WriteOptions) + Environment.NewLine;
    }

    public static Preset FromNode(JsonObject root)
    {
        const string path = "$";
        int schemaVersion = GetInt(root, path, "schemaVersion", Preset.CurrentSchemaVersion);
        if (schemaVersion != Preset.CurrentSchemaVersion)
        {
            throw new PresetFormatException(path + ".schemaVersion", $"this tool reads schema version {Preset.CurrentSchemaVersion}, and the file has version {schemaVersion}");
        }
        string id = GetString(root, path, "id");
        if (id.Length == 0 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new PresetFormatException(path + ".id", "the id must be letters, digits and underscores");
        }
        int variants = GetInt(root, path, "variants", 4);
        if (variants < 4)
        {
            throw new PresetFormatException(path + ".variants", "a set has at least 4 variants");
        }
        double jitter = GetNumber(root, path, "jitter", 0.1);
        if (jitter < 0.0 || jitter > 1.0)
        {
            throw new PresetFormatException(path + ".jitter", "jitter is a fraction from 0 to 1");
        }
        return new Preset
        {
            SchemaVersion = schemaVersion,
            Id = id,
            DisplayName = GetString(root, path, "displayName", id),
            Variants = variants,
            Jitter = jitter,
            Floor = ReadTileType(GetObject(root, path, "floor"), path + ".floor"),
            Wall = ReadTileType(GetObject(root, path, "wall"), path + ".wall"),
        };
    }

    public static JsonObject ToNode(Preset preset)
    {
        return new JsonObject
        {
            ["schemaVersion"] = preset.SchemaVersion,
            ["id"] = preset.Id,
            ["displayName"] = preset.DisplayName,
            ["variants"] = preset.Variants,
            ["jitter"] = preset.Jitter,
            ["floor"] = WriteTileType(preset.Floor),
            ["wall"] = WriteTileType(preset.Wall),
        };
    }

    private static TileType ReadTileType(JsonObject obj, string path)
    {
        double coverage = GetNumber(obj, path, "garbageCoverage", 0.0);
        if (coverage < 0.0 || coverage > 1.0)
        {
            throw new PresetFormatException(path + ".garbageCoverage", "coverage is a fraction from 0 to 1");
        }
        return new TileType
        {
            Base = ReadMaterial(GetObject(obj, path, "base"), path + ".base"),
            Garbage = ReadMaterial(GetObject(obj, path, "garbage"), path + ".garbage"),
            GarbageCoverage = coverage,
            GarbageScale = GetParam(obj, path, "garbageScale", 0.25),
            GarbageSoftness = GetParam(obj, path, "garbageSoftness", 0.05),
        };
    }

    private static JsonObject WriteTileType(TileType type)
    {
        return new JsonObject
        {
            ["base"] = WriteMaterial(type.Base),
            ["garbage"] = WriteMaterial(type.Garbage),
            ["garbageCoverage"] = type.GarbageCoverage,
            ["garbageScale"] = WriteParam(type.GarbageScale),
            ["garbageSoftness"] = WriteParam(type.GarbageSoftness),
        };
    }

    private static Material ReadMaterial(JsonObject obj, string path)
    {
        var detailNode = obj["detail"];
        Detail detail = new();
        if (detailNode is JsonObject detailObj)
        {
            string detailPath = path + ".detail";
            detail = new Detail
            {
                Scale = GetParam(detailObj, detailPath, "scale", 0.0625),
                Octaves = GetParam(detailObj, detailPath, "octaves", 3),
                Strength = GetParam(detailObj, detailPath, "strength", 0.0),
            };
        }
        else if (detailNode is not null)
        {
            throw new PresetFormatException(path + ".detail", "detail must be an object");
        }
        return new Material
        {
            Structure = ReadStructure(GetObject(obj, path, "structure"), path + ".structure"),
            Ramp = ReadRamp(obj, path),
            Detail = detail,
            Tint = GetParam(obj, path, "tint", 0.0),
            Contrast = GetParam(obj, path, "contrast", 1.0),
            EdgeDarkening = GetParam(obj, path, "edgeDarkening", 0.0),
        };
    }

    private static JsonObject WriteMaterial(Material material)
    {
        var ramp = new JsonArray();
        foreach (var stop in material.Ramp)
        {
            ramp.Add(new JsonObject { ["t"] = stop.T, ["color"] = stop.Color.ToString() });
        }
        return new JsonObject
        {
            ["structure"] = WriteStructure(material.Structure),
            ["ramp"] = ramp,
            ["detail"] = new JsonObject
            {
                ["scale"] = WriteParam(material.Detail.Scale),
                ["octaves"] = WriteParam(material.Detail.Octaves),
                ["strength"] = WriteParam(material.Detail.Strength),
            },
            ["tint"] = WriteParam(material.Tint),
            ["contrast"] = WriteParam(material.Contrast),
            ["edgeDarkening"] = WriteParam(material.EdgeDarkening),
        };
    }

    private static Structure ReadStructure(JsonObject obj, string path)
    {
        string family = GetString(obj, path, "family");
        switch (family)
        {
            case FieldStructure.FamilyName:
                return new FieldStructure
                {
                    Scale = GetParam(obj, path, "scale", 0.125),
                    Octaves = GetParam(obj, path, "octaves", 4),
                    Gain = GetParam(obj, path, "gain", 0.5),
                    Warp = GetParam(obj, path, "warp", 0.0),
                    Ridged = GetBool(obj, path, "ridged", false),
                };
            case CellsStructure.FamilyName:
                return new CellsStructure
                {
                    Count = GetParam(obj, path, "count", 8),
                    Jitter = GetParam(obj, path, "jitter", 0.8),
                    EdgeWidth = GetParam(obj, path, "edgeWidth", 0.01),
                    EdgeHeight = GetParam(obj, path, "edgeHeight", 0.0),
                };
            case PatternStructure.FamilyName:
                return new PatternStructure
                {
                    Rows = GetParam(obj, path, "rows", 4),
                    Columns = GetParam(obj, path, "columns", 4),
                    RowOffset = GetParam(obj, path, "rowOffset", 0.0),
                    MortarWidth = GetParam(obj, path, "mortarWidth", 0.01),
                    MortarHeight = GetParam(obj, path, "mortarHeight", 0.0),
                };
            default:
                throw new PresetFormatException(path + ".family", $"unknown family '{family}', expected field, cells or pattern");
        }
    }

    private static JsonObject WriteStructure(Structure structure)
    {
        var obj = new JsonObject { ["family"] = structure.Family };
        switch (structure)
        {
            case FieldStructure f:
                obj["scale"] = WriteParam(f.Scale);
                obj["octaves"] = WriteParam(f.Octaves);
                obj["gain"] = WriteParam(f.Gain);
                obj["warp"] = WriteParam(f.Warp);
                obj["ridged"] = f.Ridged;
                break;
            case CellsStructure c:
                obj["count"] = WriteParam(c.Count);
                obj["jitter"] = WriteParam(c.Jitter);
                obj["edgeWidth"] = WriteParam(c.EdgeWidth);
                obj["edgeHeight"] = WriteParam(c.EdgeHeight);
                break;
            case PatternStructure p:
                obj["rows"] = WriteParam(p.Rows);
                obj["columns"] = WriteParam(p.Columns);
                obj["rowOffset"] = WriteParam(p.RowOffset);
                obj["mortarWidth"] = WriteParam(p.MortarWidth);
                obj["mortarHeight"] = WriteParam(p.MortarHeight);
                break;
            default:
                throw new NotSupportedException($"Unknown structure family '{structure.Family}'.");
        }
        return obj;
    }

    private static IReadOnlyList<RampStop> ReadRamp(JsonObject obj, string path)
    {
        string rampPath = path + ".ramp";
        if (obj["ramp"] is not JsonArray array || array.Count == 0)
        {
            throw new PresetFormatException(rampPath, "a ramp is a non-empty list of stops");
        }
        var stops = new List<RampStop>(array.Count);
        for (int i = 0; i < array.Count; i++)
        {
            string stopPath = $"{rampPath}[{i}]";
            if (array[i] is not JsonObject stop)
            {
                throw new PresetFormatException(stopPath, "a stop is an object with t and color");
            }
            double t = GetNumber(stop, stopPath, "t");
            if (t < 0.0 || t > 1.0)
            {
                throw new PresetFormatException(stopPath + ".t", "t runs from 0 to 1");
            }
            stops.Add(new RampStop(t, ParseColor(GetString(stop, stopPath, "color"), stopPath + ".color")));
        }
        if (stops.Count > 1)
        {
            if (stops[0].T != 0.0 || stops[^1].T != 1.0)
            {
                throw new PresetFormatException(rampPath, "the first stop must be at t 0 and the last at t 1");
            }
            for (int i = 1; i < stops.Count; i++)
            {
                if (stops[i].T < stops[i - 1].T)
                {
                    throw new PresetFormatException(rampPath, "stops must be sorted by t");
                }
            }
        }
        return stops;
    }

    private static Rgb ParseColor(string text, string path)
    {
        if (text.Length == 7 && text[0] == '#'
            && byte.TryParse(text.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)
            && byte.TryParse(text.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)
            && byte.TryParse(text.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return new Rgb(r, g, b);
        }
        throw new PresetFormatException(path, $"'{text}' is not a color of the form #rrggbb");
    }

    private static JsonNode WriteParam(Param param)
    {
        if (!param.HasRange)
        {
            return JsonValue.Create(param.Value);
        }
        return new JsonObject
        {
            ["value"] = param.Value,
            ["min"] = param.Min!.Value,
            ["max"] = param.Max!.Value,
        };
    }

    /// <summary>A number is a fixed parameter and an object with value, min and max is a ranged one, as the schema defines.</summary>
    private static Param GetParam(JsonObject obj, string path, string name, double fallback)
    {
        string paramPath = path + "." + name;
        JsonNode? node = obj[name];
        if (node is null)
        {
            return new Param(fallback);
        }
        if (node is JsonObject range)
        {
            double value = GetNumber(range, paramPath, "value");
            double min = GetNumber(range, paramPath, "min");
            double max = GetNumber(range, paramPath, "max");
            try
            {
                return new Param(value, min, max);
            }
            catch (ArgumentException e)
            {
                throw new PresetFormatException(paramPath, e.Message);
            }
        }
        return new Param(AsNumber(node, paramPath));
    }

    private static JsonObject GetObject(JsonObject obj, string path, string name)
    {
        return obj[name] as JsonObject ?? throw new PresetFormatException(path + "." + name, "expected an object");
    }

    private static string GetString(JsonObject obj, string path, string name, string? fallback = null)
    {
        JsonNode? node = obj[name];
        if (node is null)
        {
            return fallback ?? throw new PresetFormatException(path + "." + name, "missing required string");
        }
        if (node is JsonValue value && value.TryGetValue(out string? text))
        {
            return text;
        }
        throw new PresetFormatException(path + "." + name, "expected a string");
    }

    private static bool GetBool(JsonObject obj, string path, string name, bool fallback)
    {
        JsonNode? node = obj[name];
        if (node is null)
        {
            return fallback;
        }
        if (node is JsonValue value && value.TryGetValue(out bool flag))
        {
            return flag;
        }
        throw new PresetFormatException(path + "." + name, "expected true or false");
    }

    private static double GetNumber(JsonObject obj, string path, string name)
    {
        JsonNode? node = obj[name];
        if (node is null)
        {
            throw new PresetFormatException(path + "." + name, "missing required number");
        }
        return AsNumber(node, path + "." + name);
    }

    private static double GetNumber(JsonObject obj, string path, string name, double fallback)
    {
        JsonNode? node = obj[name];
        return node is null ? fallback : AsNumber(node, path + "." + name);
    }

    private static int GetInt(JsonObject obj, string path, string name, int fallback)
    {
        double value = GetNumber(obj, path, name, fallback);
        if (value != Math.Floor(value) || value < int.MinValue || value > int.MaxValue)
        {
            throw new PresetFormatException(path + "." + name, "expected an integer");
        }
        return (int)value;
    }

    private static double AsNumber(JsonNode node, string path)
    {
        if (node is JsonValue value && value.TryGetValue(out double number) && double.IsFinite(number))
        {
            return number;
        }
        throw new PresetFormatException(path, "expected a number");
    }
}

/// <summary>Preset files on disk. The GUI, the CLI and the tests load shipped presets through this class.</summary>
public static class PresetFile
{
    public static Preset Load(string path)
    {
        return PresetJson.Parse(File.ReadAllText(path));
    }

    public static void Save(string path, Preset preset)
    {
        File.WriteAllText(path, PresetJson.Write(preset));
    }
}
