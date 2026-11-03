using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TileGen.App.ViewModels;
using TileGen.App.Views;

namespace TileGen.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateMainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>The desktop lifetime and the headless screenshot test build the window the same way.</summary>
    public static MainWindow CreateMainWindow(MainViewModel? viewModel = null)
    {
        return new MainWindow { DataContext = viewModel ?? new MainViewModel() };
    }
}
