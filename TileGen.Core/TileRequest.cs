namespace TileGen.Core;

/// <summary>
/// Everything Generator.Render needs for one tile. A favorite and a settings.json variant are stored requests,
/// so this record is the unit of reproduction. Seed is the variant's seed and SetSeed is the base seed of the set,
/// which the garbage mask uses so that garbage agrees along the edges of every variant in the set.
/// </summary>
public sealed record TileRequest(
    Preset Preset,
    TileKind Kind,
    ParameterSet Parameters,
    double GarbageCoverage,
    ulong Seed,
    int Size,
    ulong SetSeed)
{
    /// <summary>A request for the preset's authored values with the seed doubling as the set seed, which is the state a preset is first shown in.</summary>
    public static TileRequest WithDefaults(Preset preset, TileKind kind, ulong seed, int size)
    {
        var type = preset.Get(kind);
        return new TileRequest(preset, kind, ParameterSet.Defaults(type), type.GarbageCoverage, seed, size, seed);
    }
}
