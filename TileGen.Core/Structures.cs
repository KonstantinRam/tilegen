namespace TileGen.Core;

/// <summary>
/// What a structure family knows about one pixel. Height feeds the ramp. EdgeWeight is 1 inside a border or mortar,
/// 0 inside a cell, and crosses over within one pixel. CellRandom is a per-cell value from the cell id, 0.5 where there
/// are no cells. InteriorDistance is the distance from the border edge into the cell in tile units, for edge darkening.
/// Shared is true where the pixel lies in a cell that every variant of the set draws identically, so that the material
/// can take its detail grain from the set seed there and the cell looks the same on both sides of a tile seam.
/// Presence is 1 where the structure has a feature and 0 on empty ground, which a transparent garbage lets the base
/// show through; a family that leaves it negative gets 1 minus the edge weight, so mortar and borders are the gaps.
/// </summary>
internal readonly record struct StructureSample(double Height, double EdgeWeight, double CellRandom, double InteriorDistance, bool Shared = false, double Presence = -1.0);

/// <summary>
/// Decides whether a free placed feature, a stone or a crystal cluster, centered at the given tile coordinates is drawn,
/// where atEdge says that the feature reaches a tile edge, so every variant of the set must answer alike for it. A
/// transparent garbage passes its mask here, so a feature is whole or absent instead of cut by the mask's outline (ADR-019).
/// </summary>
internal delegate bool FeatureFilter(double u, double v, bool atEdge);

internal interface IStructureSampler
{
    /// <summary>Whether the family has cells, which is what tint and edge darkening need. The field family has none.</summary>
    bool HasCells { get; }

    /// <summary>Whether the family places free standing features that a filter can leave out, which the scatter and crystal families do.</summary>
    bool HasFeatures { get; }

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

    public bool HasFeatures => false;

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

    public bool HasFeatures => false;

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

    public bool HasFeatures => false;

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

/// <summary>
/// The blocks family: coursed masonry with courses of uneven height cut into blocks of uneven width, where some
/// blocks span two courses. The layout is built once per tile. The course heights and the one block per course that
/// crosses the tile's vertical edge come from the set seed, so every variant of a set shows the same course lines and
/// the same cut block at its left and right edges, while the blocks inside the tile come from the variant seed.
/// </summary>
internal sealed class BlocksSampler : IStructureSampler
{
    /// <summary>Blocks differ in height by up to this much, so a wall is not one flat color before tint and detail.</summary>
    private const double HeightVariation = 0.1;

    /// <summary>A free interval shorter than this is floating point residue and receives no block.</summary>
    private const double Sliver = 1e-9;

    /// <summary>The edge block reaches at least this fraction of its width to either side of the edge, so the tile edge is never a joint.</summary>
    private const double MinEdgeReach = 0.15;

    /// <summary>No block is wider than this fraction of the tile, so a course with one block still has a joint.</summary>
    private const double MaxWidth = 0.9;

    /// <summary>X0 lies in the tile and X1 may pass 1 for the edge block only. Y0 and Y1 are course tops and never wrap.</summary>
    private readonly record struct Block(double X0, double X1, double Y0, double Y1, ulong Id);

    private readonly int _rows;
    private readonly double[] _tops;
    private readonly Block[][] _courses;
    private readonly double[][] _starts;
    private readonly double _halfMortar;
    private readonly double _mortarHeight;
    private readonly double _cornerRadius;
    private readonly double _pixel;

    public BlocksSampler(ResolvedBlocks blocks, int size, ulong seed, ulong sharedSeed)
    {
        _rows = blocks.Rows;
        _halfMortar = blocks.MortarWidth * 0.5;
        _mortarHeight = blocks.MortarHeight;
        _cornerRadius = blocks.CornerRadius;
        _pixel = 1.0 / size;
        DarkeningWidth = 0.25 * Math.Min(1.0 / blocks.Rows, 1.0 / blocks.Columns);

        var shared = new Draws(Hashing.Hash64(sharedSeed, 1));
        var own = new Draws(Hashing.Hash64(seed, 1));
        _tops = CourseTops(blocks, shared);
        Block[] edges = EdgeBlocks(blocks, shared);

        _courses = new Block[_rows][];
        _starts = new double[_rows][];
        var carried = new List<Block>[_rows];
        for (int i = 0; i < _rows; i++)
        {
            carried[i] = new List<Block>();
        }
        for (int i = 0; i < _rows; i++)
        {
            // The free part of a course is the span between the two halves of its edge block, minus the tall blocks carried from above.
            var list = new List<Block>();
            double cursor = edges[i].X1 - 1.0;
            foreach (Block above in carried[i])
            {
                Fill(list, cursor, above.X0, i, blocks, edges, own, carried);
                list.Add(above);
                cursor = above.X1;
            }
            Fill(list, cursor, edges[i].X0, i, blocks, edges, own, carried);
            list.Add(edges[i]);
            _courses[i] = list.ToArray();
            _starts[i] = list.Select(b => b.X0).ToArray();
        }
    }

    public bool HasCells => true;

    public bool HasFeatures => false;

    public double DarkeningWidth { get; }

    public StructureSample Sample(double u, double v)
    {
        u -= Math.Floor(u);
        v -= Math.Floor(v);
        int course = 0;
        while (course < _rows - 1 && v >= _tops[course + 1])
        {
            course++;
        }
        Block[] blocks = _courses[course];
        double[] starts = _starts[course];
        int index;
        if (u < starts[0])
        {
            // Left of the first start lies the wrapped half of the edge block, which is the last block of every course.
            index = blocks.Length - 1;
            u += 1.0;
        }
        else
        {
            int lo = 0;
            int hi = blocks.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (starts[mid] <= u)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            index = lo;
        }
        Block block = blocks[index];

        // The signed distance to a rounded box, negative inside, measured from the block border inset by half the mortar.
        double hw = (block.X1 - block.X0) * 0.5 - _halfMortar;
        double hh = (block.Y1 - block.Y0) * 0.5 - _halfMortar;
        double inner;
        if (hw <= 0.0 || hh <= 0.0)
        {
            inner = -_halfMortar;
        }
        else
        {
            double r = Math.Min(_cornerRadius, Math.Min(hw, hh));
            double qx = Math.Abs(u - (block.X0 + block.X1) * 0.5) - (hw - r);
            double qy = Math.Abs(v - (block.Y0 + block.Y1) * 0.5) - (hh - r);
            double px = Math.Max(qx, 0.0);
            double py = Math.Max(qy, 0.0);
            inner = r - Noise.Sqrt(px * px + py * py) - Math.Min(Math.Max(qx, qy), 0.0);
        }
        double edgeWeight = 1.0 - Noise.SmoothStep(inner / _pixel + 0.5);
        if (inner < 0.0)
        {
            inner = 0.0;
        }
        double cellRandom = Hashing.Unit(block.Id);
        double interior = 1.0 - HeightVariation * Hashing.Unit(Hashing.Mix(block.Id));
        double height = interior + (_mortarHeight - interior) * edgeWeight;
        // The edge block is the last entry of every course, and it is the one block all variants of the set share. The mortar
        // is shared as well, because the course lines along the top and bottom edges are mortar in every variant, and the
        // switch between the shared and the variant grain then always lies on a block rim.
        return new StructureSample(height, edgeWeight, cellRandom, inner, index == blocks.Length - 1 || edgeWeight >= 0.5);
    }

