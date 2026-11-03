using System.Text.Json;
using System.Text.Json.Nodes;
using TileGen.Core;

namespace TileGen.IO;

/// <summary>Everything settings.json records about an export, which is everything needed to reproduce it.</summary>
public sealed record ExportSettings(
    string GeneratorVersion,
    string SetName,
    Preset Preset,
    int Size,
    int VariantCount,
    ulong BaseSeed,
    double FloorCoverage,
    double WallCoverage,
    IReadOnlyList<Variant> Variants);

/// <summary>
/// Reads and writes settings.json. The variants carry their resolved parameters, so a re-export renders from the
/// stored numbers and never runs the jitter again, which keeps old exports reproducible if the jitter code changes.
/// </summary>
public static class SettingsFile
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string Write(ExportSettings settings)
    {
        var variants = new JsonArray();
        foreach (var variant in settings.Variants)
        {
            variants.Add(new JsonObject
            {
                ["index"] = variant.Index,
                ["seed"] = variant.Seed,
                ["parameters"] = new JsonObject
                {
                    ["floor"] = WriteParameters(variant.Floor),
                    ["wall"] = WriteParameters(variant.Wall),
                },
            });
        }
        var root = new JsonObject
        {
            ["generatorVersion"] = settings.GeneratorVersion,
            ["presetId"] = settings.Preset.Id,
            ["setName"] = settings.SetName,
            ["size"] = settings.Size,
            ["variantCount"] = settings.VariantCount,
            ["baseSeed"] = settings.BaseSeed,
            ["garbageCoverage"] = new JsonObject
            {
                ["floor"] = settings.FloorCoverage,
                ["wall"] = settings.WallCoverage,
            },
            ["preset"] = PresetJson.ToNode(settings.Preset),
            ["variants"] = variants,
        };
        return root.ToJsonString(WriteOptions) + Environment.NewLine;
    }

    public static void Save(string path, ExportSettings settings)
    {
        File.WriteAllText(path, Write(settings));
    }

    public static ExportSettings Read(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException e)
        {
            throw new PresetFormatException("$", "settings.json is not valid JSON: " + e.Message);
        }
        if (root is not JsonObject obj)
        {
            throw new PresetFormatException("$", "the top level of settings.json must be an object");
        }

        string version = Str(obj, "generatorVersion");
        string setName = Str(obj, "setName");
        int size = Int(obj, "size");
        int variantCount = Int(obj, "variantCount");
        ulong baseSeed = Seed(obj, "baseSeed", "$");
        var coverage = obj["garbageCoverage"] as JsonObject ?? throw new PresetFormatException("$.garbageCoverage", "expected an object with floor and wall");
        double floorCoverage = Num(coverage, "floor", "$.garbageCoverage");
        double wallCoverage = Num(coverage, "wall", "$.garbageCoverage");
        var presetNode = obj["preset"] as JsonObject ?? throw new PresetFormatException("$.preset", "expected the preset snapshot object");
        Preset preset = PresetJson.FromNode(presetNode);

        if (obj["variants"] is not JsonArray array)
        {
            throw new PresetFormatException("$.variants", "expected a list of variants");
        }
        if (array.Count != variantCount)
        {
            throw new PresetFormatException("$.variants", $"variantCount is {variantCount} but the list holds {array.Count} entries");
        }
        var variants = new List<Variant>(array.Count);
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"$.variants[{i}]";
            if (array[i] is not JsonObject entry)
            {
                throw new PresetFormatException(path, "expected an object");
            }
            int index = Int(entry, "index", path);
            if (index != i + 1)
            {
                throw new PresetFormatException(path + ".index", $"expected {i + 1}, because variants are listed in order from 1");
            }
            var parameters = entry["parameters"] as JsonObject ?? throw new PresetFormatException(path + ".parameters", "expected an object with floor and wall");
            variants.Add(new Variant(
                index,
                Seed(entry, "seed", path),
                ReadParameters(parameters, "floor", path + ".parameters"),
                ReadParameters(parameters, "wall", path + ".parameters")));
        }

        return new ExportSettings(version, setName, preset, size, variantCount, baseSeed, floorCoverage, wallCoverage, variants);
    }

    public static ExportSettings Load(string path)
    {
        return Read(File.ReadAllText(path));
    }

    private static JsonObject WriteParameters(ParameterSet set)
    {
        var obj = new JsonObject();
        foreach (var pair in set.Values)
        {
            obj[pair.Key] = pair.Value;
        }
        return obj;
    }

    private static ParameterSet ReadParameters(JsonObject parent, string name, string path)
    {
        string setPath = path + "." + name;
        var obj = parent[name] as JsonObject ?? throw new PresetFormatException(setPath, "expected an object of parameter paths to numbers");
        var values = new List<KeyValuePair<string, double>>();
        foreach (var pair in obj)
        {
            if (pair.Value is JsonValue value && value.TryGetValue(out double number) && double.IsFinite(number))
            {
                values.Add(new KeyValuePair<string, double>(pair.Key, number));
            }
            else
            {
                throw new PresetFormatException(setPath + "." + pair.Key, "expected a number");
            }
        }
        return new ParameterSet(values);
    }

    private static string Str(JsonObject obj, string name)
    {
        if (obj[name] is JsonValue value && value.TryGetValue(out string? text))
        {
            return text;
        }
        throw new PresetFormatException("$." + name, "expected a string");
    }

    private static double Num(JsonObject obj, string name, string path)
    {
        if (obj[name] is JsonValue value && value.TryGetValue(out double number) && double.IsFinite(number))
        {
            return number;
        }
        throw new PresetFormatException(path + "." + name, "expected a number");
    }

    private static int Int(JsonObject obj, string name, string path = "$")
    {
        double number = Num(obj, name, path);
        if (number != Math.Floor(number) || number < int.MinValue || number > int.MaxValue)
        {
            throw new PresetFormatException(path + "." + name, "expected an integer");
        }
        return (int)number;
    }

    private static ulong Seed(JsonObject obj, string name, string path)
    {
        if (obj[name] is JsonValue value && value.TryGetValue(out ulong seed))
        {
            return seed;
        }
        throw new PresetFormatException(path + "." + name, "expected a non-negative integer seed");
    }
}
