namespace TileGen.Core;

/// <summary>
/// A numeric preset parameter. A parameter without a range is fixed, so jitter and randomize never move it.
/// A parameter with a range may move within that range, which is how the preset author decides which knobs variants touch.
/// </summary>
public readonly struct Param
{
    public double Value { get; }
    public double? Min { get; }
    public double? Max { get; }

    public bool HasRange => Min.HasValue;

    public Param(double value)
    {
        Value = value;
        Min = null;
        Max = null;
    }

    public Param(double value, double min, double max)
    {
        if (min > max)
        {
            throw new ArgumentException($"Parameter range has min {min} above max {max}.");
        }
        if (value < min || value > max)
        {
            throw new ArgumentException($"Parameter value {value} lies outside its range {min} to {max}.");
        }
        Value = value;
        Min = min;
        Max = max;
    }

    public static implicit operator Param(double value) => new(value);

    public override string ToString() => HasRange ? $"{Value} [{Min}, {Max}]" : Value.ToString();
}
