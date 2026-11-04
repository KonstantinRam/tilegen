namespace TileGen.Core;

/// <summary>
/// One setting as authored in a preset file. The model carries ranges on parameters, because the ranges decide
/// which parameters variants may move, and it is stored as a snapshot in every export so that old exports stay reproducible.
/// </summary>
public sealed record Preset
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public int Variants { get; init; } = 4;
    public double Jitter { get; init; } = 0.1;
    public required TileType Floor { get; init; }
    public required TileType Wall { get; init; }

    public TileType Get(TileKind kind) => kind == TileKind.Floor ? Floor : Wall;
}

/// <summary>The material pair of one tile type plus the garbage mask settings.</summary>
public sealed record TileType
{
    public required Material Base { get; init; }
    public required Material Garbage { get; init; }
    /// <summary>The default coverage from 0 to 1. The GUI slider starts here, and the value is never jittered.</summary>
    public double GarbageCoverage { get; init; }
    /// <summary>The size of one lattice cell of the coverage mask as a fraction of the tile.</summary>
    public Param GarbageScale { get; init; } = 0.25;
    /// <summary>The width of the blend band around the threshold as a fraction of the mask field's range.</summary>
    public Param GarbageSoftness { get; init; } = 0.05;
}

/// <summary>A structure, a color ramp and a detail layer, plus the per-cell modifiers that only cell-based families use.</summary>
public sealed record Material
{
    public required Structure Structure { get; init; }
    public required IReadOnlyList<RampStop> Ramp { get; init; }
    public Detail Detail { get; init; } = new();
    public Param Tint { get; init; } = 0.0;
    public Param Contrast { get; init; } = 1.0;
    public Param EdgeDarkening { get; init; } = 0.0;

    /// <summary>
    /// How far the base material shows through where this material's structure has no feature, from 0 (opaque, the
    /// default) to 1. Only a garbage material reads it, and it turns a scatter of sparkles into dust over the floor (ADR-017).
    /// </summary>
    public Param Transparency { get; init; } = 0.0;

    /// <summary>True reads the ramp by the cell's random value instead of the height, so every cell has its own color and the height sets its brightness.</summary>
    public bool CellColors { get; init; }
}

/// <summary>Grain noise added to the structure height before the ramp, so that flat regions are not flat.</summary>
public sealed record Detail
{
    public Param Scale { get; init; } = 0.0625;
    public Param Octaves { get; init; } = 3;
    public Param Strength { get; init; } = 0.0;
}

/// <summary>The structure of a material. Each family is a procedure that gives a height for any pixel at any size.</summary>
public abstract record Structure
{
    public abstract string Family { get; }
}

/// <summary>Layered value noise with optional domain warping and an optional ridged mode, for materials with no visible units.</summary>
public sealed record FieldStructure : Structure
{
    public const string FamilyName = "field";
    public override string Family => FamilyName;

    public Param Scale { get; init; } = 0.125;
    public Param Octaves { get; init; } = 4;
    public Param Gain { get; init; } = 0.5;
    public Param Warp { get; init; } = 0.0;
    public bool Ridged { get; init; }
}

/// <summary>Cellular noise on a jittered lattice, for cobbles, cracked ground, scales and crystal faces.</summary>
public sealed record CellsStructure : Structure
{
    public const string FamilyName = "cells";
    public override string Family => FamilyName;

    public Param Count { get; init; } = 8;
    public Param Jitter { get; init; } = 0.8;
    public Param EdgeWidth { get; init; } = 0.01;
    public Param EdgeHeight { get; init; } = 0.0;
}

/// <summary>A grid of rows and columns with mortar, for bricks, planks, flagstones and blocks.</summary>
public sealed record PatternStructure : Structure
{
    public const string FamilyName = "pattern";
    public override string Family => FamilyName;

    public Param Rows { get; init; } = 4;
    public Param Columns { get; init; } = 4;
    public Param RowOffset { get; init; } = 0.0;
    public Param MortarWidth { get; init; } = 0.01;
    public Param MortarHeight { get; init; } = 0.0;
}

