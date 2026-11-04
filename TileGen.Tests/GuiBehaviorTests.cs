using Avalonia.Headless.XUnit;
using TileGen.App.ViewModels;
using TileGen.Core;
using TileGen.IO;
using Xunit;

namespace TileGen.Tests;

/// <summary>
/// Drives the main view model the way the window does, without the window. These tests cover the checklist items
/// that a screenshot cannot: the preview is the export, an edit reaches the preset, and a settings.json restores the state.
/// </summary>
public class GuiBehaviorTests
{
    private static MainViewModel NewViewModel(string presetId)
    {
        var vm = new MainViewModel(PresetFolder.Locate(), persistAppData: false);
        vm.SelectedPreset = vm.Presets.First(p => p.Id == presetId);
        vm.LoadPresetCommand.Execute(null);
        vm.BaseSeedText = ShippedPresets.TestSeed.ToString();
        return vm;
    }

    [AvaloniaFact]
    public async Task PreviewTiles_AreTheBytesTheExportWrites()
    {
        var vm = NewViewModel("lava_cave");
        vm.Size = 256;
        await vm.WaitForIdleAsync();

        var variant = vm.CurrentVariants[1];
        var preset = vm.CurrentPreset;
        byte[] floor = Generator.Render(new TileRequest(preset, TileKind.Floor, variant.Floor, preset.Floor.GarbageCoverage, variant.Seed, 256, vm.BaseSeed));
        byte[] wall = Generator.Render(new TileRequest(preset, TileKind.Wall, variant.Wall, preset.Wall.GarbageCoverage, variant.Seed, 256, vm.BaseSeed));

        Assert.Equal(floor, vm.Floor.Variants[1].Tile!.Rgb);
        Assert.Equal(wall, vm.Wall.Variants[1].Tile!.Rgb);
    }

    [AvaloniaFact]
    public async Task RowEdit_ReachesThePreset_AndRerendersOnlyThatKind()
    {
        var vm = NewViewModel("lava_cave");
        await vm.WaitForIdleAsync();
        byte[] floorBefore = vm.Floor.Variants[0].Tile!.Rgb;
        byte[] wallBefore = vm.Wall.Variants[0].Tile!.Rgb;

        // The lava floor's ranged field scale lives in the garbage material, because the glowing lava is the garbage.
        var scale = vm.Floor.Garbage.Rows.First(r => r.Path == "garbage.structure.scale");
        scale.Value = 0.25;
        await vm.WaitForIdleAsync();

        var field = Assert.IsType<FieldStructure>(vm.CurrentPreset.Floor.Garbage.Structure);
        Assert.Equal(0.25, field.Scale.Value);
        Assert.True(field.Scale.HasRange, "The preset range survives an edit inside it.");
        Assert.NotEqual(floorBefore, vm.Floor.Variants[0].Tile!.Rgb);
        Assert.Same(wallBefore, vm.Wall.Variants[0].Tile!.Rgb);

        scale.IsLocked = true;
        Assert.False(((FieldStructure)vm.CurrentPreset.Floor.Garbage.Structure).Scale.HasRange);
        scale.IsLocked = false;
        var unlocked = ((FieldStructure)vm.CurrentPreset.Floor.Garbage.Structure).Scale;
        Assert.Equal(scale.Minimum, unlocked.Min);
        Assert.Equal(scale.Maximum, unlocked.Max);

        vm.Floor.Coverage = 55.0;
        Assert.Equal(0.55, vm.CurrentPreset.Floor.GarbageCoverage, 10);
        await vm.WaitForIdleAsync();
    }

    [AvaloniaFact]
    public async Task BlocksLayoutRows_HaveNoLock_AndStayFixed()
    {
        var vm = NewViewModel("dungeon");
        await vm.WaitForIdleAsync();
        var rows = vm.Wall.Base.Rows.First(r => r.Path == "base.structure.rows");
        var mortar = vm.Wall.Base.Rows.First(r => r.Path == "base.structure.mortarWidth");
        Assert.False(rows.CanRange);
        Assert.True(mortar.CanRange);
        rows.IsLocked = false;
        Assert.False(rows.ToParam().HasRange, "A layout parameter of the blocks family never gains a range.");
        var blocks = Assert.IsType<BlocksStructure>(vm.CurrentPreset.Wall.Base.Structure);
        Assert.False(blocks.Rows.HasRange);
    }