    private static double[] CourseTops(ResolvedBlocks blocks, Draws shared)
    {
        var weights = new double[blocks.Rows];
        double total = 0.0;
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = SizeWeight(shared.Next(), blocks.SizeVariation);
            total += weights[i];
        }
        var tops = new double[blocks.Rows + 1];
        for (int i = 0; i < weights.Length; i++)
        {
            tops[i + 1] = tops[i] + weights[i] / total;
        }
        // The summation can miss 1 by a bit, and the last course has to end exactly at the tile edge to wrap.
        tops[blocks.Rows] = 1.0;
        return tops;
    }

    /// <summary>One block per course crosses the vertical tile edge. A tall edge block is the edge block of the next course as well.</summary>
    private Block[] EdgeBlocks(ResolvedBlocks blocks, Draws shared)
    {
        var edges = new Block[_rows];
        double average = 1.0 / blocks.Columns;
        for (int i = 0; i < _rows; i++)
        {
            if (i > 0 && edges[i - 1].Y1 > _tops[i])
            {
                edges[i] = edges[i - 1];
                continue;
            }
            double width = Math.Min(MaxWidth, average * SizeWeight(shared.Next(), blocks.SizeVariation));
            double left = width * (MinEdgeReach + (1.0 - 2.0 * MinEdgeReach) * shared.Next());
            double tallDraw = shared.Next();
            bool tall = i < _rows - 1 && tallDraw < blocks.TallChance;
            double x0 = 1.0 - (width - left);
            edges[i] = new Block(x0, x0 + width, _tops[i], _tops[tall ? i + 2 : i + 1], shared.Next64());
        }
        return edges;
    }

    /// <summary>Cuts one free interval of a course into blocks and lets some of them extend into the course below.</summary>
    private void Fill(List<Block> list, double start, double end, int course, ResolvedBlocks blocks, Block[] edges, Draws own, List<Block>[] carried)
    {
        double length = end - start;
        if (length <= Sliver)
        {
            return;
        }
        int count = Math.Max(1, (int)Math.Round(length * blocks.Columns * CountWeight(own.Next(), blocks.SizeVariation)));
        var weights = new double[count];
        double total = 0.0;
        for (int j = 0; j < count; j++)
        {
            weights[j] = SizeWeight(own.Next(), blocks.SizeVariation);
            total += weights[j];
        }
        double x = start;
        for (int j = 0; j < count; j++)
        {
            // The last block ends exactly at the interval end, so no residue accumulates along the course.
            double x1 = j == count - 1 ? end : x + length * weights[j] / total;
            double tallDraw = own.Next();
            bool tall = course < _rows - 1 && tallDraw < blocks.TallChance && FitsBelow(edges[course + 1], x, x1);
            var block = new Block(x, x1, _tops[course], _tops[tall ? course + 2 : course + 1], own.Next64());
            list.Add(block);
            if (tall)
            {
                carried[course + 1].Add(block);
            }
            x = x1;
        }
    }

    /// <summary>A block may extend into the next course only where that course's edge block leaves room, so nothing overlaps.</summary>
    private static bool FitsBelow(Block edge, double x0, double x1)
    {
        return x0 >= edge.X1 - 1.0 && x1 <= edge.X0;
    }

    /// <summary>A width or height weight from 1 minus 0.8 variation to 1 plus 0.8 variation, so full variation spans a factor of nine.</summary>
    private static double SizeWeight(double draw, double variation)
    {
        return 1.0 + variation * (draw - 0.5) * 1.6;
    }

    /// <summary>The block count of an interval moves half as much as the sizes, so a course keeps about the authored count.</summary>
    private static double CountWeight(double draw, double variation)
    {
        return 1.0 + variation * (draw - 0.5);
    }

    /// <summary>A sequence of hash draws, which replaces a random generator in layout code that runs once per tile (ADR-008).</summary>
    private sealed class Draws
    {
        private readonly ulong _seed;
        private ulong _count;

        public Draws(ulong seed)
        {
            _seed = seed;
        }

        public ulong Next64()
        {
            return Hashing.Hash64(_seed, _count++);
        }

        public double Next()
        {
            return Hashing.Unit(Next64());
        }
    }
}

/// <summary>
/// The timber family: a half-timbered wall of posts and rails around panels of plaster or planks, with optional
/// diagonal braces, pegs and knots. The frame is identical in every variant of a set and takes its grain from the set
/// seed, so a post or rail at the tile edge is the same on both sides of a seam. Panels, braces, boards and knots come
/// from the variant seed, except that a frameless material shares its panels and boards too, because they then reach
/// the tile edges, and only its knots differ between variants.
/// </summary>
internal sealed class TimberSampler : IStructureSampler
{
    /// <summary>The frame sits at this height, so a preset puts its wood colors in the lower part of the ramp.</summary>
    private const double BeamHeight = 0.25;

    /// <summary>At grain 1 the streaks move the height by a quarter either way around the beam height.</summary>
    private const double GrainRange = 0.5;

    private const double PlasterHeight = 1.0;
    private const double PlankHeight = 0.85;
    private const double HeightVariation = 0.1;

    /// <summary>Braces are narrower than posts, as in the reference, and pegs rise above the beam height.</summary>
    private const double BraceWidthFactor = 0.8;
    private const double PegLift = 0.15;

    /// <summary>The groove between two boards as a share of the board width.</summary>
    private const double GrooveFactor = 0.08;

    private const double KnotChance = 0.4;
    private const double KnotRadiusFactor = 0.3;
    private const double KnotDepth = 0.3;

    /// <summary>A knot is an oval along the grain, this much narrower across it and longer along it.</summary>
    private const double KnotAcross = 0.75;
    private const double KnotAlong = 1.5;

    /// <summary>
    /// This share of the boards has a middle piece between two plank ends. The pieces at the top and bottom keep the
    /// board's identity, which is shared in a frameless material, so the piece that differs per variant never reaches a tile edge.
    /// </summary>
    private const double JointChance = 0.6;
    private const double JointEndShare = 0.15;
    private const double JointMinMiddleShare = 0.3;

    /// <summary>Grain cells per tile across and along the wood. Few cells along and many across give streaks.</summary>
    private const int GrainAcrossCells = 48;
    private const int GrainAlongCells = 3;

    /// <summary>A beam wider than this share of the panel would leave no panel, so the width is capped.</summary>
    private const double MaxBeamShare = 0.9;

    private readonly int _rows;
    private readonly int _columns;
    private readonly double _panelWidth;
    private readonly double _panelHeight;
    private readonly double _halfBeam;
    private readonly double _inset;
    private readonly double _braceHalf;
    private readonly double _pegRadius;
    private readonly double _grainAmplitude;
    private readonly double _pixel;
    private readonly bool _frame;
    private readonly bool _planks;
    private readonly bool _shared;
    private readonly ulong _postGrainSeed;
    private readonly ulong _railGrainSeed;
    private readonly ulong _panelSeed;
    private readonly ulong _braceSeed;
    private readonly int[] _braces;
    private readonly double _diagonalX;
    private readonly double _diagonalY;
    private readonly int _boards;
    private readonly double _boardWidth;
    private readonly double _grooveHalf;
    private readonly ulong[] _boardIds;
    private readonly ulong[] _middleIds;
    private readonly double[] _jointTop;
    private readonly double[] _jointBottom;
    private readonly double[] _knotX;
    private readonly double[] _knotY;
    private readonly double[] _knotR;

