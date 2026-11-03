namespace TileGen.Core;

/// <summary>An opaque sRGB color with 8 bits per channel, which is the only color type the output format carries.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";
}

/// <summary>A color ramp stop. The ramp maps a height from 0 to 1 to a color, and the stops are sorted by T.</summary>
public readonly record struct RampStop(double T, Rgb Color);
