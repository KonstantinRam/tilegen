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