    public TimberSampler(ResolvedTimber timber, int size, ulong seed, ulong sharedSeed)
    {
        _rows = timber.Rows;
        _columns = timber.Columns;
        _panelWidth = 1.0 / _columns;
        _panelHeight = 1.0 / _rows;
        double beamWidth = Math.Min(timber.BeamWidth, MaxBeamShare * Math.Min(_panelWidth, _panelHeight));
        _frame = beamWidth > 0.0;
        _halfBeam = beamWidth * 0.5;
        _inset = _frame ? _halfBeam : 0.0;
        _braceHalf = beamWidth * BraceWidthFactor * 0.5;
        _pegRadius = Math.Min(timber.PegSize * 0.5, _halfBeam);
        _grainAmplitude = timber.Grain * GrainRange;
        _planks = timber.Planks;
        _pixel = 1.0 / size;
        DarkeningWidth = Math.Max(beamWidth * 0.75, 0.02);

        // Without a frame nothing covers the tile edges, so the panels and boards must come from the set seed.
        _shared = !_frame;
        ulong infillSeed = _shared ? sharedSeed : seed;
        _postGrainSeed = Hashing.Hash64(sharedSeed, 1);
        _railGrainSeed = Hashing.Hash64(sharedSeed, 2);
        _panelSeed = Hashing.Hash64(infillSeed, 3);
        _braceSeed = Hashing.Hash64(seed, 4);

        double diagonal = Noise.Sqrt(_panelWidth * _panelWidth + _panelHeight * _panelHeight);
        _diagonalX = _panelWidth / diagonal;
        _diagonalY = _panelHeight / diagonal;

        int panels = _rows * _columns;
        _braces = new int[panels];
        if (_frame && timber.BraceChance > 0.0)
        {
            for (int p = 0; p < panels; p++)
            {
                ulong draw = Hashing.Hash64(_braceSeed, (ulong)p);
                if (Hashing.Unit(draw) < timber.BraceChance)
                {
                    _braces[p] = (Hashing.Mix(draw) & 1UL) == 0UL ? 1 : 2;
                }
            }
        }

        double insetWidth = _panelWidth - 2.0 * _inset;
        double insetHeight = _panelHeight - 2.0 * _inset;
        _boards = _planks ? Math.Max(1, (int)Math.Round(insetWidth / timber.PlankWidth)) : 1;
        _boardWidth = insetWidth / _boards;
        _grooveHalf = _boardWidth * GrooveFactor * 0.5;
        int count = panels * _boards;
        _boardIds = new ulong[count];
        _middleIds = new ulong[count];
        _jointTop = new double[count];
        _jointBottom = new double[count];
        _knotX = new double[count];
        _knotY = new double[count];
        _knotR = new double[count];
        if (_planks)
        {
            ulong boardSeed = Hashing.Hash64(infillSeed, 5);
            ulong knotSeed = Hashing.Hash64(seed, 6);
            ulong jointSeed = Hashing.Hash64(seed, 7);
            double radius = _boardWidth * KnotRadiusFactor;
            double margin = 1.5 * radius;
            for (int i = 0; i < count; i++)
            {
                _boardIds[i] = Hashing.Hash64(boardSeed, (ulong)i);
                ulong joint = Hashing.Hash64(jointSeed, (ulong)i);
                if (Hashing.Unit(joint) < JointChance)
                {
                    double top = insetHeight * (JointEndShare + (1.0 - 2.0 * JointEndShare - JointMinMiddleShare) * Hashing.Unit(Hashing.Hash64(joint, 1)));
                    double longest = insetHeight * (1.0 - JointEndShare) - top;
                    _jointTop[i] = top;
                    _jointBottom[i] = top + insetHeight * JointMinMiddleShare + (longest - insetHeight * JointMinMiddleShare) * Hashing.Unit(Hashing.Hash64(joint, 2));
                    _middleIds[i] = Hashing.Hash64(joint, 3);
                }
                ulong draw = Hashing.Hash64(knotSeed, (ulong)i);
                // A knot keeps its own radius and a half away from every board edge, so no knot reaches a groove or a tile edge.
                if (Hashing.Unit(draw) < KnotChance && _boardWidth > 2.0 * margin && insetHeight > 2.0 * margin)
                {
                    int b = i % _boards;
                    _knotX[i] = b * _boardWidth + margin + (_boardWidth - 2.0 * margin) * Hashing.Unit(Hashing.Hash64(draw, 1));
                    _knotY[i] = margin + (insetHeight - 2.0 * margin) * Hashing.Unit(Hashing.Hash64(draw, 2));
                    _knotR[i] = radius;
                }
            }
        }
    }

    public bool HasCells => true;

    public bool HasFeatures => false;

    public double DarkeningWidth { get; }

