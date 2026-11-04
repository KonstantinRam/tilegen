using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TileGen.App.ViewModels;
using TileGenApp = TileGen.App.App;
using TileGen.Tests;
using Xunit;
using Xunit.Abstractions;

[assembly: AvaloniaTestApplication(typeof(GuiScreenshotTests))]

namespace TileGen.Tests;

/// <summary>
/// Builds the main window on the headless platform with real Skia drawing and writes a screenshot, because a window
/// that binds and lays out wrong passes every other test. The image is reviewed like a contact sheet.
/// </summary>
public class GuiScreenshotTests
{
    private readonly ITestOutputHelper _output;

    public GuiScreenshotTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TileGenApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
    }

    [AvaloniaFact]
    public async Task MainWindow_RendersHeadless_AndWritesScreenshots()
    {
        var vm = new MainViewModel(TileGen.IO.PresetFolder.Locate(), persistAppData: false);
        var window = TileGenApp.CreateMainWindow(vm);
        vm.BaseSeedText = "1234";
        window.Show();
        await vm.WaitForIdleAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(vm.Floor.Variants);
        Assert.NotEmpty(vm.Wall.Variants);
        Assert.NotNull(vm.Floor.SelectedTile);
        Capture(window, "gui_main.png");

        // The second image shows the garbage tab, the zoomed room and a brick preset, so the review sees every control once.
        vm.Floor.SelectedTab = 1;
        vm.Room.Zoom = "1:1";
        vm.SelectedPreset = vm.Presets.FirstOrDefault(p => p.Id == "greek_ruins");
        vm.LoadPresetCommand.Execute(null);
        await vm.WaitForIdleAsync();
        Dispatcher.UIThread.RunJobs();
        Capture(window, "gui_garbage_tab.png");

        // The third image shows the blocks family, whose layout rows have no lock button, on the wall's base tab.
        vm.Floor.SelectedTab = 0;
        vm.Room.Zoom = "Fit";
        vm.SelectedPreset = vm.Presets.FirstOrDefault(p => p.Id == "dungeon");
        vm.LoadPresetCommand.Execute(null);
        await vm.WaitForIdleAsync();
        Dispatcher.UIThread.RunJobs();
        Capture(window, "gui_blocks.png");

        window.Close();
    }

    private void Capture(Avalonia.Controls.Window window, string name)
    {
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame!.PixelSize.Width > 100 && frame.PixelSize.Height > 100, "The captured frame is too small to be the window.");
        string path = ScreenshotPath(name);
        frame.Save(path);
        _output.WriteLine("Screenshot: " + path);
    }

    /// <summary>Writes into out next to the solution when the tests run from the repository, so the image sits beside the contact sheets.</summary>
    private static string ScreenshotPath(string name)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "TileGenerator.sln")))
            {
                string outDir = Path.Combine(dir, "out");
                Directory.CreateDirectory(outDir);
                return Path.Combine(outDir, name);
            }
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(AppContext.BaseDirectory, name);
    }
}
