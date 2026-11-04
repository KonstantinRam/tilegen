namespace TileGen.Core;

/// <summary>
/// Shades one material at any pixel. The constructor does the per-tile work (cell counts, usable octaves, seeds)
/// so that the per-pixel path is only noise lookups and arithmetic.
/// </summary>
internal sealed class MaterialSampler
{
    private readonly IStructureSampler _structure;
    private readonly Ramp _ramp;
    private readonly int _detailCells;
    private readonly int _detailOctaves;
    private readonly double _detailStrength;
    private readonly ulong _detailSeed;
    private readonly ulong _sharedDetailSeed;
    private readonly double _contrast;
    private readonly double _tint;
    private readonly double _edgeDarkening;
    private readonly bool _cellColors;

    /// <summary>With cell colors the height scales the brightness between this floor and full, so a cell's rim is darker than its top.</summary>
    private const double CellColorFloor = 0.5;

    /// <summary>
    /// The seed is the variant's and the shared seed is the set's. Only the blocks family reads the shared seed, for the
    /// parts of its layout that every variant of a set must agree on along the tile edges.
    /// </summary>
    public MaterialSampler(ResolvedMaterial material, int size, ulong seed, ulong sharedSeed, FeatureFilter? filter = null)
    {
        ulong structureSeed = Hashing.Hash64(seed, 1);
        ulong sharedStructureSeed = Hashing.Hash64(sharedSeed, 1);
        _structure = StructureSamplers.Create(material.Structure, size, structureSeed, sharedStructureSeed, filter);
        _ramp = new Ramp(material.Ramp);
        _detailCells = TileResolver.CellsFromScale(material.DetailScale);
        _detailOctaves = Noise.UsableOctaves(_detailCells, material.DetailOctaves, size);
        _detailStrength = material.DetailStrength;
        _detailSeed = Hashing.Hash64(seed, 2);
        _sharedDetailSeed = Hashing.Hash64(sharedSeed, 2);
        _contrast = material.Contrast;
        // Tint and edge darkening need a cell id and a border, so the field family ignores them as the schema says.
        _tint = _structure.HasCells ? material.Tint : 0.0;
        _edgeDarkening = _structure.HasCells ? material.EdgeDarkening : 0.0;
        _cellColors = material.CellColors && _structure.HasCells;
    }

    /// <summary>Whether the structure places features that the filter can leave out, which tells the generator to mask by feature.</summary>
    public bool HasFeatures => _structure.HasFeatures;

    public void Shade(double u, double v, out double r, out double g, out double b)
    {
        Shade(u, v, out r, out g, out b, out _);
    }

    /// <summary>Presence is 1 where the structure has a feature and 0 on its empty ground or mortar, which a transparent garbage lets the base show through.</summary>
    public void Shade(double u, double v, out double r, out double g, out double b, out double presence)
    {
        StructureSample sample = _structure.Sample(u, v);
        presence = sample.Presence >= 0.0 ? sample.Presence : 1.0 - sample.EdgeWeight;
        double h = sample.Height;
        if (_detailStrength > 0.0)
        {
            // A shared cell takes the set's grain, so the cell that a tile seam cuts looks the same in both neighbors.
            ulong detailSeed = sample.Shared ? _sharedDetailSeed : _detailSeed;
            h += _detailStrength * (Noise.Fbm(u, v, _detailCells, _detailOctaves, 0.5, detailSeed, false) - 0.5);
        }
        h = 0.5 + (h - 0.5) * _contrast;
        if (h < 0.0)
        {
            h = 0.0;
        }
        else if (h > 1.0)
        {
            h = 1.0;
        }
        if (_cellColors)
        {
            // Each cell takes its own ramp color and the height, which is the cell's shape, sets how bright it is. The ground
            // and the mortar between cells take the ramp's color at 0, so a transparent garbage's haze has the ramp's ground
            // color and a pixel on a cell's rim mixes the two by its share of cell.
            _ramp.Evaluate(0.0, out r, out g, out b);
            if (sample.EdgeWeight < 1.0)
            {
                _ramp.Evaluate(sample.CellRandom, out double cr, out double cg, out double cb);
                double brightness = CellColorFloor + (1.0 - CellColorFloor) * h;
                double cellShare = 1.0 - sample.EdgeWeight;
                r += (cr * brightness - r) * cellShare;
                g += (cg * brightness - g) * cellShare;
                b += (cb * brightness - b) * cellShare;
            }
        }
        else
        {
            _ramp.Evaluate(h, out r, out g, out b);
        }

        double factor = 1.0;
        double interior = 1.0 - sample.EdgeWeight;
        if (_tint > 0.0)
        {
            // The tint scales the color, so it also works on a one-stop ramp, and the border keeps its own color.
            factor *= 1.0 + _tint * (sample.CellRandom - 0.5) * interior;
        }
        if (_edgeDarkening > 0.0)
        {
            double s = sample.InteriorDistance / _structure.DarkeningWidth;
            if (s > 1.0)
            {
                s = 1.0;
            }
            factor *= 1.0 - _edgeDarkening * (1.0 - s * (2.0 - s)) * interior;
        }
        if (factor != 1.0)
        {
            r *= factor;
            g *= factor;
            b *= factor;
        }
    }
}
