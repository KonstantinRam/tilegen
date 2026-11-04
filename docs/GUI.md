# GUI

Version 1, 2026-10-31. This document describes the TileGen.App window, what each control does, and which parts of the reference tool "Brick and Mortar" were copied and which were left out on purpose. The screenshot of that tool and the wireframe the layout was drawn from are no longer kept in the repository, and the test suite writes a screenshot of the built window to out/gui_main.png.

## Status

Draft, agreed with Konstantin on 2026-10-31 in principle, and implemented the same day in TileGen.App on Avalonia (DECISIONS.md ADR-011). The control set is derived from SPEC.md and PRESET_SCHEMA.md, so a new preset field appears in the GUI without GUI code, and a GUI control that has no field in the schema is a defect. The paragraphs marked "as built" record where the implementation pinned down a detail this document left open.

## What the reference tool got right

The reference tool shows both tile types at once, so the user tunes the wall against the floor instead of switching back and forth. It shows a room preview, where wall tiles surround a floor area, which is how the tiles meet in the game and the only view that shows the pair together. It keeps presets in a list with load, save and delete. It gives every parameter a numeric box, so a value can be typed and read exactly. TileGen keeps all four of these.

## What the reference tool does that TileGen does not copy

The reference tool bakes lighting into the tiles (light edge, dark edge, top wall darkening). SPEC.md rules lighting out, so no such controls exist. Its colors are single RGB values, while a TileGen material has a color ramp, so the color control is a ramp editor. It produces one tile per type with no variants, while TileGen produces at least four, so the previews show variants. It regenerates only on a button press, while TileGen renders the preview live, because the renderer is fast enough at preview resolution and a live preview makes a slider worth having.

## Layout

The window has three columns. The left column is the wall, the middle column is the floor, and the right column holds the room preview on top and the preset list and set controls below. The two material columns are identical in structure, which means they are one user control instantiated twice.

A material column starts with a 1:1 preview of the selected variant at preview resolution, followed by a strip of variant thumbnails that selects which variant the 1:1 preview shows. Below the strip is the garbage coverage slider from 0 to 100, which is the headline knob and sits above everything else for that reason. Below it are two tabs, Base and Garbage, and each tab holds the parameter rows of that material: the structure family selector, the ramp editor, and then one row per numeric parameter of the family, the detail layer, contrast, tint and edge darkening. The rows scroll when they do not fit. As built, the Garbage tab starts with the two mask rows, mask scale and mask softness, because they belong to the tile type rather than to a material and the garbage tab is where a user looks for them.

The room preview draws a ring of wall tiles around a rectangle of floor tiles, with variants chosen at random per cell from a fixed preview seed, and it fits the panel by downscaling with a smooth filter. A zoom control switches between fit, 1:1 and 2:1, because the fit view shows repetition and seams at a glance and the 1:1 view shows detail. A room size control sets the floor rectangle from 4 by 4 to 16 by 16 cells. A "full size" check box renders the preview at the export size instead of the preview resolution, which is slower and is used for a final check, because fine octaves that are dropped at 256 appear at 1024.

The preset and favorites panel has two tabs. The presets tab lists the JSON files in the presets folder, with Load, Save as and Delete. Load replaces the current parameters with the preset defaults and draws a new base seed. Save as writes the current parameters as a new preset file after asking for an id and a display name, and it writes the current ranges with them. Delete removes the file after a confirmation. The favorites tab lists the saved favorites with Load, Rename and Delete, and a favorite restores the full state including the seed, the size and the variant count, which a preset does not.

The set panel holds the set name, the size selector (64 to 1024), the variant count (4 or more), the editable base seed, the three roll buttons (Reroll seed, Randomize parameters, Roll both), Add favorite and Export set. Export set asks for an output folder once, remembers it, and writes the folder described in SPEC.md. A menu bar above the columns offers Open settings.json, which restores a previous export, Palette from image, which fills the ramp of the selected material from a picture, and Reset to preset defaults. As built, the File menu also offers Choose output folder, so the remembered folder can be changed, and a check item Write contact sheet on export, which writes the sheet next to the set folder the way the CLI's --contact flag does. The palette dialog asks which of the four materials receives the ramp, defaulting to the material whose column was touched last, how many colors to extract, and whether the image hash also becomes the base seed, which is the second thing ADR-007 allows an image to supply. A settings.json that was opened keeps its stored variant parameters for the preview and the export until any parameter, the seed or the variant count changes, so a reopened export reproduces its files exactly.

## Parameter rows

Every numeric parameter row has a label, a slider, a numeric text box and a lock toggle, except the rows of parameters that the schema keeps fixed across variants (the layout parameters of the blocks family), which have no lock toggle because they can never carry a range. The boolean fields of the schema, ridged on the field family, planks on the timber family and cellColors on any material, have no control and are set in the preset file; the family selector creates a structure with the boolean at its default. The rows of the structure behind the columns of the columns family appear with the label prefix Behind, and the family of that structure is set in the preset file, because the selector only picks the family of the material itself. The slider and the box show the same value and either one edits it. The slider bounds come from the schema's absolute bounds for that parameter, for example 1/64 to 1/2 for a scale, so a value outside the bounds cannot be entered. The lock toggle shows whether the parameter takes part in jitter and randomize: a parameter with a range in the preset has the lock open and a parameter in the short form has it closed. Toggling the lock on a locked parameter gives it the full schema bounds as its range, and the range can be narrowed by editing the preset JSON, because a range editor in the GUI would double the number of controls for a setting that is changed rarely. Integer parameters such as octaves, rows and cell count use a spinner instead of a slider.

The ramp editor shows the color stops as swatches in order, with a plus and a minus button to add or remove a stop. Clicking a swatch opens a color dialog and shows the hex value in a text box next to it, so a color can be typed. Dragging a swatch left or right changes its position t within its neighbors. A ramp keeps at least one stop.

## Preview rendering

The preview resolution is 256, or the export size when the export size is smaller. Every parameter change schedules a render after a short delay of about 50 milliseconds, so a dragged slider renders at most 20 times per second, and a render that is still running when the next one is scheduled is cancelled. The preview calls the same Generator.Render as the export, with the same seeds, so the preview at the export size is byte-identical to the export. In practice this means there is one renderer and the preview is a cache of its output. As built, an edit in one column re-renders only that tile type, and the room preview draws straight from the cached tiles with no composed image, so a 16 by 16 room costs only its draw calls.

## Checking the GUI without a window

The test suite builds the main window on the headless platform with real Skia drawing and writes out/gui_main.png and out/gui_garbage_tab.png, which are reviewed like contact sheets. Behavior tests drive the view model without a window and check that the preview tiles are the bytes the export writes, that a row edit reaches the preset and re-renders only its tile type, that ramp edits keep the ends at 0 and 1, and that a GUI export matches the exporter and reproduces itself after Open settings.json. The view models reference no Avalonia type, which is what makes those tests possible.

## Keyboard

Space rerolls the seed, because that is the most frequent action while tuning. Control plus S saves a favorite. Control plus E exports. These are listed in a tooltip on the matching buttons.

## Out of scope for version one

There is no undo stack; favorites and the editable seed cover the cases where a user wants a previous state back. There is no range editor in the GUI. There is no multi-set batch export in the GUI; the CLI does that.