    public StructureSample Sample(double u, double v)
    {
        u -= Math.Floor(u);
        v -= Math.Floor(v);
        double fx = u * _columns;
        double fy = v * _rows;
        double floorX = Math.Floor(fx);
        double floorY = Math.Floor(fy);
        // Signed distances to the nearest post and rail center lines, in tile units.
        double dxPost = (fx - Math.Floor(fx + 0.5)) / _columns;
        double dyRail = (fy - Math.Floor(fy + 0.5)) / _rows;
        double adx = Math.Abs(dxPost);
        double ady = Math.Abs(dyRail);

        // The panel the pixel lies in, and the pixel's offset from the panel's top left frame lines and inset corner.
        int k = Noise.Wrap((int)floorX, _columns);
        int j = Noise.Wrap((int)floorY, _rows);
        int panel = j * _columns + k;
        double px = (fx - floorX) / _columns;
        double py = (fy - floorY) / _rows;
        double lx = px - _inset;
        double ly = py - _inset;
        ulong panelId = Hashing.Lattice(_panelSeed, k, j);
        double cellRandom = Hashing.Unit(panelId);
        double interior = _frame ? Math.Min(adx, ady) - _halfBeam : 1.0;
        double height;
        double edge = 0.0;

        if (_planks)
        {
            int b = (int)Math.Floor(lx / _boardWidth);
            if (b < 0)
            {
                b = 0;
            }
            else if (b >= _boards)
            {
                b = _boards - 1;
            }
            double bx = lx - b * _boardWidth;
            double groove = Math.Min(bx, _boardWidth - bx) - _grooveHalf;
            int i = panel * _boards + b;
            ulong pieceId = _boardIds[i];
            if (_jointBottom[i] > 0.0)
            {
                // Between its two plank ends a board is another piece, with its own tint and grain.
                if (ly >= _jointTop[i] && ly < _jointBottom[i])
                {
                    pieceId = _middleIds[i];
                }
                groove = Math.Min(groove, Math.Min(Math.Abs(ly - _jointTop[i]), Math.Abs(ly - _jointBottom[i])) - _grooveHalf);
            }
            double board = PlankHeight - HeightVariation * Hashing.Unit(Hashing.Mix(pieceId)) + _grainAmplitude * (Grain(u, v, pieceId) - 0.5);
            double r = _knotR[i];
            if (r > 0.0)
            {
                double kx = (lx - _knotX[i]) / KnotAcross;
                double ky = (ly - _knotY[i]) / KnotAlong;
                double d = Noise.Sqrt(kx * kx + ky * ky);
                board -= KnotDepth * (1.0 - Noise.SmoothStep((d - 0.5 * r) / (0.5 * r)));
            }
            double grooveWeight = 1.0 - Noise.SmoothStep(groove / _pixel + 0.5);
            height = board + (BeamHeight - board) * grooveWeight;
            edge = grooveWeight;
            cellRandom = Hashing.Unit(pieceId);
            interior = Math.Min(interior, groove);
        }
        else
        {
            height = PlasterHeight - HeightVariation * Hashing.Unit(Hashing.Mix(panelId));
        }

        int brace = _braces[panel];
        if (brace != 0)
        {
            // The brace runs corner to corner of the panel's frame lines, so its ends hide under the posts and rails.
            double qx = brace == 1 ? px : _panelWidth - px;
            double across = qx * _diagonalY - py * _diagonalX;
            double along = qx * _diagonalX + py * _diagonalY;
            double distance = Math.Abs(across) - _braceHalf;
            double weight = 1.0 - Noise.SmoothStep(distance / _pixel + 0.5);
            if (weight > 0.0)
            {
                double grain = Grain(across, along, Hashing.Hash64(_braceSeed, 0x1000UL + (ulong)panel));
                double wood = BeamHeight + _grainAmplitude * (grain - 0.5);
                height += (wood - height) * weight;
                edge = Math.Max(edge, weight);
            }
            interior = Math.Min(interior, distance);
        }

        bool shared = _shared;
        if (_frame)
        {
            double distance = Math.Min(adx, ady) - _halfBeam;
            double weight = 1.0 - Noise.SmoothStep(distance / _pixel + 0.5);
            if (weight > 0.0)
            {
                // Posts run over rails at a crossing, and the pegs sit on the posts just beside every rail.
                bool post = adx <= ady;
                double grain = post ? Grain(u, v, _postGrainSeed) : Grain(v, u, _railGrainSeed);
                double wood = BeamHeight + _grainAmplitude * (grain - 0.5);
                if (post && _pegRadius > 0.0)
                {
                    double dy = ady - (_halfBeam + 2.0 * _pegRadius);
                    double d = Noise.Sqrt(dxPost * dxPost + dy * dy);
                    wood += PegLift * (1.0 - Noise.SmoothStep((d - _pegRadius) / _pixel + 0.5));
                }
                height += (wood - height) * weight;
                edge = Math.Max(edge, weight);
                shared = weight >= 0.5;
            }
        }

        if (interior < 0.0)
        {
            interior = 0.0;
        }
        return new StructureSample(height, edge, cellRandom, interior, shared);
    }

    /// <summary>Wood grain: value noise with few cells along the second coordinate and many across, folded into streaks, in two octaves.</summary>
    private static double Grain(double across, double along, ulong seed)
    {
        double n1 = Noise.Value(across, along, GrainAcrossCells, GrainAlongCells, seed);
        double n2 = Noise.Value(across, along, GrainAcrossCells * 2, GrainAlongCells * 2, Hashing.Hash64(seed, 1));
        double r1 = 1.0 - Math.Abs(2.0 * n1 - 1.0);
        double r2 = 1.0 - Math.Abs(2.0 * n2 - 1.0);
        return 0.65 * r1 + 0.35 * r2;
    }
}

/// <summary>
/// The scatter family: rounded stones, mounds or pebbles of varied size on a ground, in up to three layers of
/// decreasing size, where overlapping stones show the taller one. The layout is built once per tile. A stone that
/// touches the tile edge is drawn from the set seed with the authored density and size, so every variant of a set shows
/// the same stones along its edges, and the ground is shared as well. The stones inside the tile come from the variant
/// seed with the variant's own density and size.
/// </summary>
internal sealed class ScatterSampler : IStructureSampler
{
    public const int MaxLayers = 3;

    /// <summary>A radius under one lattice cell keeps every stone that can reach a pixel inside the 3 by 3 neighborhood of its cell.</summary>
    private const double MaxRadiusCells = 0.95;

    /// <summary>At full size variation a radius moves by up to 60 percent of itself either way.</summary>
    private const double VariationRange = 1.2;

    /// <summary>Each further layer has stones of half the size and this share of the density, so pebbles do not bury the mounds.</summary>
    private const double LayerDensityFactor = 0.75;

    /// <summary>At warp 1 a pixel moves by half a coarse cell before it looks up its stones, which turns circles into blobs.</summary>
    private const double WarpPerCell = 0.5;
    private const int WarpOctaves = 2;

    /// <summary>A stone with a radius of 0 is absent. X and Y lie in the tile, and a stone near an edge also reaches the opposite edge.</summary>
    private readonly record struct Stone(double X, double Y, double R, ulong Id, bool Shared, bool Star);

    /// <summary>A star is two spikes along the axes, this wide across as a share of the radius, with a round core of this share of the radius.</summary>
    private const double StarSpikeWidth = 0.2;
    private const double StarCore = 0.35;

    private readonly int[] _counts;
    private readonly Stone[][] _layers;
    private readonly double _ground;
    private readonly double _pixel;
    private readonly double _warpAmplitude;
    private readonly int _warpCells;
    private readonly int _warpOctaves;
    private readonly ulong _warpSeedU;
    private readonly ulong _warpSeedV;

