using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TileGen.App.ViewModels;

namespace TileGen.App.Views;

/// <summary>Shows one tile. Without an explicit size it takes the tile's pixel size, which is the 1:1 preview.</summary>
public sealed class TileView : Control
{
    public static readonly StyledProperty<TileImage?> TileProperty =
        AvaloniaProperty.Register<TileView, TileImage?>(nameof(Tile));

    private static readonly IBrush Placeholder = new SolidColorBrush(Color.FromRgb(40, 40, 44));

    static TileView()
    {
        AffectsRender<TileView>(TileProperty);
        AffectsMeasure<TileView>(TileProperty);
    }

    public TileView()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
    }

    public TileImage? Tile
    {
        get => GetValue(TileProperty);
        set => SetValue(TileProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var tile = Tile;
        return tile is null ? new Size(64, 64) : new Size(tile.Size, tile.Size);
    }

    public override void Render(DrawingContext context)
    {
        var tile = Tile;
        if (tile is null)
        {
            context.FillRectangle(Placeholder, new Rect(Bounds.Size));
            return;
        }
        double scale = Math.Min(Bounds.Width / tile.Size, Bounds.Height / tile.Size);
        double side = tile.Size * scale;
        var dest = new Rect((Bounds.Width - side) / 2.0, (Bounds.Height - side) / 2.0, side, side);
        context.DrawImage(TileBitmaps.Get(tile), new Rect(0, 0, tile.Size, tile.Size), dest);
    }
}
