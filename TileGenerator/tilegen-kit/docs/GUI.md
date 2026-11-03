# GUI

Version 1, 2026-10-31. This document describes the TileGen.App window, what each control does, and which parts of the reference tool "Brick and Mortar" (reference/brick_and_mortar_ui.png) were copied and which were left out on purpose. The layout is drawn in reference/gui_wireframe.png, and the WPF layout may differ from it by small amounts.

## Status

Draft, agreed with Konstantin on 2026-10-31 in principle. The control set is derived from SPEC.md and PRESET_SCHEMA.md, so a new preset field appears in the GUI without GUI code, and a GUI control that has no field in the schema is a defect.

## What the reference tool got right

The reference tool shows both tile types at once, so the user tunes the wall against the floor instead of switching back and forth. It shows a room preview, where wall tiles surround a floor area, which is how the tiles meet in the game and the only view that shows the pair together. It keeps presets in a list with load, save and delete. It gives every parameter a numeric box, so a value can be typed and read exactly. TileGen keeps all four of these.

## What the reference tool does that TileGen does not copy

The reference tool bakes lighting into the tiles (light edge, dark edge, top wall darkening). SPEC.md rules lighting out, so no such controls exist. Its colors are single RGB values, while a TileGen material has a color ramp, so the color control is a ramp editor. It produces one tile per type with no variants, while TileGen produces at least four, so the previews show variants. It regenerates only on a button press, while TileGen renders the preview live, because the renderer is fast enough at preview resolution and a live preview makes a slider worth having.

## Layout

The window has three columns. The left column is the wall, the middle column is the floor, and the right column holds the room preview on top and the preset list and set controls below. The two material columns are identical in structure, which means they are one WPF user control instantiated twice.

A material column starts with a 1:1 preview of the selected variant at preview resolution, followed by a strip of variant thumbnails that selects which variant the 1:1 preview shows. Below the strip is the garbage coverage slider from 0 to 100, which is the headline knob and sits above everything else for that reason. Below it are two tabs, Base and Garbage, and each tab holds the parameter rows of that material: the structure family selector, the ramp editor, and then one row per numeric parameter of the family, the detail layer, contrast, tint and edge darkening. The rows scroll when they do not fit.

The room preview draws a ring of wall tiles around a rectangle of floor tiles, with variants chosen at random per cell from a fixed preview seed, and it fits the panel by downscaling with a smooth filter. A zoom control switches between fit, 1:1 and 2:1, because the fit view shows repetition and seams at a glance and the 1:1 view shows detail. A room size control sets the floor rectangle from 4 by 4 to 16 by 16 cells. A "full size" check box renders the preview at the export size instead of the preview resolution, which is slower and is used for a final check, because fine octaves that are dropped at 256 appear at 1024.

The preset and favorites panel has two tabs. The presets tab lists the JSON files in the presets folder, with Load, Save as and Delete. Load replaces the current parameters with the preset defaults and draws a new base seed. Save as writes the current parameters as a new preset file after asking for an id and a display name, and it writes the current ranges with them. Delete removes the file after a confirmation. The favorites tab lists the saved favorites with Load, Rename and Delete, and a favorite restores the full state including the seed, the size and the variant count, which a preset does not.

The set panel holds the set name, the size selector (64 to 1024), the variant count (4 or more), the editable base seed, the three roll buttons (Reroll seed, Randomize parameters, Roll both), Add favorite and Export set. Export set asks for an output folder once, remembers it, and writes the folder described in SPEC.md. A menu bar above the columns offers Open settings.json, which restores a previous export, Palette from image, which fills the ramp of the selected material from a picture, and Reset to preset defaults.

## Parameter rows

Every numeric parameter row has a label, a slider, a numeric text box and a lock toggle. The slider and the box show the same value and either one edits it. The slider bounds come from the schema's absolute bounds for that parameter, for example 1/64 to 1/2 for a scale, so a value outside the bounds cannot be entered. The lock toggle shows whether the parameter takes part in jitter and randomize: a parameter with a range in the preset has the lock open and a parameter in the short form has it closed. Toggling the lock on a locked parameter gives it the full schema bounds as its range, and the range can be narrowed by editing the preset JSON, because a range editor in the GUI would double the number of controls for a setting that is changed rarely. Integer parameters such as octaves, rows and cell count use a spinner instead of a slider.

The ramp editor shows the color stops as swatches in order, with a plus and a minus button to add or remove a stop. Clicking a swatch opens a color dialog and shows the hex value in a text box next to it, so a color can be typed. Dragging a swatch left or right changes its position t within its neighbors. A ramp keeps at least one stop.

## Preview rendering

The preview resolution is 256, or the export size when the export size is smaller. Every parameter change schedules a render after a short delay of about 50 milliseconds, so a dragged slider renders at most 20 times per second, and a render that is still running when the next one is scheduled is cancelled. The preview calls the same Generator.Render as the export, with the same seeds, so the preview at the export size is byte-identical to the export. In practice this means there is one renderer and the preview is a cache of its output.

## Keyboard

Space rerolls the seed, because that is the most frequent action while tuning. Control plus S saves a favorite. Control plus E exports. These are listed in a tooltip on the matching buttons.

## Out of scope for version one

There is no undo stack; favorites and the editable seed cover the cases where a user wants a previous state back. There is no range editor in the GUI. There is no multi-set batch export in the GUI; the CLI does that.