    public ScatterSampler(ResolvedScatter scatter, int size, ulong seed, ulong sharedSeed, FeatureFilter? filter = null)
    {
        _ground = scatter.GroundHeight;
        _pixel = 1.0 / size;
        // The warp field comes from the set seed, because it moves the shared stones along the edges as well.
        _warpAmplitude = scatter.Warp * WarpPerCell / scatter.Count;
        _warpCells = scatter.Count * 2;
        _warpOctaves = Noise.UsableOctaves(_warpCells, WarpOctaves, size);
        _warpSeedU = Hashing.Hash64(sharedSeed, 0x57415250UL);
        _warpSeedV = Hashing.Hash64(sharedSeed, 0x57415251UL);
        // A stone counts as touching the edge when its disc, moved by the warp, could cover the outermost pixel centers.
        double margin = _pixel + _warpAmplitude;
        // The authored size sets the darkening width, because a width that moved with a variant's size would change the shared edge stones.
        DarkeningWidth = 0.5 * scatter.SharedSize / scatter.Count;
        _counts = new int[scatter.Layers];
        _layers = new Stone[scatter.Layers][];
        for (int layer = 0; layer < scatter.Layers; layer++)
        {
            int n = scatter.Count << layer;
            double densityFactor = 1.0;
            for (int k = 0; k < layer; k++)
            {
                densityFactor *= LayerDensityFactor;
            }
            ulong sharedLayerSeed = Hashing.Hash64(sharedSeed, (ulong)layer + 1);
            ulong ownLayerSeed = Hashing.Hash64(seed, (ulong)layer + 1);
            var stones = new Stone[n * n];
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    Stone shared = Make(Hashing.Lattice(sharedLayerSeed, i, j), i, j, n, scatter.SharedDensity * densityFactor, scatter.SharedSize, scatter.SharedSizeVariation, scatter.Jitter, scatter.Stars, true);
                    Stone own = Make(Hashing.Lattice(ownLayerSeed, i, j), i, j, n, scatter.Density * densityFactor, scatter.Size, scatter.SizeVariation, scatter.Jitter, scatter.Stars, false);
                    // Whatever touches the edge must be the shared stone, so a variant's own stone yields to it there even when
                    // the shared stone is absent, because the edge pixels of every variant then agree.
                    bool atEdge = Touches(shared, margin);
                    Stone chosen = atEdge || Touches(own, margin) ? shared : own;
                    // A stone the filter leaves out is absent from this tile altogether, so it never wins a pixel from a stone that is
                    // drawn, and only a stone that reaches the edge needs the verdict every variant shares.
                    stones[j * n + i] = chosen.R > 0.0 && filter != null && !filter(chosen.X, chosen.Y, atEdge) ? chosen with { R = 0.0 } : chosen;
                }
            }
            _counts[layer] = n;
            _layers[layer] = stones;
        }
    }

    public bool HasCells => true;

    public bool HasFeatures => true;

    public double DarkeningWidth { get; }

    public StructureSample Sample(double u, double v)
    {
        if (_warpAmplitude > 0.0)
        {
            // The warp fields have the tile's period, so the warped lookup still wraps.
            double du = Noise.Fbm(u, v, _warpCells, _warpOctaves, 0.5, _warpSeedU, false) - 0.5;
            double dv = Noise.Fbm(u, v, _warpCells, _warpOctaves, 0.5, _warpSeedV, false) - 0.5;
            u += _warpAmplitude * du;
            v += _warpAmplitude * dv;
        }
        u -= Math.Floor(u);
        v -= Math.Floor(v);
        bool hit = false;
        double best = _ground;
        double rim = 0.0;
        double presence = 0.0;
        double cellRandom = 0.5;
        bool shared = true;
        for (int layer = 0; layer < _layers.Length; layer++)
        {
            int n = _counts[layer];
            Stone[] stones = _layers[layer];
            int ci = (int)Math.Floor(u * n);
            int cj = (int)Math.Floor(v * n);
            for (int dj = -1; dj <= 1; dj++)
            {
                int nj = cj + dj;
                double offsetY = nj < 0 ? -1.0 : nj >= n ? 1.0 : 0.0;
                int wj = Noise.Wrap(nj, n);
                for (int di = -1; di <= 1; di++)
                {
                    int ni = ci + di;
                    double offsetX = ni < 0 ? -1.0 : ni >= n ? 1.0 : 0.0;
                    int wi = Noise.Wrap(ni, n);
                    Stone stone = stones[wj * n + wi];
                    if (stone.R <= 0.0)
                    {
                        continue;
                    }
                    double dx = u - (stone.X + offsetX);
                    double dy = v - (stone.Y + offsetY);
                    double q = dx * dx + dy * dy;
                    if (q >= stone.R * stone.R)
                    {
                        continue;
                    }
                    double d = Noise.Sqrt(q);
                    double shape;
                    if (stone.Star)
                    {
                        shape = StarShape(Math.Abs(dx), Math.Abs(dy), d, stone.R);
                        if (shape <= 0.0)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        // The dome rises fast at the rim and flattens at the top, so a stone reads as rounded with a dark outline.
                        double t = 1.0 - d / stone.R;
                        shape = t * (2.0 - t);
                    }
                    double height = _ground + (1.0 - _ground) * shape;
                    if (!hit || height > best)
                    {
                        hit = true;
                        best = height;
                        presence = shape;
                        rim = stone.Star ? shape * stone.R : stone.R - d;
                        // A star takes the ramp's end color, which a sparkle preset makes white, and a stone its own value.
                        cellRandom = stone.Star ? 1.0 : Hashing.Unit(stone.Id);
                        shared = stone.Shared;
                    }
                }
            }
        }
        if (!hit)
        {
            return new StructureSample(_ground, 1.0, 0.5, 0.0, true, 0.0);
        }
        double edgeWeight = 1.0 - Noise.SmoothStep(rim / _pixel + 0.5);
        return new StructureSample(best, edgeWeight, cellRandom, rim, shared, presence);
    }

    /// <summary>Two soft spikes along the axes and a round core, each falling to nothing at the radius, so a glint stays inside its stone's reach.</summary>
    private static double StarShape(double ax, double ay, double d, double r)
    {
        double alongX = 1.0 - ax / r;
        double alongY = 1.0 - ay / r;
        double acrossX = 1.0 - ay / (StarSpikeWidth * r);
        double acrossY = 1.0 - ax / (StarSpikeWidth * r);
        double spikeX = alongX > 0.0 && acrossX > 0.0 ? alongX * alongX * acrossX : 0.0;
        double spikeY = alongY > 0.0 && acrossY > 0.0 ? alongY * alongY * acrossY : 0.0;
        double core = 1.0 - d / (StarCore * r);
        core = core > 0.0 ? core * core : 0.0;
        return Math.Max(Math.Max(spikeX, spikeY), core);
    }

    private static Stone Make(ulong hash, int i, int j, int n, double density, double size, double variation, double jitter, double stars, bool shared)
    {
        if (Hashing.Unit(hash) >= density)
        {
            return new Stone(0.0, 0.0, 0.0, hash, shared, false);
        }
        double x = (i + 0.5 + jitter * (Hashing.Unit(Hashing.Hash64(hash, 1)) - 0.5)) / n;
        double y = (j + 0.5 + jitter * (Hashing.Unit(Hashing.Hash64(hash, 2)) - 0.5)) / n;
        double radiusCells = size * (1.0 + variation * (Hashing.Unit(Hashing.Hash64(hash, 3)) - 0.5) * VariationRange);
        if (radiusCells > MaxRadiusCells)
        {
            radiusCells = MaxRadiusCells;
        }
        bool star = stars > 0.0 && Hashing.Unit(Hashing.Hash64(hash, 4)) < stars;
        return new Stone(x, y, radiusCells / n, hash, shared, star);
    }

    private static bool Touches(Stone stone, double margin)
    {
        double reach = stone.R + margin;
        return stone.R > 0.0 && (stone.X - reach < 0.0 || stone.X + reach > 1.0 || stone.Y - reach < 0.0 || stone.Y + reach > 1.0);
    }
}

/// <summary>Builds the sampler of a resolved structure. The columns family calls it for the structure behind its columns.</summary>
internal static class StructureSamplers
{
    public static IStructureSampler Create(ResolvedStructure structure, int size, ulong seed, ulong sharedSeed, FeatureFilter? filter = null)
    {
        return structure switch
        {
            ResolvedField f => new FieldSampler(f, size, seed),
            ResolvedCells c => new CellsSampler(c, size, seed),
            ResolvedPattern p => new PatternSampler(p, size, seed),
            ResolvedBlocks b => new BlocksSampler(b, size, seed, sharedSeed),
            ResolvedTimber t => new TimberSampler(t, size, seed, sharedSeed),
            ResolvedScatter sc => new ScatterSampler(sc, size, seed, sharedSeed, filter),
            ResolvedColumns co => new ColumnsSampler(co, size, seed, sharedSeed),
            ResolvedCrystal cr => new CrystalSampler(cr, size, seed, sharedSeed, filter),
            _ => throw new NotSupportedException($"Unknown structure {structure.GetType().Name}."),
        };
    }
}

