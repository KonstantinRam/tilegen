using System.Diagnostics;
using System.Globalization;
using TileGen.Core;
using TileGen.IO;

namespace TileGen.Cli;

/// <summary>Exports one set from a preset without a GUI, which is how the tests and Claude Code exercise the full pipeline.</summary>
public static class Program
{
    private const string Usage = """
        Usage: TileGen.Cli --preset <file> [options]

          --preset <file>        Preset JSON file. Required unless --settings is given.
          --settings <file>      A settings.json from an earlier export. Re-exports that set from its stored
                                 variants; only --set, --out and --contact may be combined with it.
          --set <name>           Set name, also the folder name. Default: the preset id.
          --size <n>             Tile size: 64, 128, 256, 512 or 1024. Default: 256.
          --variants <n>         Variant count per tile type. Default: the preset's count.
          --seed <n>             Base seed, a non-negative integer. Default: drawn at random and printed.
          --out <dir>            Output directory. Default: ./out.
          --garbage-floor <0..1> Floor garbage coverage. Default: the preset's value.
          --garbage-wall <0..1>  Wall garbage coverage. Default: the preset's value.
          --contact              Also write <out>/<set>_contact.png next to the set folder.
          --help                 Show this text.
        """;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (PresetFormatException e)
        {
            Console.Error.WriteLine("Preset error: " + e.Message);
            return 2;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Error: " + e.Message);
            return 2;
        }
    }

    private static int Run(string[] args)
    {
        string? presetPath = null;
        string? settingsPath = null;
        string? setName = null;
        int size = 256;
        bool sizeGiven = false;
        int? variants = null;
        ulong? seed = null;
        string outDir = "out";
        double? floorCoverage = null;
        double? wallCoverage = null;
        bool contact = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    Console.WriteLine(Usage);
                    return 0;
                case "--contact":
                    contact = true;
                    break;
                case "--preset":
                    presetPath = Next(args, ref i);
                    break;
                case "--settings":
                    settingsPath = Next(args, ref i);
                    break;
                case "--set":
                    setName = Next(args, ref i);
                    break;
                case "--size":
                    size = ParseInt(Next(args, ref i), arg);
                    sizeGiven = true;
                    break;
                case "--variants":
                    variants = ParseInt(Next(args, ref i), arg);
                    break;
                case "--seed":
                    seed = ParseSeed(Next(args, ref i), arg);
                    break;
                case "--out":
                    outDir = Next(args, ref i);
                    break;
                case "--garbage-floor":
                    floorCoverage = ParseFraction(Next(args, ref i), arg);
                    break;
                case "--garbage-wall":
                    wallCoverage = ParseFraction(Next(args, ref i), arg);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{arg}'.\n{Usage}");
            }
        }

        if (settingsPath is not null)
        {
            if (presetPath is not null || variants is not null || seed is not null || floorCoverage is not null || wallCoverage is not null || sizeGiven)
            {
                throw new ArgumentException("--settings reproduces a stored export, so it cannot be combined with --preset, --size, --variants, --seed or the garbage options.");
            }
            return RunFromSettings(settingsPath, setName, outDir, contact);
        }
        if (presetPath is null)
        {
            Console.Error.WriteLine(Usage);
            return 1;
        }

        Preset preset = PresetFile.Load(presetPath);
        // The seed is drawn outside the pixel path, so System.Random is allowed here and the drawn value is printed for reproduction.
        ulong baseSeed = seed ?? (ulong)Random.Shared.NextInt64(0, long.MaxValue);
        var request = new ExportRequest(
            preset,
            setName ?? preset.Id,
            size,
            variants ?? preset.Variants,
            baseSeed,
            floorCoverage ?? preset.Floor.GarbageCoverage,
            wallCoverage ?? preset.Wall.GarbageCoverage,
            outDir,
            contact);

        var watch = Stopwatch.StartNew();
        ExportResult result = SetExporter.Export(request);
        watch.Stop();

        Console.WriteLine($"Preset {preset.Id} ({preset.DisplayName}), size {size}, {request.VariantCount} variants, seed {baseSeed}.");
        Report(result, watch.ElapsedMilliseconds);
        return 0;
    }

    private static int RunFromSettings(string settingsPath, string? setName, string outDir, bool contact)
    {
        ExportSettings settings = SettingsFile.Load(settingsPath);
        if (setName is not null)
        {
            settings = settings with { SetName = setName };
        }
        var watch = Stopwatch.StartNew();
        ExportResult result = SetExporter.Export(settings, outDir, contact);
        watch.Stop();

        Console.WriteLine($"Reproduced {settings.SetName} from {settingsPath}: preset {settings.Preset.Id}, size {settings.Size}, {settings.VariantCount} variants, seed {settings.BaseSeed}.");
        if (settings.GeneratorVersion != Generator.Version)
        {
            Console.WriteLine($"Warning: the export was made with generator {settings.GeneratorVersion} and this is {Generator.Version}, so the bytes may differ.");
        }
        Report(result, watch.ElapsedMilliseconds);
        return 0;
    }

    private static void Report(ExportResult result, long elapsedMilliseconds)
    {
        foreach (var tile in result.Tiles)
        {
            Console.WriteLine($"  {Path.GetFileName(tile.Path)}  garbage requested {tile.RequestedCoverage.ToString("0.000", CultureInfo.InvariantCulture)}  measured {tile.MeasuredCoverage.ToString("0.000", CultureInfo.InvariantCulture)}");
        }
        Console.WriteLine($"Set folder: {result.SetDirectory}");
        Console.WriteLine($"Settings:   {result.SettingsPath}");
        if (result.ContactSheetPath is not null)
        {
            Console.WriteLine($"Contact:    {result.ContactSheetPath}  (floor grid left, wall grid right, 4 by 4 wrapped, variants mixed)");
        }
        foreach (string warning in result.Warnings)
        {
            Console.WriteLine("Warning: " + warning);
        }
        Console.WriteLine($"Done in {elapsedMilliseconds} ms.");
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Option '{args[i]}' needs a value.\n{Usage}");
        }
        return args[++i];
    }

    private static int ParseInt(string text, string option)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException($"Option '{option}' expects an integer, got '{text}'.");
        }
        return value;
    }

    private static ulong ParseSeed(string text, string option)
    {
        if (!ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value))
        {
            throw new ArgumentException($"Option '{option}' expects a non-negative integer, got '{text}'.");
        }
        return value;
    }

    private static double ParseFraction(string text, string option)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0.0 || value > 1.0)
        {
            throw new ArgumentException($"Option '{option}' expects a fraction from 0 to 1, got '{text}'.");
        }
        return value;
    }
}
