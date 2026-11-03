using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TileGen.App.ViewModels;

namespace TileGen.App.Views;

/// <summary>The gradient bar with one swatch per stop. Clicking selects a stop and dragging moves it between its neighbors.</summary>
public sealed class RampBar : Control
{
    private const double Pad = 8.0;
    private const double BarHeight = 22.0;
    private const double SwatchWidth = 14.0;
    private const double SwatchHeight = 16.0;

    private RampEditorViewModel? _vm;
    private int _dragIndex = -1;

    public RampBar()
    {
        Height = BarHeight + SwatchHeight + 6.0;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.Changed -= InvalidateVisual;
        }
        _vm = DataContext as RampEditorViewModel;
        if (_vm is not null)
        {
            _vm.Changed += InvalidateVisual;
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_vm is null || _vm.Stops.Count == 0)
        {
            return;
        }
        double width = Math.Max(1.0, Bounds.Width - 2.0 * Pad);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        if (_vm.Stops.Count == 1)
        {
            var only = ToColor(_vm.Stops[0].Color);
            brush.GradientStops.Add(new GradientStop(only, 0.0));
            brush.GradientStops.Add(new GradientStop(only, 1.0));
        }
        else
        {
            foreach (var stop in _vm.Stops)
            {
                brush.GradientStops.Add(new GradientStop(ToColor(stop.Color), stop.T));
            }
        }
        context.FillRectangle(brush, new Rect(Pad, 0, width, BarHeight));

        for (int i = 0; i < _vm.Stops.Count; i++)
        {
            var stop = _vm.Stops[i];
            double cx = Pad + stop.T * width;
            var rect = new Rect(cx - SwatchWidth / 2.0, BarHeight + 3.0, SwatchWidth, SwatchHeight);
            context.FillRectangle(new SolidColorBrush(ToColor(stop.Color)), rect);
            bool selected = i == _vm.SelectedIndex;
            context.DrawRectangle(new Pen(selected ? Brushes.White : Brushes.Gray, selected ? 2.0 : 1.0), rect);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_vm is null)
        {
            return;
        }
        double x = e.GetPosition(this).X;
        double width = Math.Max(1.0, Bounds.Width - 2.0 * Pad);
        int best = -1;
        double bestDistance = SwatchWidth;
        for (int i = 0; i < _vm.Stops.Count; i++)
        {
            double d = Math.Abs(Pad + _vm.Stops[i].T * width - x);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        if (best >= 0)
        {
            _vm.SelectedIndex = best;
            _dragIndex = best;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_vm is null || _dragIndex < 0)
        {
            return;
        }
        double width = Math.Max(1.0, Bounds.Width - 2.0 * Pad);
        double t = Math.Clamp((e.GetPosition(this).X - Pad) / width, 0.0, 1.0);
        _vm.MoveStop(_dragIndex, t);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragIndex >= 0)
        {
            _dragIndex = -1;
            e.Pointer.Capture(null);
        }
    }

    private static Color ToColor(Core.Rgb c)
    {
        return Color.FromRgb(c.R, c.G, c.B);
    }
}