/// <summary>
/// Coursed masonry: courses of uneven height cut into blocks of uneven width, some spanning two courses, for dungeon
/// walls, fieldstone and flagstones. Rows, Columns, SizeVariation and TallChance never carry a range, because the
/// course heights and the block at the tile's vertical edge are shared by every variant of a set (ADR-013).
/// </summary>
public sealed record BlocksStructure : Structure
{
    public const string FamilyName = "blocks";
    public override string Family => FamilyName;

    public Param Rows { get; init; } = 4;
    public Param Columns { get; init; } = 4;
    public Param SizeVariation { get; init; } = 0.5;
    public Param TallChance { get; init; } = 0.0;
    public Param MortarWidth { get; init; } = 0.01;
    public Param MortarHeight { get; init; } = 0.0;
    public Param CornerRadius { get; init; } = 0.0;
}

/// <summary>
/// A half-timbered wall: posts and rails of wood around panels of plaster or planks, with optional diagonal braces
/// and pegs. The frame is identical in every variant of a set, so only BraceChance carries a range (ADR-014).
/// A BeamWidth of 0 removes the frame, which with Planks gives a plain plank wall or floor.
/// </summary>
public sealed record TimberStructure : Structure
{
    public const string FamilyName = "timber";
    public override string Family => FamilyName;

    public Param Rows { get; init; } = 2;
    public Param Columns { get; init; } = 2;
    public Param BeamWidth { get; init; } = 0.08;
    public Param BraceChance { get; init; } = 0.5;
    public Param Grain { get; init; } = 0.5;
    public Param PegSize { get; init; } = 0.012;
    public bool Planks { get; init; }
    public Param PlankWidth { get; init; } = 0.1;
}

/// <summary>
/// Rounded stones, mounds or pebbles of varied size scattered on a ground, in up to three layers of decreasing size,
/// for swamp, gravel, boulders and rubble. A stone that touches the tile edge is shared by every variant of a set and
/// uses the authored Density, Size and SizeVariation, so those three may range while Count, Jitter, Layers and
/// GroundHeight may not (ADR-015).
/// </summary>
public sealed record ScatterStructure : Structure
{
    public const string FamilyName = "scatter";
    public override string Family => FamilyName;

    public Param Count { get; init; } = 6;
    public Param Density { get; init; } = 0.8;
    public Param Size { get; init; } = 0.5;
    public Param SizeVariation { get; init; } = 0.5;
    public Param Jitter { get; init; } = 1.0;
    public Param Layers { get; init; } = 1;
    public Param GroundHeight { get; init; } = 0.0;
    public Param Warp { get; init; } = 0.3;

    /// <summary>The share of stones drawn as four point stars with the ramp's end color instead of domes, for glints.</summary>
    public Param Stars { get; init; } = 0.0;
}

/// <summary>
/// Round columns with a capital and a base standing in front of another structure, for temples and halls. Behind
/// holds the structure drawn between the columns, and its parameters nest under the path behind. A column runs from
/// the top edge to the bottom edge of the tile, so no field of this family carries a range (ADR-016).
/// </summary>
public sealed record ColumnsStructure : Structure
{
    public const string FamilyName = "columns";
    public override string Family => FamilyName;

    public Param Count { get; init; } = 2;
    public Param Width { get; init; } = 0.12;
    public Param Capital { get; init; } = 0.12;
    public Param Base { get; init; } = 0.08;
    public Param Flare { get; init; } = 0.5;
    public Param Flutes { get; init; } = 0;
    public Structure Behind { get; init; } = new BlocksStructure();
}

/// <summary>
/// Clusters of pointed crystal shards radiating from scattered centers, for crystal caves, as a base or as a
/// transparent garbage over rock. Shards that could reach a tile edge are shared by every variant of a set and use the
/// authored Density, Size, Aspect and SizeVariation, so those may range while Count, Shards and Facet may not (ADR-018).
/// </summary>
public sealed record CrystalStructure : Structure
{
    public const string FamilyName = "crystal";
    public override string Family => FamilyName;

    public Param Count { get; init; } = 8;
    public Param Density { get; init; } = 0.7;
    public Param Shards { get; init; } = 4;
    public Param Size { get; init; } = 0.6;
    public Param Aspect { get; init; } = 0.28;
    public Param SizeVariation { get; init; } = 0.5;
    public Param Facet { get; init; } = 0.6;
}
