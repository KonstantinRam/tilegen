namespace TileGen.Core;

/// <summary>
/// What a structure family knows about one pixel. Height feeds the ramp. EdgeWeight is 1 inside a border or mortar,
/// 0 inside a cell, and crosses over within one pixel. CellRandom is a per-cell value from the cell id, 0.5 where there
/// are no cells. InteriorDistance is the distance from the border edge into the cell in tile units, for edge darkening.
/// </summary>
internal readonly record struct StructureSample(double Height, double EdgeWeight, double CellRandom, double InteriorDistance);

internal interface IStructureSampler
{
    /// <summary>Whether the family has cells, which is what tint and edge darkening need. The field family has none.</summary>
    bool HasCells { get; }

    /// <summary>The distance over which edge darkening fades into the cell, in tile units.</summary>
    double DarkeningWidth { get; }

    StructureSample Sample(double u, double v);
}

/// <summary>The field family: warped, optionally ridged fractal value noise with an integer cell count per tile.</summary>
internal sealed class FieldSampler : IStructureSampler
{
    private const int WarpOctaves = 2;

    private readonly int _cells;
    private readonly int _octaves;
    private readonly double _gain;
    private readonly bool _ridged;
    private readonly ulong _seed;
    private readonly double _warpAmplitude;
    private readonly int _warpOctaves;
    private readonly ulong _warpSeedU;
    private readonly ulong _warpSeedV;

    public FieldSampler(ResolvedField field, int size, ulong seed)
    {
        _cells = field.Cells;
        _octaves = Noise.UsableOctaves(field.Cells, field.Octaves, size);
        _gain = field.Gain;
        _ridged = field.Ridged;
        _seed = Hashing.Hash64(seed, 1);
        // At warp 1 a point may move by one lattice cell of the first octave, which is far enough to bend every feature.
        _warpAmplitude = 2.0 * field.Warp / field.Cells;
        _warpOctaves = Noise.UsableOctaves(field.Cells, WarpOctaves, size);
        _warpSeedU = Hashing.Hash64(seed, 2);
        _warpSeedV = Hashing.Hash64(seed, 3);
    }

    public bool HasCells => false;

    public double DarkeningWidth => 1.0;

    public StructureSample Sample(double u, double v)
    {
        if (_warpAmplitude > 0.0)
        {
            // The warp fields have the same period as the tile, so the warped field still wraps.
            double du = Noise.Fbm(u, v, _cells, _warpOctaves, 0.5, _warpSeedU, false) - 0.5;
            double dv = Noise.Fbm(u, v, _cells, _warpOctaves, 0.5, _warpSeedV, false) - 0.5;
            u += _warpAmplitude * du;
            v += _warpAmplitude * dv;
        }
        double height = Noise.Fbm(u, v, _cells, _octaves, _gain, _seed, _ridged);
        return new StructureSample(height, 0.0, 0.5, 1.0);
    }
}

/// <summary>
/// The cells family: Worley noise on a jittered lattice. Every feature point stays inside its own lattice cell, so the
/// nearest points of any pixel lie in the 3 by 3 neighborhood, and the lattice indices wrap, so the cells tile.
/// </summary>
internal sealed class CellsSampler : IStructureSampler
{
    private readonly int _count;
    private readonly double _jitter;
    private readonly double _halfEdge;
    private readonly double _edgeHeight;
    private readonly double _halfCell;
    private readonly double _pixel;
    private readonly ulong _seedX;
    private readonly ulong _seedY;
    private readonly ulong _seedId;

    public CellsSampler(ResolvedCells cells, int size, ulong seed)
    {
        _count = cells.Count;
        _jitter = cells.Jitter;
        _halfEdge = cells.EdgeWidth * 0.5;
        _edgeHeight = cells.EdgeHeight;
        _halfCell = 0.5 / cells.Count;
        _pixel = 1.0 / size;
        _seedX = Hashing.Hash64(seed, 1);
        _seedY = Hashing.Hash64(seed, 2);
        _seedId = Hashing.Hash64(seed, 3);
        DarkeningWidth = 0.25 / cells.Count;
    }

    public bool HasCells => true;

    public double DarkeningWidth { get; }