/// <summary>
/// The columns family: round columns with a capital and a base standing in front of another structure. The structure
/// behind keeps its own rules and takes the lower part of the height range, and the columns take the upper part, so one
/// ramp carries both the wall and the marble. A column runs from the top edge to the bottom edge of the tile and never
/// reaches a vertical edge, and it is the same in every variant of a set, so a seam never cuts it into two columns.
/// </summary>
internal sealed class ColumnsSampler : IStructureSampler
{
    /// <summary>The structure behind is scaled into 0 to BehindTop, and the columns run from ColumnBottom to 1.</summary>
    public const double BehindTop = 0.45;
    public const double ColumnBottom = 0.55;

    /// <summary>The abacus on the capital and the plinth under the base take this share of their zone and this flat shading.</summary>
    private const double BlockShare = 0.35;
    private const double BlockShading = 0.82;
    private const double BlockWidthFactor = 1.05;

    /// <summary>Flutes cut this deep into the shaft shading.</summary>
    private const double FluteDepth = 0.35;

    /// <summary>The widest part of a column keeps this share of its slot, so neighboring columns never meet.</summary>
    private const double SlotShare = 0.95;

    private const ulong BehindSeedIndex = 11;

    private readonly IStructureSampler _behind;
    private readonly int _count;
    private readonly double _halfWidth;
    private readonly double _capital;
    private readonly double _base;
    private readonly double _flare;
    private readonly int _flutes;
    private readonly double _pixel;
    private readonly ulong _idSeed;

    public ColumnsSampler(ResolvedColumns columns, int size, ulong seed, ulong sharedSeed)
    {
        _behind = StructureSamplers.Create(columns.Behind, size, Hashing.Hash64(seed, BehindSeedIndex), Hashing.Hash64(sharedSeed, BehindSeedIndex));
        _count = columns.Count;
        _flare = columns.Flare;
        double widest = 0.5 / _count * SlotShare / ((1.0 + _flare) * BlockWidthFactor);
        _halfWidth = Math.Min(columns.Width * 0.5, widest);
        _capital = columns.Capital;
        _base = columns.Base;
        _flutes = columns.Flutes;
        _pixel = 1.0 / size;
        // The column ids come from the set seed, so a column's tint is the same in the tile above and below.
        _idSeed = Hashing.Hash64(sharedSeed, 1);
        DarkeningWidth = _behind.DarkeningWidth;
    }

    public bool HasCells => true;

    public bool HasFeatures => false;

    public double DarkeningWidth { get; }

    public StructureSample Sample(double u, double v)
    {
        u -= Math.Floor(u);
        v -= Math.Floor(v);
        StructureSample behind = _behind.Sample(u, v);
        double wallHeight = behind.Height * BehindTop;

        int k = (int)Math.Floor(u * _count);
        if (k >= _count)
        {
            k = _count - 1;
        }
        double axis = (k + 0.5) / _count;
        double dx = Math.Abs(u - axis);

        // The silhouette half width at this height: the shaft, a flared capital under an abacus, a flared base over a plinth.
        double half = _halfWidth;
        bool block = false;
        if (_capital > 0.0 && v < _capital)
        {
            Silhouette(v / _capital, ref half, ref block);
        }
        else if (_base > 0.0 && v > 1.0 - _base)
        {
            Silhouette((1.0 - v) / _base, ref half, ref block);
        }

        double distance = dx - half;
        double weight = 1.0 - Noise.SmoothStep(distance / _pixel + 0.5);
        if (weight <= 0.0)
        {
            // The wall next to a column darkens like a shadow, through the interior distance that edge darkening reads.
            return new StructureSample(wallHeight, behind.EdgeWeight, behind.CellRandom, Math.Min(behind.InteriorDistance, distance), behind.Shared);
        }

        double shading;
        if (block)
        {
            shading = BlockShading;
        }
        else
        {
            // A round shaft: full height on the axis and falling to nothing at the silhouette, with no light direction.
            double x = dx / half;
            shading = Noise.Sqrt(1.0 - x * x);
            if (_flutes > 0)
            {
                double across = ((u - axis) / half + 1.0) * 0.5 * _flutes;
                double frac = across - Math.Floor(across);
                shading *= 1.0 - FluteDepth * (1.0 - Math.Abs(2.0 * frac - 1.0));
            }
        }
        double columnHeight = ColumnBottom + (1.0 - ColumnBottom) * shading;
        double height = wallHeight + (columnHeight - wallHeight) * weight;
        // Along the silhouette a pixel is part column and part wall. Tint and darkening apply to the column's share in
        // full and to the wall's share only where the wall is cell interior, and the cell value mixes in the same
        // proportion, so a mortar pixel next to a column depends on nothing drawn from the variant seed.
        double wallInterior = 1.0 - behind.EdgeWeight;
        double interiorWeight = wallInterior * (1.0 - weight) + weight;
        double columnRandom = Hashing.Unit(Hashing.Hash64(_idSeed, (ulong)k));
        double cellRandom = 0.5 + (wallInterior * (1.0 - weight) * (behind.CellRandom - 0.5) + weight * (columnRandom - 0.5)) / interiorWeight;
        bool column = weight >= 0.5;
        double interior = column ? -distance : Math.Min(behind.InteriorDistance, distance);
        if (interior < 0.0)
        {
            interior = 0.0;
        }
        return new StructureSample(height, 1.0 - interiorWeight, cellRandom, interior, column || behind.Shared);
    }

    /// <summary>Widens the silhouette inside a capital or base zone, where t runs from 0 at the tile edge to 1 at the shaft.</summary>
    private void Silhouette(double t, ref double half, ref bool block)
    {
        if (t < BlockShare)
        {
            half = _halfWidth * (1.0 + _flare) * BlockWidthFactor;
            block = true;
        }
        else
        {
            double f = 1.0 - (t - BlockShare) / (1.0 - BlockShare);
            half = _halfWidth * (1.0 + _flare * f * f);
        }
    }
}

/// <summary>
/// The crystal family: clusters of pointed prisms radiating from scattered centers. Each shard has a ridge along its
/// length and two faces that darken toward its sides, so it reads as a protruding crystal without a light direction.
/// The layout is built once per tile, and a cluster that could reach a tile edge is the one drawn from the set seed
/// with the authored density and size, so every variant of a set agrees along its edges, as in the scatter family.
/// </summary>
internal sealed class CrystalSampler : IStructureSampler
{
    public const int MaxShards = 6;

    /// <summary>A shard and its root corners stay under one lattice cell from the cluster center, so the 3 by 3 neighborhood holds every shard that can reach a pixel.</summary>
    private const double MaxLengthCells = 0.85;

    /// <summary>The edge test keeps this many pixels beyond the drawn region, which covers the outermost pixel centers and the rim pixel the sampler draws.</summary>
    private const double EdgeMarginPixels = 2.0;

    /// <summary>At full size variation a length moves by up to 60 percent of itself either way.</summary>
    private const double VariationRange = 1.2;

