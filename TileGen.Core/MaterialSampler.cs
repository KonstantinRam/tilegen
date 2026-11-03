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
    private readonly double _contrast;
    private readonly double _tint;
    private readonly double _edgeDarkening;

    public MaterialSampler(ResolvedMaterial material, int size, ulong seed)
    {
        ulong structureSeed = Hashing.Hash64(seed, 1);
        _structure = material.Structure switch
        {
            ResolvedField f => new FieldSampler(f, size, structureSeed),
            ResolvedCells c => new CellsSampler(c, size, structureSeed),
            ResolvedPattern p => new PatternSampler(p, size, structureSeed),
            _ => throw new NotSupportedException($"Unknown structure {material.Structure.GetType().Name}."),
        };
        _ramp = new Ramp(material.Ramp);
        _detailCells = TileResolver.CellsFromScale(material.DetailScale);
        _detailOctaves = Noise.UsableOctaves(_detailCells, material.DetailOctaves, size);
        _detailStrength = material.DetailStrength;
        _detailSeed = Hashing.Hash64(seed, 2);
        _contrast = material.Contrast;
        // Tint and edge darkening need a cell id and a border, so the field family ignores them as the schema says.
        _tint = _structure.HasCells ? material.Tint : 0.0;
        _edgeDarkening = _structure.HasCells ? material.EdgeDarkening : 0.0;
    }

    public void Shade(double u, double v, out double r, out double g, out double b)
    {
        StructureSample sample = _structure.Sample(u, v);
        double h = sample.Height;
        if (_detailStrength > 0.0)
        {
            h += _detailStrength * (Noise.Fbm(u, v, _detailCells, _detailOctaves, 0.5, _detailSeed, false) - 0.5);
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
        _ramp.Evaluate(h, out r, out g, out b);

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
