using System.Text.Json;
using System.Text.Json.Nodes;
using TileGen.Core;

namespace TileGen.IO;

/// <summary>A stored favorite: a preset snapshot with the seed, size and variant count, regenerated on demand.</summary>
public sealed record Favorite(string Name, Preset Preset, ulong BaseSeed, int Size, int VariantCount);

/// <summary>Small things the GUI remembers between runs. None of it affects what a tile looks like.</summary>
public sealed record UiState(string? OutputFolder, string? LastPresetId, bool WriteContactSheet);

/// <summary>
/// favorites.json and ui.json under the user's application data folder. Favorites hold a preset snapshot rather than
/// a preset id, because the preset file can change after the favorite was saved.
/// </summary>
public static class AppData
{
    public const string FavoritesFileName = "favorites.json";
    public const string UiFileName = "ui.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string Directory
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);
            return Path.Combine(root, "TileGen");
        }
    }

    public static IReadOnlyList<Favorite> LoadFavorites()
    {
        string path = Path.Combine(Directory, FavoritesFileName);
        if (!File.Exists(path))
        {
            return Array.Empty<Favorite>();
        }
        return ParseFavorites(File.ReadAllText(path));
    }

    public static void SaveFavorites(IReadOnlyList<Favorite> favorites)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(Path.Combine(Directory, FavoritesFileName), WriteFavorites(favorites));
    }

    public static string WriteFavorites(IReadOnlyList<Favorite> favorites)
    {
        var list = new JsonArray();
        foreach (var f in favorites)
        {
            list.Add(new JsonObject
            {
                ["name"] = f.Name,
                ["baseSeed"] = f.BaseSeed,
                ["size"] = f.Size,
                ["variantCount"] = f.VariantCount,
                ["preset"] = PresetJson.ToNode(f.Preset),
            });
        }
        var root = new JsonObject { ["generatorVersion"] = Generator.Version, ["favorites"] = list };
        return root.ToJsonString(WriteOptions) + Environment.NewLine;
    }

    public static IReadOnlyList<Favorite> ParseFavorites(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new PresetFormatException("$", "favorites.json must be an object");
        if (root["favorites"] is not JsonArray list)
        {
            throw new PresetFormatException("$.favorites", "expected a list");
        }
        var favorites = new List<Favorite>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            string path = $"$.favorites[{i}]";
            var obj = list[i] as JsonObject ?? throw new PresetFormatException(path, "expected an object");
            var presetNode = obj["preset"] as JsonObject ?? throw new PresetFormatException(path + ".preset", "expected the preset snapshot");
            favorites.Add(new Favorite(
                obj["name"]?.GetValue<string>() ?? throw new PresetFormatException(path + ".name", "expected a string"),
                PresetJson.FromNode(presetNode),
                obj["baseSeed"]?.GetValue<ulong>() ?? throw new PresetFormatException(path + ".baseSeed", "expected a seed"),
                obj["size"]?.GetValue<int>() ?? throw new PresetFormatException(path + ".size", "expected a size"),
                obj["variantCount"]?.GetValue<int>() ?? throw new PresetFormatException(path + ".variantCount", "expected a count")));
        }
        return favorites;
    }

    public static UiState LoadUiState()
    {
        string path = Path.Combine(Directory, UiFileName);
        if (!File.Exists(path))
        {
            return new UiState(null, null, false);
        }
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            return new UiState(
                root?["outputFolder"]?.GetValue<string>(),
                root?["lastPresetId"]?.GetValue<string>(),
                root?["writeContactSheet"]?.GetValue<bool>() ?? false);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            // A damaged ui.json only loses a remembered folder, so starting fresh beats failing to start.
            return new UiState(null, null, false);
        }
    }

    public static void SaveUiState(UiState state)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var root = new JsonObject
        {
            ["outputFolder"] = state.OutputFolder,
            ["lastPresetId"] = state.LastPresetId,
            ["writeContactSheet"] = state.WriteContactSheet,
        };
        File.WriteAllText(Path.Combine(Directory, UiFileName), root.ToJsonString(WriteOptions) + Environment.NewLine);
    }
}