    /// <summary>The pointed tip takes this share of the length, and the shard starts this share of its length behind the center so the cluster has no hole.</summary>
    private const double TipShare = 0.3;
    private const double RootShare = 0.2;

    /// <summary>The height rises by this much from the root to the tip, so the tips read as the highest points.</summary>
    private const double TipLift = 0.15;

    /// <summary>The flat top face covers this share of the half width, and the side faces begin after a short bevel, so a shard reads as a prism.</summary>
    private const double TopFaceShare = 0.35;
    private const double BevelShare = 0.3;

    /// <summary>
    /// At full facet one side face loses this much height and the other this much less, and which side is darker is drawn
    /// per shard, so the faces differ like those of a real crystal without a light direction shared by every shard.
    /// </summary>
    private const double SideDarkening = 1.0;
    private const double SideContrast = 0.4;

    /// <summary>A dark knob of rock under every cluster, this share of the longest shard across, at half height, which the shards grow out of.</summary>
    private const double KnobShare = 0.45;
    private const double KnobHeight = 0.5;

    private readonly record struct Shard(double X, double Y, double DirX, double DirY, double Length, double HalfWidth, ulong Id, bool Shared);

    private readonly record struct Knob(double X, double Y, double R, bool Shared);

    private readonly int _count;
    private readonly int[] _first;
    private readonly int[] _shardCount;
    private readonly Shard[] _shards;
    private readonly Knob[] _knobs;
    private readonly double _facet;
    private readonly double _pixel;

