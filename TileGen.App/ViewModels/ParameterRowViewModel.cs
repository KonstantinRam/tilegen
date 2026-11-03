using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TileGen.Core;

namespace TileGen.App.ViewModels;

/// <summary>
/// One numeric parameter row: label, slider or spinner, text box and lock. The row knows the preset's current range,
/// so an edit keeps the range unless the value leaves it, in which case the range grows to include the value.
/// </summary>
public sealed partial class ParameterRowViewModel : ObservableObject
{
    private readonly Action<ParameterRowViewModel> _apply;
    private bool _suppress;
    private double? _rangeMin;
    private double? _rangeMax;

    public ParameterRowViewModel(ParameterDescriptor descriptor, Action<ParameterRowViewModel> apply)
    {
        _apply = apply;
        Path = descriptor.Path;
        Label = descriptor.Label;
        IsInteger = descriptor.IsInteger;
        Minimum = descriptor.AbsoluteMin;
        Maximum = descriptor.AbsoluteMax;
        Refresh(descriptor.Param);
    }

    public string Path { get; }
    public string Label { get; }
    public bool IsInteger { get; }
    public bool IsContinuous => !IsInteger;
    public double Minimum { get; }
    public double Maximum { get; }
    public double Step => IsInteger ? 1.0 : (Maximum - Minimum) / 1000.0;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private string _valueText = "";

    /// <summary>Locked means the short form: the parameter never jitters and never randomizes.</summary>
    [ObservableProperty]
    private bool _isLocked;

    public string RangeText => _rangeMin.HasValue ? $"range {Format(_rangeMin.Value)} to {Format(_rangeMax!.Value)}" : "fixed";

    /// <summary>Spinners bind to a nullable decimal, so the integer rows expose the value in that shape.</summary>
    public decimal? NumericValue
    {
        get => (decimal)Value;
        set
        {
            if (value.HasValue)
            {
                Value = (double)value.Value;
            }
        }
    }

    public void Refresh(Param param)
    {
        _suppress = true;
        try
        {
            _rangeMin = param.Min;
            _rangeMax = param.Max;
            IsLocked = !param.HasRange;
            Value = param.Value;
            ValueText = Format(Value);
            OnPropertyChanged(nameof(NumericValue));
        }
        finally
        {
            _suppress = false;
        }
        OnPropertyChanged(nameof(RangeText));
    }

    public Param ToParam()
    {
        double v = Math.Clamp(Value, Minimum, Maximum);
        if (IsLocked)
        {
            return new Param(v);
        }
        double min = Math.Min(_rangeMin ?? Minimum, v);
        double max = Math.Max(_rangeMax ?? Maximum, v);
        return new Param(v, min, max);
    }

    partial void OnValueChanged(double value)
    {
        double clamped = Math.Clamp(IsInteger ? Math.Round(value) : value, Minimum, Maximum);
        if (clamped != value)
        {
            Value = clamped;
            return;
        }
        ValueText = Format(clamped);
        OnPropertyChanged(nameof(NumericValue));
        if (!_suppress)
        {
            _apply(this);
        }
    }

    partial void OnValueTextChanged(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed != Value)
        {
            Value = parsed;
        }
    }

    partial void OnIsLockedChanged(bool value)
    {
        if (_suppress)
        {
            return;
        }
        // Unlocking gives the full schema bounds as the range, because the GUI has no range editor (docs/GUI.md).
        if (value)
        {
            _rangeMin = null;
            _rangeMax = null;
        }
        else
        {
            _rangeMin = Minimum;
            _rangeMax = Maximum;
        }
        OnPropertyChanged(nameof(RangeText));
        _apply(this);
    }

    private string Format(double v)
    {
        return IsInteger ? ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture) : v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
