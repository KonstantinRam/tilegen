using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TileGen.App.ViewModels;

namespace TileGen.App.Views;

public partial class MaterialColumnView : UserControl
{
    public MaterialColumnView()
    {
        InitializeComponent();
        // The column touched last is the target of "palette from image", so the press is noted before any child handles it.
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MaterialColumnViewModel column && this.FindAncestorOfType<Window>()?.DataContext is MainViewModel main)
        {
            main.ActiveKind = column.Kind;
        }
    }
}
