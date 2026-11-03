using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TileGen.App.ViewModels;

namespace TileGen.App.Views;

/// <summary>
/// Draws a ring of wall tiles around a rectangle of floor tiles straight from the cached bitmaps, so no composed
/// image exists and a 16 by 16 room at 1024 pixels costs nothing more than the draw calls that are visible.
/// </summary>
public sealed class RoomPreviewControl : Control
{
    private RoomPreviewViewModel? _vm;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.Changed -= OnChanged;
        }
        _vm = DataContext as RoomPreviewViewModel;
        if (_vm is not null)
        {
            _vm.Changed += OnChanged;
        }
        OnChanged();
    }

    private void OnChanged()
    {
        // The fit view downscales with a smooth filter and the zoomed views show the pixels as they are.
        RenderOptions.SetBitmapInterpolationMode(this, _vm?.IsFit == true ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private int Cells => (_vm?.RoomSize ?? 8) + 2;

    private int TileSize => _vm?.FloorTiles.FirstOrDefault()?.Size ?? _vm?.WallTiles.FirstOrDefault()?.Size ?? 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_vm is null || _vm.IsFit)
        {
            double w = double.IsInfinity(availableSize.Width) ? 512.0 : availableSize.Width;
            double h = double.IsInfinity(availableSize.Height) ? 512.0 : availableSize.Height;
            return new Size(w, h);
        }
        double side = Cells * TileSize * _vm.Scale;
        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        if (_vm is null || TileSize == 0 || _vm.FloorTiles.Count == 0 || _vm.WallTiles.Count == 0)
        {
            return;
        }
        int cells = Cells;
        int tileSize = TileSize;
        double cell = _vm.IsFit ? Math.Floor(Math.Min(Bounds.Width, Bounds.Height) / cells) : tileSize * _vm.Scale;
        if (cell < 1.0)
        {
            return;
        }
        double originX = _vm.IsFit ? Math.Floor((Bounds.Width - cell * cells) / 2.0) : 0.0;
        double originY = _vm.IsFit ? Math.Floor((Bounds.Height - cell * cells) / 2.0) : 0.0;
        var source = new Rect(0, 0, tileSize, tileSize);
        for (int row = 0; row < cells; row++)
        {
            for (int column = 0; column < cells; column++)
            {
                bool isWall = row == 0 || column == 0 || row == cells - 1 || column == cells - 1;
                var tiles = isWall ? _vm.WallTiles : _vm.FloorTiles;
                var tile = tiles[RoomArrangement.VariantAt(row, column, tiles.Count)];
                if (tile.Size != tileSize)
                {
                    continue;
                }
                var dest = new Rect(originX + column * cell, originY + row * cell, cell, cell);
                context.DrawImage(TileBitmaps.Get(tile), source, dest);
            }
        }
    }
}
