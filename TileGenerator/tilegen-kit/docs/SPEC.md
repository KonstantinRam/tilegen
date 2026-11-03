# TileGen specification

Version 0.1, 2026-10-31. TileGen is a working name for the generator and can be renamed without changing anything else in this document.

## Status

This is a draft agreed with the client on 2026-10-31. Items marked OPEN are still waiting on the client or on Konstantin, and every other item is settled. The repository copy of this file is the source of truth, and the copy in the Claude Project knowledge is refreshed from it at milestones.

## Purpose

TileGen produces square, top-down, wrapping floor and wall textures for a 2D roguelike that is built in Unreal. The client picks a setting such as lava cave, ice cave, cursed lands or ancient greek ruins, picks a size, a variant count and a garbage level, and receives a folder of PNG files plus a settings file that reproduces them exactly. About 20 settings are planned for the first release, so adding a setting must be cheap, because authoring the settings is more work than writing the code.

## Agreed requirements

The client confirmed the following on 2026-10-31. Each requirement is stated with its consequence for the code, so that a reader can see why the architecture looks the way it does.

Textures are smooth and are generated natively at the requested size, which is style B in reference/brick_demo.png. The three pixel-art samples the client sent earlier are not style references, because they contradict this choice, and they are kept only as a record. This means every parameter that describes a scale is expressed as a fraction of the tile, and the generator rasterizes at the requested size instead of upscaling a base drawing.

Tiles are square and power of two. The supported sizes are 64, 128, 256, 512 and 1024. OPEN: the client said "64, 128 etc", so 1024 as the upper bound is an assumption to confirm.

Output files are opaque 8-bit RGB PNG files in sRGB, one file per tile. There is no transparency, no outline, no dithering, no shadow and no light direction. The bevel visible in the client's samples was incidental, so edge darkening exists as an optional material parameter with a default of zero and nothing in the tool assumes a light direction.

Tiles are flat top-down views. Every wall cell shows the same wall texture regardless of its neighbors, so there are no corner, end or junction tiles. There are no transition tiles between two floor types. Decorations are not produced by this tool, because the game draws them on a separate layer.

Each export is a folder named after the set, containing floor_NN.png and wall_NN.png with NN zero-padded from 01, plus settings.json. The client's example is SuperDungeon/floor_01.png. The client imports the PNG files with his own script, so no atlas and no Unreal-side import settings are in scope.

A set has at least 4 variants per tile type, and the count is a parameter. Variants differ by seed and by a small parameter jitter, which means a variant is a derived seed plus a derived parameter set, and both are recorded in settings.json.

The garbage slider runs from 0 to 100. At 0 the tile shows only the base material, and at 100 the garbage material covers the whole tile. This means garbage is a second full material in every preset, and the slider is the coverage fraction of that material over the base.

The user can keep favorites and regenerate or export them later. The user can reroll the seed only, randomize the parameters only, or do both at once.

The platform is Windows only.

A setting is a JSON preset file. The tool ships with starter presets, the GUI can save the current state as a new preset, and an image can supply a palette and a seed number to a preset (see the section on image input).

## Non-goals for version one

The following are out of scope, and a change request for any of them goes through DECISIONS.md first: atlas or sprite sheet output, transparency, decoration tiles, transition tiles, neighbor-aware wall sets, normal, roughness or emissive maps, example-based synthesis from photographs, exported layers for hand editing, and any platform other than Windows.

## Architecture

The solution has five projects. TileGen.Core is a class library that turns a preset, a tile kind, a parameter set, a seed and a size into a pixel buffer, and it has no UI dependency and no file I/O. TileGen.IO loads and saves presets, encodes PNG files, writes and reads settings.json, stores favorites, and builds contact sheets. TileGen.Cli is a console application that exports a set from a preset without a GUI, which is how Claude Code and the tests exercise the pipeline. TileGen.App is the WPF application. TileGen.Tests holds the xunit tests.

