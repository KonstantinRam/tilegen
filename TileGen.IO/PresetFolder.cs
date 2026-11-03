using TileGen.Core;

namespace TileGen.IO;

/// <summary>One entry of the preset list. Error is set when the file did not parse, so the GUI can show the reason instead of hiding the file.</summary>
public sealed record PresetEntry(string Path, string Id, string DisplayName, string? Error);

/// <summary>
/// The presets folder. When the tool runs from the repository the folder next to the solution is used, so a preset
/// saved from the GUI lands in version control; a published build uses the presets folder next to the executable.
/// </summary>
public static class PresetFolder
{
    public const string FolderName = "presets";
    private const string SolutionFileName = "TileGenerator.sln";

    public static string Locate()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, SolutionFileName)) && Directory.Exists(Path.Combine(dir, FolderName)))
            {
                return Path.Combine(dir, FolderName);
            }
            dir = Path.GetDirectoryName(dir);
        }
        string beside = Path.Combine(AppContext.BaseDirectory, FolderName);
        Directory.CreateDirectory(beside);
        return beside;
    }

    public static IReadOnlyList<PresetEntry> List(string folder)
    {
        var entries = new List<PresetEntry>();
        foreach (string path in Directory.EnumerateFiles(folder, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Preset preset = PresetFile.Load(path);
                entries.Add(new PresetEntry(path, preset.Id, preset.DisplayName, null));
            }
            catch (Exception e) when (e is PresetFormatException or IOException)
            {
                entries.Add(new PresetEntry(path, Path.GetFileNameWithoutExtension(path), Path.GetFileName(path), e.Message));
            }
        }
        return entries;
    }

    public static string PathFor(string folder, string id)
    {
        return Path.Combine(folder, id + ".json");
    }
}