    public CrystalSampler(ResolvedCrystal crystal, int size, ulong seed, ulong sharedSeed, FeatureFilter? filter = null)
    {
        _count = crystal.Count;
        _facet = crystal.Facet;
        _pixel = 1.0 / size;
        DarkeningWidth = 0.5 * crystal.SharedAspect * crystal.SharedSize / crystal.Count;

        int n = _count;
        var shards = new List<Shard>();
        _first = new int[n * n];
        _shardCount = new int[n * n];
        _knobs = new Knob[n * n];
        ulong sharedCellSeed = Hashing.Hash64(sharedSeed, 1);
        ulong ownCellSeed = Hashing.Hash64(seed, 1);
        var scratch = new List<Shard>(MaxShards);
        var own = new List<Shard>(MaxShards);
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                scratch.Clear();
                own.Clear();
                Make(Hashing.Lattice(sharedCellSeed, i, j), i, j, crystal.SharedDensity, crystal.Shards, crystal.SharedSize, crystal.SharedAspect, crystal.SharedSizeVariation, true, scratch);
                ulong ownHash = Hashing.Lattice(ownCellSeed, i, j);
                Make(ownHash, i, j, crystal.Density, crystal.Shards, crystal.Size, crystal.Aspect, crystal.SizeVariation, false, own);
                List<Shard> chosen;
                Knob knob;
                bool atEdge = Touches(scratch);
                if (atEdge)
                {
                    // The shared cluster reaches the edge, so its center, its knob and the shards that reach the edge are the set's,
                    // and every other shard is drawn again from the variant seed at the same center, so the cluster still differs
                    // between variants instead of repeating in every tile of a room.
                    knob = KnobUnder(scratch);
                    Redraw(scratch, ownHash, crystal);
                    chosen = scratch;
                }
                else
                {
                    // The variant's own cluster yields to the shared one wherever it could reach the edge, even when the shared one is absent.
                    chosen = Touches(own) ? scratch : own;
                    knob = KnobUnder(chosen);
                }
                // A cluster the filter leaves out is absent from this tile altogether, so it never wins a pixel from a cluster that is drawn.
                if (chosen.Count > 0 && filter != null && !filter(knob.X, knob.Y, atEdge))
                {
                    chosen.Clear();
                }
                int cell = j * n + i;
                _first[cell] = shards.Count;
                _shardCount[cell] = chosen.Count;
                shards.AddRange(chosen);
                if (chosen.Count > 0)
                {
                    _knobs[cell] = knob;
                }
            }
        }
        _shards = shards.ToArray();
    }

    public bool HasCells => true;

    public bool HasFeatures => true;

    public double DarkeningWidth { get; }

    public StructureSample Sample(double u, double v)
    {
        u -= Math.Floor(u);
        v -= Math.Floor(v);
        int n = _count;
        int ci = (int)Math.Floor(u * n);
        int cj = (int)Math.Floor(v * n);
        bool hit = false;
        double best = 0.0;
        double rim = 0.0;
        ulong id = 0;
        bool shared = true;
        bool knobHit = false;
        double knobBest = 0.0;
        double knobRim = 0.0;
        bool knobShared = true;
        for (int dj = -1; dj <= 1; dj++)
        {
            int nj = cj + dj;
            double offsetY = nj < 0 ? -1.0 : nj >= n ? 1.0 : 0.0;
            int wj = Noise.Wrap(nj, n);
            for (int di = -1; di <= 1; di++)
            {
                int ni = ci + di;
                double offsetX = ni < 0 ? -1.0 : ni >= n ? 1.0 : 0.0;
                int wi = Noise.Wrap(ni, n);
                int cell = wj * n + wi;
                int end = _first[cell] + _shardCount[cell];
                if (_shardCount[cell] > 0)
                {
                    // The knob under the cluster shows only where no shard covers it, so it is tracked apart from the shards.
                    Knob knob = _knobs[cell];
                    double kx = u - (knob.X + offsetX);
                    double ky = v - (knob.Y + offsetY);
                    double kq = kx * kx + ky * ky;
                    if (kq < knob.R * knob.R)
                    {
                        double kd = Noise.Sqrt(kq);
                        double kt = 1.0 - kd / knob.R;
                        double dome = kt * (2.0 - kt);
                        if (!knobHit || dome > knobBest)
                        {
                            knobHit = true;
                            knobBest = dome;
                            knobRim = knob.R - kd;
                            knobShared = knob.Shared;
                        }
                    }
                }
                for (int k = _first[cell]; k < end; k++)
                {
                    Shard shard = _shards[k];
                    double px = u - (shard.X + offsetX);
                    double py = v - (shard.Y + offsetY);
                    // The shard's own frame: along runs from the root through the center to the tip, across is signed distance from the ridge.
                    double along = px * shard.DirX + py * shard.DirY;
                    double signedAcross = py * shard.DirX - px * shard.DirY;
                    double across = Math.Abs(signedAcross);
                    double root = -RootShare * shard.Length;
                    if (along < root - _pixel || along > shard.Length + _pixel)
                    {
                        continue;
                    }
                    double tipStart = shard.Length * (1.0 - TipShare);
                    double halfAt = along > tipStart ? shard.HalfWidth * (shard.Length - along) / (shard.Length - tipStart) : shard.HalfWidth;
                    double inside = Math.Min(halfAt - across, Math.Min(along - root, shard.Length - along));
                    if (inside <= -_pixel)
                    {
                        continue;
                    }
                    double ridge = halfAt > 0.0 ? Math.Min(across / halfAt, 1.0) : 1.0;
                    double rise = (along - root) / (shard.Length - root);
                    if (rise < 0.0)
                    {
                        rise = 0.0;
                    }
                    else if (rise > 1.0)
                    {
                        rise = 1.0;
                    }
                    // A flat top face, a short bevel and two side faces of unequal darkness, a little higher toward the tip, so a shard reads as a prism.
                    double side = Noise.SmoothStep((ridge - TopFaceShare) / BevelShare);
                    bool darkSide = (signedAcross > 0.0) == ((shard.Id & 1UL) == 0UL);
                    double darkening = _facet * (darkSide ? SideDarkening : SideDarkening - SideContrast);
                    double height = (1.0 - darkening * side) * (1.0 - TipLift + TipLift * rise);
                    if (height < 0.05)
                    {
                        height = 0.05;
                    }
                    // A pixel fully inside a shard beats the rim of another, so an overlap never shows the other's dark outline.
                    double key = inside >= 0.0 ? height + 1.0 : height;
                    if (!hit || key > best)
                    {
                        hit = true;
                        best = key;
                        rim = inside;
                        id = shard.Id;
                        shared = shard.Shared;
                    }
                }
            }
        }
        if (!hit)
        {
            if (!knobHit)
            {
                return new StructureSample(0.0, 1.0, 0.5, 0.0, true);
            }
            // The knob is a cell with the ramp's bottom value, so with cell colors it is darker rock and never a crystal color,
            // and its edge weight follows its dome, so it fades out like a shadow instead of ending in a circle.
            return new StructureSample(KnobHeight * knobBest, 1.0 - knobBest, 0.0, knobRim, knobShared);
        }
        double edgeWeight = 1.0 - Noise.SmoothStep(rim / _pixel + 0.5);
        if (rim < 0.0)
        {
            rim = 0.0;
        }
        if (best >= 1.0)
        {
            best -= 1.0;
        }
        return new StructureSample(best, edgeWeight, Hashing.Unit(id), rim, shared);
    }

    /// <summary>Builds one cluster of shards for a cell, or none when the density draw fails, into the given list.</summary>
    private void Make(ulong hash, int i, int j, double density, int maxShards, double size, double aspect, double variation, bool shared, List<Shard> into)
    {
        if (Hashing.Unit(hash) >= density)
        {
            return;
        }
        int n = _count;
        double x = (i + Hashing.Unit(Hashing.Hash64(hash, 1))) / n;
        double y = (j + Hashing.Unit(Hashing.Hash64(hash, 2))) / n;
        // A cluster has at least two shards, because one shard on a knob reads as a splinter and not as a crystal.
        int shards = maxShards < 2 ? maxShards : 2 + (int)Math.Floor(Hashing.Unit(Hashing.Hash64(hash, 3)) * (maxShards - 1));
        if (shards > maxShards)
        {
            shards = maxShards;
        }
        for (int k = 0; k < shards; k++)
        {
            into.Add(MakeShard(Hashing.Hash64(hash, 10UL + (ulong)k), x, y, size, aspect, variation, shared));
        }
    }

    private Shard MakeShard(ulong h, double x, double y, double size, double aspect, double variation, bool shared)
    {
        // A random direction as a normalized random vector, which needs no trigonometry.
        double dx = Hashing.Unit(Hashing.Hash64(h, 1)) - 0.5;
        double dy = Hashing.Unit(Hashing.Hash64(h, 2)) - 0.5;
        double norm = Noise.Sqrt(dx * dx + dy * dy);
        if (norm < 1e-9)
        {
            dx = 1.0;
            dy = 0.0;
        }
        else
        {
            dx /= norm;
            dy /= norm;
        }
        double lengthCells = size * (1.0 + variation * (Hashing.Unit(Hashing.Hash64(h, 3)) - 0.5) * VariationRange);
        if (lengthCells > MaxLengthCells)
        {
            lengthCells = MaxLengthCells;
        }
        double length = lengthCells / _count;
        return new Shard(x, y, dx, dy, length, aspect * length * 0.5, h, shared);
    }

    /// <summary>
    /// Replaces every shard of a shared cluster that stays clear of the tile edge with a shard drawn from the variant seed
    /// at the same center, and keeps the shared shard where the variant's would reach the edge, so the cluster keeps its
    /// seam and loses its repeat.
    /// </summary>
    private void Redraw(List<Shard> cluster, ulong ownHash, ResolvedCrystal crystal)
    {
        for (int k = 0; k < cluster.Count; k++)
        {
            Shard shared = cluster[k];
            if (Reaches(shared))
            {
                continue;
            }
            Shard alt = MakeShard(Hashing.Hash64(ownHash, 40UL + (ulong)k), shared.X, shared.Y, crystal.Size, crystal.Aspect, crystal.SizeVariation, false);
            if (!Reaches(alt))
            {
                cluster[k] = alt;
            }
        }
    }

    /// <summary>The knob under a cluster, sized by its longest shard, or an absent one for an empty cluster.</summary>
    private static Knob KnobUnder(List<Shard> cluster)
    {
        if (cluster.Count == 0)
        {
            return default;
        }
        double longest = 0.0;
        foreach (Shard shard in cluster)
        {
            longest = Math.Max(longest, shard.Length);
        }
        return new Knob(cluster[0].X, cluster[0].Y, longest * KnobShare, cluster[0].Shared);
    }

    /// <summary>
    /// Whether any pixel of the cluster, with the knob under it and the rim pixel the sampler draws around a shard, can lie
    /// in the outermost pixel ring of the tile. Only such a cluster has to be the shared one, and the extents follow the
    /// shard directions, so a cluster near an edge whose shards point inward stays the variant's own and the variants
    /// differ in more of the tile.
    /// </summary>
    private bool Touches(List<Shard> cluster)
    {
        foreach (Shard shard in cluster)
        {
            if (Reaches(shard))
            {
                return true;
            }
        }
        Knob knob = KnobUnder(cluster);
        double margin = EdgeMarginPixels * _pixel;
        return knob.R > 0.0 && (knob.X - knob.R < margin || knob.X + knob.R > 1.0 - margin || knob.Y - knob.R < margin || knob.Y + knob.R > 1.0 - margin);
    }

    /// <summary>
    /// Whether the shard, with the rim pixel the sampler draws around it, can reach the outermost pixel ring. The drawn
    /// region lies inside a rectangle of the shard's frame, so the rectangle's corners bound the shard in the tile.
    /// </summary>
    private bool Reaches(Shard shard)
    {
        double alongMin = -RootShare * shard.Length - _pixel;
        double alongMax = shard.Length + _pixel;
        double across = shard.HalfWidth + _pixel;
        double alongX0 = Math.Min(alongMin * shard.DirX, alongMax * shard.DirX);
        double alongX1 = Math.Max(alongMin * shard.DirX, alongMax * shard.DirX);
        double alongY0 = Math.Min(alongMin * shard.DirY, alongMax * shard.DirY);
        double alongY1 = Math.Max(alongMin * shard.DirY, alongMax * shard.DirY);
        double acrossX = across * Math.Abs(shard.DirY);
        double acrossY = across * Math.Abs(shard.DirX);
        double margin = EdgeMarginPixels * _pixel;
        return shard.X + alongX0 - acrossX < margin || shard.X + alongX1 + acrossX > 1.0 - margin
            || shard.Y + alongY0 - acrossY < margin || shard.Y + alongY1 + acrossY > 1.0 - margin;
    }
}