The core entry point is a pure function. In C# terms it is Generator.Render(TileRequest request), where TileRequest carries the preset, the tile kind (floor or wall), the resolved parameter set, the seed and the size, and the result is a width by height RGB byte buffer. In practice this means the GUI, the CLI and the tests all call the same function, and a favorite is just a stored TileRequest.

The stack is .NET 8 with C#, WPF for the GUI, xunit for tests, and PNG encoding in TileGen.IO through System.Drawing.Common or SkiaSharp. OPEN: Konstantin has not vetoed this choice, and it is treated as accepted until he does; see DECISIONS.md ADR-003.

## Generation model

A material is a structure, a color ramp and a detail layer. The structure produces a height value in the range 0 to 1 for every pixel, plus a cell id where the structure has cells, plus an edge mask where the structure has edges. The ramp maps a height value to a color through a list of color stops. The detail layer is a noise field that is added to the height before the ramp is applied, so that flat regions receive grain. A per-cell tint, drawn from the cell id, darkens or lightens each cell slightly so that bricks and stones do not share one flat color.

There are three structure families in version one, and every preset material names one of them.

The field family is layered value noise (fractal Brownian motion) with optional domain warping and an optional ridged mode. It covers cave rock, dirt, ice, lava and any material with no visible units. In practice this means the material is defined by a base scale, an octave count, a warp strength and a ridged flag.

The cells family is cellular (Worley) noise on a jittered lattice. It covers cobbles, cracked ground, scales and crystal faces. Each cell has an id for tinting, and the distance to the nearest cell border gives an edge mask that can be drawn as mortar or as a crack.

The pattern family is a grid layout of rows and columns with an optional row offset, which covers bricks, planks, flagstones and marble blocks. Each cell has an id, the gap between cells is the mortar, and the cell interior can receive its own field noise.

Every family is periodic by construction. Lattice noise takes its lattice coordinates modulo the cell count, and the cell count per tile is an integer, so the result wraps at the tile edges without any blending step. Patterns use integer row and column counts that divide the tile, so they wrap for the same reason. Tileability is still tested (see the test plan in CLAUDE.md), because a bug in a modulo is easy to write and hard to see.

All scale parameters are fractions of the tile. For example, a brick row height of 0.125 means eight rows per tile at any size, and a detail scale of 0.0625 means the detail lattice has sixteen cells per tile at any size. The generator converts these to pixels only inside the rasterizer. The number of usable octaves depends on the size, because an octave whose lattice cell would be smaller than one pixel adds nothing, so the generator drops such octaves and a 64 pixel tile is the same design with less fine detail.

## Determinism

The same preset, parameters, seed and size must produce the same bytes on every run and on every Windows machine, because favorites and settings.json promise reproduction. Three rules follow from this. First, all randomness comes from an integer hash of integer lattice coordinates and the seed, and there is no use of System.Random in the pixel path. Second, the pixel path uses only addition, subtraction, multiplication, division, floor, minimum, maximum and comparisons on doubles, and it does not call Math.Sin, Math.Pow, Math.Exp or any other transcendental function, because their results can differ between runtimes. Third, parallel rendering is allowed only when each pixel is computed independently, so that the row scheduling cannot change the output.

## Garbage

The garbage material is composited over the base material with a coverage mask. The mask field is its own fractal noise with its own scale, and the threshold is the quantile of that field at 1 minus the coverage, so a coverage of 0.5 covers exactly half of the pixels. A soft blend width around the threshold hides the hard edge, and the measured coverage is defined as the fraction of pixels whose blend weight is at least 0.5. The exporter and the tests check that the measured coverage is within 0.01 of the requested value.

Garbage coverage is a parameter of each tile type, so a set can have a mossy floor and a clean wall. The GUI shows the slider for the tile type that is selected in the preview.

## Variants, seeds and jitter

A set has a base seed. Variant i, counted from 1, uses the seed Hash64(baseSeed, i), so adding a fifth variant never changes the first four. Each numeric parameter with a declared range receives a jitter in variant i: the value becomes clamp(value + u * jitter * (max - min), min, max), where u is drawn from the variant seed in the range -1 to 1, and jitter is the preset's jitter fraction. A parameter without a declared range never jitters and never randomizes. The resolved parameter set of every variant is written to settings.json, so a variant can be reproduced without re-running the jitter.

