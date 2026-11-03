using System.Text;
using System.Text.RegularExpressions;
using TileGen.Core;

namespace TileGen.IO;

public sealed record ExportRequest(
    Preset Preset,
    string SetName,
    int Size,
    int VariantCount,
    ulong BaseSeed,
    double FloorCoverage,
    double WallCoverage,
    string OutputDirectory,
    bool ContactSheet);

public sealed record ExportedTile(TileKind Kind, int Index, string Path, double RequestedCoverage, double MeasuredCoverage);

public sealed record ExportResult(
    string SetDirectory,
    string SettingsPath,
    string? ContactSheetPath,
    IReadOnlyList<ExportedTile> Tiles,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Writes a set folder as docs/SPEC.md describes it: floor_NN.png, wall_NN.png and settings.json, with the contact
/// sheet next to the folder and never inside it, because the client's import script scans the folder for PNG files.
/// </summary>
public static partial class SetExporter
{
    /// <summary>The exporter reports a coverage further than this from the request, because the quantile threshold promises the slider is honest.</summary>
    public const double CoverageTolerance = 0.01;

    /// <summary>docs/SPEC.md fixes the minimum, and the GUI offers no smaller count, so the exporter refuses smaller sets too.</summary>
    public const int MinVariants = 4;

    /// <summary>File names carry two digits, so a set holds at most 99 variants.</summary>
    public const int MaxVariants = 99;

    [GeneratedRegex(@"^(floor|wall)_[0-9]{2}\.png$")]
    public static partial Regex TileFileName();

    /// <summary>Set names become folder names, so anything outside letters, digits and underscores is replaced.</summary>
    public static string SanitizeSetName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            sb.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
        }
        return sb.Length == 0 ? "Set" : sb.ToString();
    }

    public static string FileName(TileKind kind, int index)
    {
        return (kind == TileKind.Floor ? "floor_" : "wall_") + index.ToString("00") + ".png";
    }

    /// <summary>Exports a fresh set: the variants are derived from the base seed and the preset's jitter.</summary>
    public static ExportResult Export(ExportRequest request)
    {
        CheckVariantCount(request.VariantCount);
        var settings = new ExportSettings(
            Generator.Version,
            SanitizeSetName(request.SetName),
            request.Preset,
            request.Size,
            request.VariantCount,
            request.BaseSeed,
            request.FloorCoverage,
            request.WallCoverage,
            Variants.Resolve(request.Preset, request.BaseSeed, request.VariantCount));
        return Export(settings, request.OutputDirectory, request.ContactSheet);
    }

    /// <summary>Exports from stored settings, which is how a settings.json is reopened: the stored variant parameters are rendered as they are.</summary>
    public static ExportResult Export(ExportSettings settings, string outputDirectory, bool contactSheet)
    {
        CheckVariantCount(settings.VariantCount);
        if (settings.Variants.Count != settings.VariantCount)
        {
            throw new ArgumentException($"The settings list {settings.Variants.Count} variants but declare {settings.VariantCount}.");
        }
        string setName = SanitizeSetName(settings.SetName);
        string setDirectory = Path.Combine(outputDirectory, setName);
        Directory.CreateDirectory(setDirectory);
        RemoveStaleTiles(setDirectory);

        var warnings = new List<string>();
        var tiles = new List<ExportedTile>();
        var floorTiles = new List<byte[]>();
        var wallTiles = new List<byte[]>();
        var preset = settings.Preset;

        foreach (var variant in settings.Variants)
        {
            floorTiles.Add(WriteTile(new TileRequest(preset, TileKind.Floor, variant.Floor, settings.FloorCoverage, variant.Seed, settings.Size, settings.BaseSeed), variant.Index, setDirectory, tiles, warnings));
            wallTiles.Add(WriteTile(new TileRequest(preset, TileKind.Wall, variant.Wall, settings.WallCoverage, variant.Seed, settings.Size, settings.BaseSeed), variant.Index, setDirectory, tiles, warnings));
        }

        string settingsPath = Path.Combine(setDirectory, SettingsFile.FileName);
        SettingsFile.Save(settingsPath, settings with { SetName = setName });

        string? sheetPath = null;
        if (contactSheet)
        {
            var (rgb, width, height) = IO.ContactSheet.Build(floorTiles, wallTiles, settings.Size);
            sheetPath = Path.Combine(outputDirectory, setName + "_contact.png");
            PngFile.Write(sheetPath, rgb, width, height);
        }

        return new ExportResult(setDirectory, settingsPath, sheetPath, tiles, warnings);
    }

    private static void CheckVariantCount(int count)
    {
        if (count < MinVariants || count > MaxVariants)
        {
            throw new ArgumentOutOfRangeException(nameof(count), $"A set has between {MinVariants} and {MaxVariants} variants per tile type, got {count}.");
        }
    }

    private static byte[] WriteTile(TileRequest tileRequest, int index, string setDirectory, List<ExportedTile> tiles, List<string> warnings)
    {
        byte[] pixels = Generator.Render(tileRequest);
        double measured = Generator.MeasureCoverage(tileRequest);
        string path = Path.Combine(setDirectory, FileName(tileRequest.Kind, index));
        PngFile.Write(path, pixels, tileRequest.Size, tileRequest.Size);
        tiles.Add(new ExportedTile(tileRequest.Kind, index, path, tileRequest.GarbageCoverage, measured));
        if (Math.Abs(measured - tileRequest.GarbageCoverage) > CoverageTolerance)
        {
            warnings.Add($"{Path.GetFileName(path)}: measured garbage coverage {measured:0.000} is further than {CoverageTolerance} from the requested {tileRequest.GarbageCoverage:0.000}.");
        }
        return pixels;
    }

    /// <summary>A previous export with more variants would leave extra tiles that the import script would pick up, so tiles of this tool's pattern are removed first.</summary>
    private static void RemoveStaleTiles(string setDirectory)
    {
        foreach (string file in Directory.EnumerateFiles(setDirectory))
        {
            string name = Path.GetFileName(file);
            if (TileFileName().IsMatch(name) || name == SettingsFile.FileName)
            {
                File.Delete(file);
            }
        }
    }
}
