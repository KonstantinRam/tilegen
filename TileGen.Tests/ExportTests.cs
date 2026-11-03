using TileGen.Core;
using TileGen.IO;
using Xunit;

namespace TileGen.Tests;

public class ExportTests
{
    [Fact]
    public void ExportedFileNames_MatchThePattern_AndTheSheetStaysOutside()
    {
        using var temp = new TempDirectory();
        var preset = ShippedPresets.Load("greek_ruins");
        var request = new ExportRequest(preset, "File Names?", 64, 5, ShippedPresets.TestSeed, 0.2, 0.1, temp.Path, true);

        var result = SetExporter.Export(request);

        Assert.Equal(Path.Combine(temp.Path, "File_Names_"), result.SetDirectory);
        string[] names = Directory.GetFiles(result.SetDirectory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        string[] expected = Enumerable.Range(1, 5).Select(i => $"floor_{i:00}.png")
            .Concat(new[] { SettingsFile.FileName })
            .Concat(Enumerable.Range(1, 5).Select(i => $"wall_{i:00}.png"))
            .ToArray();
        Assert.Equal(expected, names);
        foreach (string name in names.Where(n => n != SettingsFile.FileName))
        {
            Assert.Matches(SetExporter.TileFileName(), name);
        }
        Assert.NotNull(result.ContactSheetPath);
        Assert.True(File.Exists(result.ContactSheetPath));
        Assert.Equal(temp.Path, Path.GetDirectoryName(result.ContactSheetPath));
    }

    [Theory]
    [InlineData("lava_cave")]
    [InlineData("greek_ruins")]
    public void Export_ReopenedFromSettings_ReexportsIdenticalFiles(string presetId)
    {
        using var temp = new TempDirectory();
        var preset = ShippedPresets.Load(presetId);
        string firstDir = Path.Combine(temp.Path, "first");
        string secondDir = Path.Combine(temp.Path, "second");
        var request = new ExportRequest(preset, "RoundTrip", 128, 4, ShippedPresets.TestSeed, 0.3, 0.1, firstDir, false);

        var first = SetExporter.Export(request);
        var settings = SettingsFile.Load(first.SettingsPath);
        var second = SetExporter.Export(settings, secondDir, false);

        Assert.Equal(first.Tiles.Count, second.Tiles.Count);
        foreach (string file in Directory.GetFiles(first.SetDirectory))
        {
            string counterpart = Path.Combine(second.SetDirectory, Path.GetFileName(file));
            Assert.True(File.Exists(counterpart), $"{counterpart} is missing.");
            Assert.True(File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(counterpart)), $"{Path.GetFileName(file)} differs after the round trip.");
        }
    }

    /// <summary>A fresh folder per test, removed afterwards, so tests never see each other's files.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TileGenTests", Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp folder is not worth failing a test over.
            }
        }
    }
}