The three roll actions are defined on top of this. Reroll seed draws a new base seed and keeps the parameters. Randomize parameters keeps the base seed and draws every ranged parameter uniformly from its range. Roll both does both.

## Export format

An export writes one folder named after the set. The set name is entered by the user and is sanitized to letters, digits and underscores. The folder contains floor_01.png through floor_NN.png, wall_01.png through wall_NN.png, and settings.json. The file name pattern is fixed by the regular expression ^(floor|wall)_[0-9]{2}\.png$, and the tests check it.

settings.json is the reproduction record of the export. It holds the generator version, the preset id, a full snapshot of the preset, the size, the variant count, the base seed, the garbage coverage per tile type, and for each variant its index, its seed and its resolved parameters. Opening a settings.json in the GUI restores that exact state, which is how a client export can be reproduced or extended later. The client's import script must skip it, which is easy because it is the only file in the folder that is not a PNG.

A contact sheet is a PNG that shows every tile of the set in a 4 by 4 wrapped arrangement with the variants mixed, so that seams and repetition are visible at a glance. The CLI writes it next to the set folder as SetName_contact.png when the --contact flag is given, and never inside the folder, because the client's import script scans the folder for PNG files.

## Favorites

A favorite is a named TileRequest set: the preset snapshot, the resolved parameters, the base seed, the size and the variant count. Favorites are stored in favorites.json under the user's application data folder, and a favorite is regenerated on demand rather than cached as pixels, because generation is a pure function and the pixels are cheap to recompute. Exporting a favorite is the same code path as exporting the current state.

## GUI

The window is described in GUI.md and drawn in reference/gui_wireframe.png. The wall and the floor each have a column with a 1:1 preview, a variant strip, the garbage slider and the parameter rows of the base and garbage materials, and the right column shows a room preview (wall tiles around a floor area, variants mixed), the preset and favorites lists, and the set controls (name, size, variant count, seed, the three roll buttons, add favorite, export). The parameter rows are generated from the preset schema, so a new field appears in the GUI without GUI code.

The preview renders live at 256 pixels (or the export size when smaller) through the same Generator.Render as the export, with a short delay after each change so that a dragged slider renders at most 20 times per second. The target is four variants at 256 pixels in under 200 milliseconds on a laptop.

## Image input

An image can contribute two things to a preset, and nothing else. It can supply a palette, where the tool reduces the image to a small number of dominant colors, sorts them by luminance and writes them as the ramp stops of the selected material. It can supply a seed number, where the tool hashes the image bytes to a base seed. Structure does not transfer from an image without example-based synthesis, which is out of scope, so the GUI labels the feature "palette from image" and not "sample", because a user who loads a photo of lava expects lava structure and will not get it.

OPEN: Konstantin chooses the reference images. CC0 texture libraries such as ambientCG and Poly Haven are suitable sources, because their license allows palette extraction for a commercial game without attribution.

## Unreal notes

The client's script imports the PNG files, so this section only records what the files guarantee. Every tile wraps, so the Unreal texture can use the Wrap address mode. Sizes are powers of two, so mip generation and streaming work with the defaults. Colors are sRGB, so the sRGB flag on the texture should stay on. Nothing else is required.

## Performance targets

Four variants at 256 pixels render in under 200 milliseconds for the live preview. One tile at 1024 pixels exports in under 2 seconds. A full set of 4 floors and 4 walls at 512 exports in under 10 seconds. These targets are checked by hand on the client's machine before the first release, and a failed target is a reason to add parallel rows, not a reason to lower the quality.

## Open items

The upper size bound of 1024 is an assumption. The stack is assumed accepted. The reference images are Konstantin's choice. The list of 20 settings is known only by example (lava cave, ice cave, cursed lands, ancient greek ruins), so the first four presets are built from those names and the rest are added as the client names them.
