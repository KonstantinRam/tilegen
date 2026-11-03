namespace TileGen.Core;

/// <summary>Maps a height from 0 to 1 to a color by linear interpolation between sorted stops in sRGB.</summary>
internal sealed class Ramp
{
    private readonly double[] _t;
    private readonly double[] _r;
    private readonly double[] _g;
    private readonly double[] _b;

    public Ramp(IReadOnlyList<RampStop> stops)
    {
        Validate(stops);
        int n = stops.Count;
        _t = new double[n];
        _r = new double[n];
        _g = new double[n];
        _b = new double[n];
        for (int i = 0; i < n; i++)
        {
            _t[i] = stops[i].T;
            _r[i] = stops[i].Color.R;
            _g[i] = stops[i].Color.G;
            _b[i] = stops[i].Color.B;
        }
    }

    /// <summary>Checks the invariants the schema promises, so that a bad ramp fails here with a message instead of rendering garbage.</summary>
    public static void Validate(IReadOnlyList<RampStop> stops)
    {
        if (stops is null || stops.Count == 0)
        {
            throw new ArgumentException("A ramp needs at least one stop.");
        }
        if (stops.Count == 1)
        {
            return;
        }
        if (stops[0].T != 0.0 || stops[^1].T != 1.0)
        {
            throw new ArgumentException("A ramp with more than one stop must start at t 0 and end at t 1.");
        }
        for (int i = 1; i < stops.Count; i++)
        {
            if (stops[i].T < stops[i - 1].T)
            {
                throw new ArgumentException("Ramp stops must be sorted by t.");
            }
        }
    }

    public void Evaluate(double h, out double r, out double g, out double b)
    {
        int n = _t.Length;
        if (n == 1 || h <= _t[0])
        {
            r = _r[0];
            g = _g[0];
            b = _b[0];
            return;
        }
        if (h >= _t[n - 1])
        {
            r = _r[n - 1];
            g = _g[n - 1];
            b = _b[n - 1];
            return;
        }
        int i = 1;
        while (h > _t[i])
        {
            i++;
        }
        double span = _t[i] - _t[i - 1];
        // Two stops at the same t form a hard step, and the later stop wins on its side of the step.
        double f = span > 0.0 ? (h - _t[i - 1]) / span : 1.0;
        r = _r[i - 1] + (_r[i] - _r[i - 1]) * f;
        g = _g[i - 1] + (_g[i] - _g[i - 1]) * f;
        b = _b[i - 1] + (_b[i] - _b[i - 1]) * f;
    }
}
