# Review checklist

This checklist is used by Konstantin and by Claude in the Project when reviewing a change from Claude Code. A review covers the diff, the test output and the contact sheet together, because a generation change that passes the tests can still look wrong, and a contact sheet that looks right can still hide a determinism bug.

## Status

Version 1, 2026-10-31. Add an item when a defect slips through, and record why in the item, so that the list stays short and every item has a reason.

## Layering

TileGen.Core references no UI assembly and no file system API. The reason is that the core must be callable from the tests and the CLI without a window, and a stray System.IO call in the core would make the generator depend on paths. Check the project references and search the core for System.IO, System.Drawing, System.Windows and SkiaSharp.

Generation is reachable through one entry point, Generator.Render, and the GUI, the CLI and the tests call that same function. A second code path for the preview is a defect, because the preview would stop matching the export.

## Determinism

The core contains no System.Random and no Math.Sin, Math.Cos, Math.Pow, Math.Exp, Math.Log or Math.Sqrt in the pixel path. The reason is ADR-008: these can differ between runtimes, and favorites promise exact reproduction. A sqrt is allowed only in code that runs once per tile, such as computing a lattice size, never per pixel.

The determinism test renders the same request twice and compares the byte arrays, and the test exists in the suite and passes.

Variant i derives its seed from Hash64(baseSeed, i), and a change that reshuffles existing variants when the count changes is a defect.

## Tileability and coverage

Every structure family takes its lattice coordinates modulo an integer cell count, and patterns use integer row and column counts. Check the modulo on every lattice lookup, including the neighbor lookups at plus one, because a missing modulo on one neighbor produces a one-pixel seam that the contact sheet shows but the author's single tile view does not.

The tileability test computes the mean absolute difference across the wrapped edges divided by the mean absolute difference between interior neighbors, and the ratio stays under the threshold recorded in the test. A new preset is included in the test's preset list.

The coverage test checks the measured garbage coverage within 0.01 of the requested value at 0, 0.25, 0.5, 0.75 and 1 for every shipped preset.

## Parameters and units

Every scale parameter is a fraction of the tile, and the conversion to pixels happens only in the rasterizer. A pixel count in a preset or in a material is a defect, because the preset would stop working at a different size.

A parameter in the long form carries min and max, and jitter and randomize touch only those parameters. A parameter in the short form never moves.

## Export

File names match ^(floor|wall)_[0-9]{2}\.png$ and the set folder contains only those files plus settings.json. The contact sheet is written next to the folder, not inside it.

settings.json contains the generator version, the preset snapshot, the size, the variant count, the base seed, the coverage per tile type and the resolved parameters of every variant, and the GUI opens it and restores the same state. The round-trip test exports, reopens and re-exports, and the two exports are byte-identical.

## GUI

Every control in TileGen.App maps to a field in PRESET_SCHEMA.md or to a set-level setting in SPEC.md, and a control without a field is a defect, because the preset could not store what the user set. The preview and the export call the same Generator.Render with the same seeds, and the full-size preview is byte-identical to the export of the same state. Parameter rows are generated from the schema, so check that a new schema field appears without a GUI change.

## Contact sheet

The contact sheet is attached to the review whenever generation code or a preset changed. On the sheet, look for a visible seam in the 4 by 4 wrapped grid, for a repeat pattern that the eye catches at a glance, for variants that are either identical or so different that they do not read as one set, and for garbage that does not read as the second material described by the preset.

## Register of code comments

Comments state why the code does something and not what the line already says. A comment longer than three sentences belongs in a doc. The repository docs follow the register rules in CLAUDE.md.

## Verdict

A review ends with a list of findings ordered by severity, each with the file, the line, the failure scenario and the fix, and then one line stating whether the change can merge. A review without findings says so in one line and does not pad.
