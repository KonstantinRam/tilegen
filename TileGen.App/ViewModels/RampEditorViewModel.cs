using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileGen.Core;

namespace TileGen.App.ViewModels;

public sealed partial class RampStopViewModel : ObservableObject
{
    [ObservableProperty]
    private double _t;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hex))]
    private Rgb _color;

    public string Hex => Color.ToString();
}

/// <summary>
/// The color ramp of one material. Stops stay sorted by t, the first and last stop stay at 0 and 1 while there are
/// at least two, and the ramp never drops below one stop, which is what the schema promises the generator.
/// </summary>
public sealed partial class RampEditorViewModel : ObservableObject
{
    private readonly Action<IReadOnlyList<RampStop>> _apply;
    private bool _suppress;

    public RampEditorViewModel(IReadOnlyList<RampStop> ramp, Action<IReadOnlyList<RampStop>> apply)
    {
        _apply = apply;
        Refresh(ramp);
    }

    public ObservableCollection<RampStopViewModel> Stops { get; } = new();

    /// <summary>Raised after every change, so the ramp bar redraws without watching every stop.</summary>
    public event Action? Changed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedStop))]
    [NotifyPropertyChangedFor(nameof(SelectedColor))]
    [NotifyPropertyChangedFor(nameof(SelectedHex))]
    [NotifyPropertyChangedFor(nameof(SelectedT))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    private int _selectedIndex;

    public RampStopViewModel? SelectedStop => SelectedIndex >= 0 && SelectedIndex < Stops.Count ? Stops[SelectedIndex] : null;

    public bool CanRemove => Stops.Count > 1;

    public Rgb? SelectedColor
    {
        get => SelectedStop?.Color;
        set
        {
            if (value.HasValue && SelectedStop is { } stop && stop.Color != value.Value)
            {
                stop.Color = value.Value;
                Commit();
            }
        }
    }

    public string SelectedHex
    {
        get => SelectedStop?.Hex ?? "";
        set
        {
            if (TryParseHex(value, out Rgb color))
            {
                SelectedColor = color;
            }
        }
    }

    /// <summary>Spinners bind to a nullable decimal, so the position is exposed in that shape.</summary>
    public decimal? SelectedT
    {
        get => (decimal)(SelectedStop?.T ?? 0.0);
        set
        {
            if (value.HasValue)
            {
                MoveStop(SelectedIndex, (double)value.Value);
            }
        }
    }

    public IReadOnlyList<RampStop> ToRamp()
    {
        return Stops.Select(s => new RampStop(s.T, s.Color)).ToList();
    }

    public void Refresh(IReadOnlyList<RampStop> ramp)
    {
        _suppress = true;
        try
        {
            Stops.Clear();
            foreach (var stop in ramp)
            {
                Stops.Add(new RampStopViewModel { T = stop.T, Color = stop.Color });
            }
            SelectedIndex = Math.Clamp(SelectedIndex, 0, Stops.Count - 1);
        }
        finally
        {
            _suppress = false;
        }
        NotifySelection();
    }

    [RelayCommand]
    private void AddStop()
    {
        var current = SelectedStop ?? Stops[^1];
        int index = Stops.IndexOf(current);
        double t;
        Rgb color = current.Color;
        if (index < Stops.Count - 1)
        {
            t = (current.T + Stops[index + 1].T) / 2.0;
            Stops.Insert(index + 1, new RampStopViewModel { T = t, Color = color });
            SelectedIndex = index + 1;
        }
        else if (Stops.Count == 1)
        {
            // A one-stop ramp has no span yet, so the second stop opens the full range.
            Stops[0].T = 0.0;
            Stops.Add(new RampStopViewModel { T = 1.0, Color = color });
            SelectedIndex = 1;
        }
        else
        {
            t = (Stops[index - 1].T + current.T) / 2.0;
            Stops.Insert(index, new RampStopViewModel { T = t, Color = color });
            SelectedIndex = index;
        }
        Commit();
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void RemoveStop()
    {
        if (Stops.Count <= 1)
        {
            return;
        }
        int index = Math.Clamp(SelectedIndex, 0, Stops.Count - 1);
        Stops.RemoveAt(index);
        if (Stops.Count > 1)
        {
            Stops[0].T = 0.0;
            Stops[^1].T = 1.0;
        }
        SelectedIndex = Math.Min(index, Stops.Count - 1);
        Commit();
    }

    /// <summary>Moves a stop between its neighbors. The ends stay at 0 and 1, so the ramp keeps covering the whole height range.</summary>
    public void MoveStop(int index, double t)
    {
        if (index < 0 || index >= Stops.Count || Stops.Count < 2)
        {
            return;
        }
        if (index == 0 || index == Stops.Count - 1)
        {
            return;
        }
        double min = Stops[index - 1].T;
        double max = Stops[index + 1].T;
        double clamped = Math.Clamp(t, min, max);
        if (Stops[index].T != clamped)
        {
            Stops[index].T = clamped;
            Commit();
        }
    }

    public static bool TryParseHex(string text, out Rgb color)
    {
        color = default;
        string s = text.Trim().TrimStart('#');
        if (s.Length == 6
            && byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)
            && byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)
            && byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            color = new Rgb(r, g, b);
            return true;
        }
        return false;
    }

    private void Commit()
    {
        NotifySelection();
        if (!_suppress)
        {
            _apply(ToRamp());
        }
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedStop));
        OnPropertyChanged(nameof(SelectedColor));
        OnPropertyChanged(nameof(SelectedHex));
        OnPropertyChanged(nameof(SelectedT));
        OnPropertyChanged(nameof(CanRemove));
        RemoveStopCommand.NotifyCanExecuteChanged();
        Changed?.Invoke();
    }

    partial void OnSelectedIndexChanged(int value)
    {
        Changed?.Invoke();
    }
}