    public StructureSample Sample(double u, double v)
    {
        double fx = u * _count;
        double fy = v * _count;
        int cx = (int)Math.Floor(fx);
        int cy = (int)Math.Floor(fy);

        double d1 = double.MaxValue;
        double d2 = double.MaxValue;
        double p1x = 0.0;
        double p1y = 0.0;
        double p2x = 0.0;
        double p2y = 0.0;
        int id1x = 0;
        int id1y = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                // The hash takes the wrapped index, while the position keeps the unwrapped one, so distances stay continuous across the tile edge.
                int wx = Noise.Wrap(nx, _count);
                int wy = Noise.Wrap(ny, _count);
                double px = nx + 0.5 + _jitter * (Hashing.Unit(Hashing.Lattice(_seedX, wx, wy)) - 0.5);
                double py = ny + 0.5 + _jitter * (Hashing.Unit(Hashing.Lattice(_seedY, wx, wy)) - 0.5);
                double ddx = fx - px;
                double ddy = fy - py;
                double d = ddx * ddx + ddy * ddy;
                if (d < d1)
                {
                    d2 = d1;
                    p2x = p1x;
                    p2y = p1y;
                    d1 = d;
                    p1x = px;
                    p1y = py;
                    id1x = wx;
                    id1y = wy;
                }
                else if (d < d2)
                {
                    d2 = d;
                    p2x = px;
                    p2y = py;
                }
            }
        }

        // The distance to the bisector of the two nearest points is (d2 - d1) / (2 |p2 - p1|) with d the squared distances,
        // which is the distance to the cell border wherever that bisector is the nearest Voronoi edge.
        double sx = p2x - p1x;
        double sy = p2y - p1y;
        double separation = Noise.Sqrt(sx * sx + sy * sy);
        double border = separation > 0.0 ? (d2 - d1) / (2.0 * separation) / _count : 0.0;

        double edgeWeight = 1.0 - Noise.SmoothStep((border - _halfEdge) / _pixel + 0.5);
        double inner = border - _halfEdge;
        if (inner < 0.0)
        {
            inner = 0.0;
        }
        double reach = _halfCell - _halfEdge;
        double s = reach > 0.0 ? inner / reach : 1.0;
        if (s > 1.0)
        {
            s = 1.0;
        }
        // The dome rises fast at the border and flattens at the center, so a cell reads as a rounded stone.
        double dome = s * (2.0 - s);
        double interior = _edgeHeight + (1.0 - _edgeHeight) * dome;
        double height = interior + (_edgeHeight - interior) * edgeWeight;
        double cellRandom = Hashing.Unit(Hashing.Lattice(_seedId, id1x, id1y));
        return new StructureSample(height, edgeWeight, cellRandom, inner);
    }
}

/// <summary>
/// The pattern family: rows and columns of cells with mortar between them and every second row shifted.
/// The counts are integers per tile and the row index wraps, so the layout tiles by construction.
/// </summary>
internal sealed class PatternSampler : IStructureSampler
{
    /// <summary>Cells differ in height by up to this much, so a wall of blocks is not one flat color before tint and detail.</summary>
    private const double HeightVariation = 0.1;

    private readonly int _rows;
    private readonly int _columns;
    private readonly double _rowShift;
    private readonly double _halfMortar;
    private readonly double _mortarHeight;
    private readonly double _pixel;
    private readonly ulong _seedId;

    public PatternSampler(ResolvedPattern pattern, int size, ulong seed)
    {
        _rows = pattern.Rows;
        _columns = pattern.Columns;
        _rowShift = pattern.RowOffset / pattern.Columns;
        _halfMortar = pattern.MortarWidth * 0.5;
        _mortarHeight = pattern.MortarHeight;
        _pixel = 1.0 / size;
        _seedId = Hashing.Hash64(seed, 1);
        DarkeningWidth = 0.25 * Math.Min(1.0 / pattern.Rows, 1.0 / pattern.Columns);
    }

    public bool HasCells => true;

    public double DarkeningWidth { get; }

    public StructureSample Sample(double u, double v)
    {
        double fy = v * _rows;
        double rowFloor = Math.Floor(fy);
        double ty = fy - rowFloor;
        int row = Noise.Wrap((int)rowFloor, _rows);

        // Odd rows shift by the offset, and the wrapped row index decides, so the shift pattern has the tile's period.
        double shifted = u + ((row & 1) == 1 ? _rowShift : 0.0);
        double fx = shifted * _columns;
        double colFloor = Math.Floor(fx);
        double tx = fx - colFloor;
        int col = Noise.Wrap((int)colFloor, _columns);

        double dx = Math.Min(tx, 1.0 - tx) / _columns;
        double dy = Math.Min(ty, 1.0 - ty) / _rows;
        double d = Math.Min(dx, dy);
        double edgeWeight = 1.0 - Noise.SmoothStep((d - _halfMortar) / _pixel + 0.5);
        double inner = d - _halfMortar;
        if (inner < 0.0)
        {
            inner = 0.0;
        }

        ulong cell = Hashing.Lattice(_seedId, col, row);
        double cellRandom = Hashing.Unit(cell);
        double interior = 1.0 - HeightVariation * Hashing.Unit(Hashing.Mix(cell));
        double height = interior + (_mortarHeight - interior) * edgeWeight;
        return new StructureSample(height, edgeWeight, cellRandom, inner);
    }
}
