using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TileGen.Core;

namespace TileGen.App.Views;

/// <summary>Bridges the generator's color type and Avalonia's, so the view models never reference Avalonia.</summary>
public sealed class RgbToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Rgb rgb ? Color.FromRgb(rgb.R, rgb.G, rgb.B) : Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Color c ? new Rgb(c.R, c.G, c.B) : null;
    }
}

/// <summary>The fit view fills its panel, so the scroll bars go away, and the zoomed views scroll.</summary>
public sealed class FitToScrollConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
