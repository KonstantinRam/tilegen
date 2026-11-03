# CLAUDE.md

This file is read by Claude Code at the start of every session in this repository. It says what the project is, where the rules live, how to build and test, and what a finished change looks like. Read docs/SPEC.md before touching generation code, read docs/PRESET_SCHEMA.md before touching presets, and read docs/GUI.md with reference/gui_wireframe.png before touching TileGen.App.

## What this project is

TileGen generates square, top-down, wrapping floor and wall textures for a 2D roguelike built in Unreal. The full requirements are in docs/SPEC.md, the reasons behind the architecture are in docs/DECISIONS.md, and the preset format is in docs/PRESET_SCHEMA.md. The reviewers are Konstantin and a Claude Project that uses docs/REVIEW_CHECKLIST.md, so every change is written to pass that checklist.

## Layout

TileGen.Core is the generator and has no UI dependency and no file I/O. TileGen.IO loads and saves presets, encodes PNG files, reads and writes settings.json, stores favorites and builds contact sheets. TileGen.Cli exports a set from the command line. TileGen.App is the WPF GUI. TileGen.Tests holds the xunit tests. The presets folder holds the JSON presets, docs holds the documents named above, and reference holds the Python demo that the core is ported from.

## Commands

Build with dotnet build. Run the tests with dotnet test, and run them before reporting any change as done. Export a set with dotnet run --project TileGen.Cli -- --preset presets/lava_cave.json --set SuperDungeon --size 256 --variants 4 --seed 1234 --out ./out --contact, which writes out/SuperDungeon with the tiles and settings.json and out/SuperDungeon_contact.png next to it. Open the contact sheet with the Read tool after any change to generation code or to a preset, because the tests do not judge how a tile looks.

## Rules that the review enforces

The core calls no file system API and references no UI assembly. Generation is one pure function, Generator.Render, and the GUI, the CLI and the tests all call it. The pixel path uses integer hash noise and only basic double arithmetic, and it never calls System.Random or a transcendental Math function, because the output must be byte-identical on every machine (docs/DECISIONS.md ADR-008). Every scale parameter is a fraction of the tile, and pixels appear only in the rasterizer. Every lattice lookup, including neighbor lookups, takes its coordinates modulo the cell count, so that tiles wrap by construction.

## Test plan

Tests check properties and not golden images, because golden images break on every intentional change to the look. The required tests are: the output buffer is exactly size by size RGB; the same request rendered twice gives identical bytes; the tileability ratio of every shipped preset stays under the threshold recorded in the test; the measured garbage coverage is within 0.01 of the requested value at 0, 0.25, 0.5, 0.75 and 1 for every shipped preset; variant seeds are stable when the variant count grows; exported file names match ^(floor|wall)_[0-9]{2}\.png$; and an export reopened from its settings.json re-exports byte-identical files. Add a new preset to the preset list of the tileability and coverage tests in the same change that adds the preset.

## Definition of done

A change is done when dotnet test passes, when the contact sheet for an affected preset has been rendered and looked at, when docs/SPEC.md or docs/PRESET_SCHEMA.md are updated if a behavior or a field changed, and when a decision that changes an ADR is recorded as a new entry in docs/DECISIONS.md. Report the change with the test output summary and the path of the contact sheet, and state in one line what the reviewer should look at first.

## Things not to do

The non-goals listed in docs/SPEC.md (atlas, transparency, decorations, transition tiles, neighbor-aware wall sets, normal or emissive maps, example-based synthesis) stay out until a decision record admits them. A second preview code path in the GUI is a defect, because the preview would stop matching the export. Pixel counts belong in the rasterizer and never in a preset. Renaming a preset field after the first client export needs a migration, because settings.json stores preset snapshots.

## Writing register

Documents and code comments follow the register in docs/REVIEW_CHECKLIST.md: full sentences, one idea per sentence, conclusions stated as instructions with the reason attached, no em-dashes, no double hyphens, no emojis, and no stock AI vocabulary such as delve, leverage, robust, seamless or crucial. Comments say why, not what. For a tile that wraps at its edges, write "wrapping" or "tiles without a visible seam".