    [AvaloniaFact]
    public async Task RampEdit_KeepsTheEndsAtZeroAndOne()
    {
        var vm = NewViewModel("greek_ruins");
        var ramp = vm.Wall.Base.Ramp;
        int before = ramp.Stops.Count;

        ramp.SelectedIndex = 0;
        ramp.AddStopCommand.Execute(null);
        ramp.MoveStop(1, 0.9);
        ramp.SelectedHex = "#ff0000";

        var stops = vm.CurrentPreset.Wall.Base.Ramp;
        Assert.Equal(before + 1, stops.Count);
        Assert.Equal(0.0, stops[0].T);
        Assert.Equal(1.0, stops[^1].T);
        Assert.True(stops[1].T <= stops[2].T, "A moved stop stays between its neighbors.");
        Assert.Equal(new Rgb(255, 0, 0), stops[1].Color);

        while (ramp.Stops.Count > 1)
        {
            ramp.RemoveStopCommand.Execute(null);
        }
        Assert.Single(vm.CurrentPreset.Wall.Base.Ramp);
        await vm.WaitForIdleAsync();
    }

    [AvaloniaFact]
    public async Task GuiExport_MatchesTheExporter_AndReopensFromSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), "TileGenTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var vm = NewViewModel("lava_cave");
            var dialogs = new FakeDialogs { Folder = Path.Combine(root, "gui") };
            vm.Dialogs = dialogs;
            vm.SetName = "GuiSet";
            vm.Size = 128;
            await vm.WaitForIdleAsync();

            await vm.ExportSetCommand.ExecuteAsync(null);
            string guiDir = Path.Combine(root, "gui", "GuiSet");
            Assert.True(File.Exists(Path.Combine(guiDir, "settings.json")), vm.Status);

            var preset = vm.CurrentPreset;
            var direct = SetExporter.Export(new ExportRequest(preset, "Direct", 128, vm.VariantCount, vm.BaseSeed,
                preset.Floor.GarbageCoverage, preset.Wall.GarbageCoverage, Path.Combine(root, "direct"), false));
            foreach (var tile in direct.Tiles)
            {
                byte[] expected = File.ReadAllBytes(tile.Path);
                byte[] actual = File.ReadAllBytes(Path.Combine(guiDir, Path.GetFileName(tile.Path)));
                Assert.True(expected.AsSpan().SequenceEqual(actual), $"{Path.GetFileName(tile.Path)} differs between the GUI export and the exporter.");
            }

            // Change everything, reopen the settings, and the second export must be the first one again.
            vm.RerollSeedCommand.Execute(null);
            vm.Floor.Coverage = 90.0;
            vm.Size = 64;
            dialogs.File = Path.Combine(guiDir, "settings.json");
            dialogs.Folder = Path.Combine(root, "again");
            // The output folder is remembered after the first export, so a new one is chosen through the menu command.
            await vm.ChooseOutputFolderCommand.ExecuteAsync(null);
            await vm.OpenSettingsCommand.ExecuteAsync(null);
            Assert.Equal(ShippedPresets.TestSeed, vm.BaseSeed);
            Assert.Equal(128, vm.Size);
            Assert.Equal(preset.Floor.GarbageCoverage, vm.CurrentPreset.Floor.GarbageCoverage);
            await vm.WaitForIdleAsync();

            await vm.ExportSetCommand.ExecuteAsync(null);
            string againDir = Path.Combine(root, "again", "GuiSet");
            foreach (string file in Directory.GetFiles(guiDir))
            {
                byte[] expected = File.ReadAllBytes(file);
                byte[] actual = File.ReadAllBytes(Path.Combine(againDir, Path.GetFileName(file)));
                Assert.True(expected.AsSpan().SequenceEqual(actual), $"{Path.GetFileName(file)} differs after reopening settings.json.");
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeDialogs : IDialogs
    {
        public string? File { get; set; }
        public string? Folder { get; set; }

        public Task<string?> PickOpenFileAsync(string title, string filterName, IReadOnlyList<string> patterns) => Task.FromResult(File);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult(Folder);
        public Task<string?> PromptTextAsync(string title, string label, string initial) => Task.FromResult<string?>(initial);
        public Task<(string Id, string DisplayName)?> PromptPresetNameAsync(string initialId, string initialName) => Task.FromResult<(string, string)?>((initialId, initialName));
        public Task<PaletteChoice?> PromptPaletteAsync(IReadOnlyList<PaletteTarget> targets, PaletteTarget initial) => Task.FromResult<PaletteChoice?>(new PaletteChoice(initial, 4, false));
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
    }
}
