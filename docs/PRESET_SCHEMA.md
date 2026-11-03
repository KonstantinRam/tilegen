# Preset schema

Version 1, 2026-10-31. A preset is one JSON file in the presets folder, and it describes one setting: a floor material pair and a wall material pair, plus the defaults for variants and jitter. This document defines every field, and the four shipped presets (lava_cave, greek_ruins, ice_cave and cursed_lands in the presets folder) are complete examples.

## Status

Draft. The field names are final once the first preset renders, because settings.json stores a full preset snapshot and renaming a field later would break reproduction of old exports. Until then the names can change freely.

## Numeric parameters and ranges

Every numeric parameter accepts two forms. The short form is a plain number, for example "scale": 0.1, and a parameter in the short form is fixed: it never jitters and never randomizes. The long form is an object with value, min and max, for example "scale": {"value": 0.1, "min": 0.05, "max": 0.2}, and a parameter in the long form takes part in jitter and in randomize parameters, within its range. In practice this means the preset author decides which knobs the variants may move, and a preset with every parameter in the short form produces variants that differ by seed only.

## Top level

The top level object has these fields.

schemaVersion is the integer 1 and lets the loader reject files from a future version with a clear message.

id is a short identifier in letters, digits and underscores, for example lava_cave, and it is the name stored in settings.json and favorites.

displayName is the name shown in the GUI, for example "Lava Cave".

floor and wall are tile type objects, described next.

variants is the default variant count and must be at least 4.

jitter is the jitter fraction applied to ranged parameters in each variant, as a number from 0 to 1. A value of 0.1 means a variant may move a parameter by up to one tenth of its range in either direction.

## Tile type object

A tile type object has these fields.

base is the material shown where there is no garbage.

garbage is the material shown where the coverage mask is on. It is a full material object with the same fields as base, because at coverage 1 it is the whole tile.

garbageCoverage is the default coverage from 0 to 1, and it is the value the GUI slider starts at. The slider shows it as 0 to 100.

garbageScale is the scale of the coverage mask noise as a fraction of the tile, and garbageSoftness is the width of the blend band around the threshold as a fraction of the mask field's range. Both accept ranges. A jittered value only moves the mask away from the tile edges, because along the edges every variant of a set follows a shared mask built from the authored values, which is what makes garbage continue from one variant to the next (SPEC.md, Garbage). The crossover from the shared mask to the variant's own mask sits about half an authored mask cell from the edge, so a large garbageScale shares a wider strip of every tile.

## Material object

A material object has these fields.

structure is an object whose family field is one of field, cells or pattern, and whose other fields depend on the family, as listed below.

ramp is a list of color stops. Each stop has t from 0 to 1 and color as a hex string such as "#4a1c10". The list must be sorted by t, must start at 0 and end at 1, and colors between stops are interpolated linearly in sRGB. A ramp with one color is allowed and produces a flat material whose only variation comes from tint and detail.

detail is an object with scale, octaves and strength. The detail noise is added to the structure height before the ramp is applied, so strength 0 disables it. All three accept ranges.

tint is the per-cell tint strength from 0 to 1, and it is ignored by the field family because that family has no cells. The tint scales the color after the ramp by 1 plus tint times (c minus 0.5), where c is a value from 0 to 1 drawn from the cell id, so a tint of 0.2 moves a cell by up to a tenth of its brightness, and the border between cells keeps its own color. Scaling the color instead of shifting the height is what lets a one-color ramp still show tint.

contrast remaps the height around 0.5 before the ramp, where 1 is no change.

edgeDarkening darkens pixels near a cell edge from 0 (off) to 1, and it is the only remnant of the bevel seen in the client's samples. It defaults to 0 and is ignored by the field family. The darkening is full at the border and fades out over a quarter of a cell into the interior, and it leaves the border or mortar itself alone.

## Structure families

The field family has scale (the size of one lattice cell of the first octave as a fraction of the tile), octaves (an integer from 1 to 8), gain (the amplitude ratio between octaves, usually 0.5), warp (the domain warp strength from 0 to 1), and ridged (a boolean that folds the noise into ridges, which is how lava cracks and crystal veins are made). The number of lattice cells per tile is 1 divided by scale and must be an integer, so the loader rounds it and reports the rounded value.

The cells family has count (the integer number of cells per axis per tile), jitter (how far a cell center may move from its lattice position, from 0 to 1), edgeWidth (the width of the border between cells as a fraction of the tile), and edgeHeight (the height value assigned to the border, usually 0 so that borders map to the dark end of the ramp). The border is centered on the line between two cells, so each cell gives up half of edgeWidth. The cell interior height rises from edgeHeight at the border to 1 at half a lattice cell from it, with a profile that is steep at the border and flat toward the center, so a cell reads as a rounded stone, and the detail noise adds grain on top.

The pattern family has rows (the integer number of rows per tile), columns (the integer number of cells per row), rowOffset (how far every second row is shifted, as a fraction of a cell width, 0.5 for bricks), mortarWidth (as a fraction of the tile), and mortarHeight (the height value of the mortar, usually 0). The mortar is centered on the cell boundary, so each cell gives up half of mortarWidth. The cell interior height is 1 minus a per-cell variation of up to 0.1, and the detail noise adds grain on top.

## Worked example

The lava cave preset uses the cells family for the floor base with a dark gray ramp, so that the floor reads as cooled rock crust, and it uses the field family for the garbage material with ridged noise and a ramp from near black through dark red to bright orange, so that garbage reads as glowing lava breaking through the crust. The client asked for the red to be the garbage, because the slider then adds lava to a walkable floor instead of adding crust to a lava lake. The wall base is the field family without ridges and a dark basalt ramp, and the wall garbage is a ridged lava field, so that garbage on walls reads as glowing veins. The variants count is 4 and jitter is 0.1, and the ranged parameters are the lava field scale, the warp strength, the lava detail strength and the floor's garbage scale, so that variants differ in the size and density of the lava cracks without changing the palette.

The cursed lands preset shows two uses of the cells family that are not stones. Its floor garbage is a cells material whose ramp stays at the dirt color up to t 0.74 and turns bone white only above t 0.9, so only the top of each dome shows and a garbage patch reads as bone bits scattered on dead ground instead of as a pavement. A small cell shows no bone at all and a large one shows a blob, which is where the scatter comes from, and the dirt color matches the mid tone of the base so that the patch does not read as a different ground. The ice cave wall is a cells material with contrast 1.5 and a tint of 0.4, so the dome flattens into one level per cell and the per-cell tint turns the cells into facets at different brightness, which reads as ice blocks or crystal faces. Use a thin edgeWidth (0.006 to 0.012) and a border color close to the interior for both, because a wide dark border turns any cells material into a net.

## Adding a preset

Copy the closest existing preset, change id and displayName, edit the ramps first because color carries most of the look, then adjust the structure, and render a contact sheet with the CLI before touching the detail parameters. A preset that renders a visible seam, or whose measured garbage coverage is off by more than 0.01, fails the tests and is not shipped.
