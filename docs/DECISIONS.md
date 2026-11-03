# Decision records

One entry per decision, newest at the bottom. Each entry states the decision as an instruction with the reason attached, so that a reader who was not in the chat can restate it. A decision is changed by adding a new entry that supersedes it. The old entry is left as written, because the history of a decision is part of its explanation.

## ADR-001 Generate smooth textures natively at the requested size

### Status

Accepted 2026-10-31.

### Context

The client asked for "textures" and sent three 32 by 32 pixel-art samples, which contradicted each other. A demo (reference/brick_demo.png) showed both styles side by side, and the client chose style B, the smooth texture, and said the samples were wrong.

### Decision

The generator renders every tile natively at the requested size, and every scale parameter is a fraction of the tile.

### Rationale

A native render keeps one preset valid at every size, because the design is described in tile units and only the rasterizer knows the pixel count. The alternative, drawing at a base resolution and upscaling, only makes sense for pixel art, which the client rejected.

### Consequences

The three samples are kept only as a record and are not style references. Konstantin chooses the reference images. Octaves finer than one pixel are dropped at small sizes, so a 64 pixel tile is the same design with less fine detail.

### Rejected alternatives

Pixel-art style with integer upscales (style A), which the client rejected after seeing the demo.

## ADR-002 Use three procedural structure families instead of bitmap masks

### Status

Accepted 2026-10-31.

### Context

The first idea was a set of black and white masks per setting. Masks fit bricks and planks but not cave rock or lava, and bitmap masks at a fixed resolution conflict with native rendering at any size.

### Decision

A material names one of three structure families, field, cells or pattern, and each family is a procedure that produces a height value, a cell id and an edge mask for any pixel at any size.

### Rationale

A procedure is periodic by construction when its lattice is taken modulo an integer cell count, which means every tile wraps without a blending step, and it needs no authored bitmap library, which would have been more work than the code.

### Consequences

Adding a structure that none of the families can express means adding a family in code, which is a review item. Authored masks can be added later as a fourth family if a setting needs them.

### Rejected alternatives

Black and white mask library (fixed resolution, organic materials impossible); grayscale height maps (same resolution problem).

## ADR-003 Build on .NET 8 with C#, WPF and xunit

### Status

Proposed 2026-10-31, treated as accepted until Konstantin vetoes it.

### Context

Konstantin is the code reviewer and his strongest language is C#. The client is on Windows only. Claude Code writes the code and needs fast headless checks.

### Decision

Use .NET 8 with C#. TileGen.Core is a class library with no UI and no file I/O, TileGen.IO handles files, TileGen.Cli is a console exporter, TileGen.App is WPF, and TileGen.Tests is xunit. PNG encoding lives in TileGen.IO through System.Drawing.Common or SkiaSharp.

### Rationale

Review quality is highest in the reviewer's best language. Windows only means WPF and a self-contained single executable cost nothing extra. The test suite runs headless with dotnet test in seconds, and Claude Code can open the output PNG files directly.

### Consequences

The Python demo in reference/brick_demo.py is a reference for the port and is not shipped. The core must not reference WPF or System.Drawing, and the review checklist enforces this.

### Rejected alternatives

Unity (every check needs an editor launch and Unity installed where Claude Code runs); Python (faster for the first noise experiments, but packaging a GUI for Windows and reviewing Python are both weaker for this team); TypeScript in a browser (writing files to a folder from a browser is awkward, and the reviewer gains nothing).

## ADR-004 Define the garbage slider as coverage of a second material

### Status

Accepted 2026-10-31.

### Context

The client defined the slider as 0 meaning none and 100 meaning the whole tile covered.

### Decision

Every preset carries a garbage material per tile type, and the slider is the coverage fraction. The coverage mask is a noise field thresholded at its quantile, so the covered fraction equals the slider value, and a soft blend band hides the edge.

### Rationale

The client's definition only works if garbage is a full material, because at 100 nothing else is visible. The quantile threshold makes the slider honest, which means 50 covers half the pixels, and the tests can check it.

### Consequences

A preset author writes two materials per tile type. The measured coverage is checked within 0.01 in tests.

### Rejected alternatives

A single grain or noise amplitude parameter (does not reach full coverage and does not match the client's definition); separate sliders for grain, debris and wear (the client asked for one slider, and the extra knobs are preset parameters instead).

## ADR-005 Derive variants from the base seed plus parameter jitter

### Status

Accepted 2026-10-31.

### Context

The client wants at least 4 variants and chose seed plus a small parameter jitter over seed only.

### Decision

Variant i uses Hash64(baseSeed, i) as its seed, and every parameter with a declared range moves by up to the preset's jitter fraction of that range, drawn from the variant seed. Parameters without a range never move.

### Rationale

Hashing the index keeps the first four variants stable when a fifth is added. Restricting jitter to ranged parameters lets the preset author decide which knobs variants may touch, and a preset with no ranges still works.

### Consequences

settings.json stores the resolved parameters of every variant, so reproduction does not depend on the jitter code staying the same.

### Rejected alternatives

Independent random seeds per variant (adding a variant would reshuffle the others); jittering every parameter (palette and layout would drift between variants of one set).

## ADR-006 Write settings.json next to the tiles

### Status

Accepted 2026-10-31. Revised the same day: the client first wrote ".txt file", and Konstantin confirmed that a JSON file is what was meant.

### Context

The client asked for a settings file in the set folder with the size and other settings, so that a set can be replicated.

### Decision

The file is named settings.json. It holds the generator version, the preset id and a full preset snapshot, the size, the variant count, the base seed, the garbage coverage per tile type, and the resolved seed and parameters of every variant. The GUI can open it to restore the state.

### Rationale

The content has to carry a nested parameter set per variant and be read back by the tool, and JSON does that with the same serializer the presets already use. A .json extension also tells the client's import script what the file is without a special case.

### Consequences

The preset field names become part of the file format once the first client export exists, so renaming a field after that requires a migration. The import script must skip the one non-PNG file in the folder.

### Rejected alternatives

INI-style key and value lines (no clean nesting); a .txt extension with JSON inside (the client's first wording, withdrawn).

## ADR-007 Limit image input to palette extraction and seed derivation

### Status

Accepted 2026-10-31.

### Context

The client asked whether random sample images could be used as a kind of seed.

### Decision

An image can supply a color ramp (dominant colors sorted by luminance) and a base seed (a hash of the image bytes), and nothing else. The GUI calls the feature "palette from image".

### Rationale

Structure does not transfer from an image without example-based synthesis, which is research-grade work with poor results at tile sizes and is out of scope. Palette and seed are the two properties that do transfer, and both are cheap.

### Consequences

A user who loads a photo of lava gets lava colors and a different seed, and the structure still comes from the preset. The label makes that expectation explicit.

### Rejected alternatives

Image quilting or patch-based synthesis (out of scope); using an image as a tileable swatch under a mask (deferred until a setting needs it).

## ADR-008 Make generation bit-for-bit deterministic

### Status

Accepted 2026-10-31.

### Context

Favorites and settings.json promise that a set can be regenerated exactly, and the client will regenerate sets on his own machine.

### Decision

All randomness comes from an integer hash of lattice coordinates and the seed. The pixel path uses only addition, subtraction, multiplication, division, floor, minimum, maximum and comparisons on doubles, and never calls transcendental functions. Parallel rendering is allowed only per pixel.

### Rationale

Integer hashing is identical on every machine. Basic double arithmetic is IEEE and identical on every x64 runtime, while Math.Sin and Math.Pow can differ between runtimes. Per-pixel independence means thread scheduling cannot change the bytes.

### Consequences

Smoothstep and similar curves are written as polynomials. A determinism test renders twice and compares bytes, and the review checklist bans the listed functions in TileGen.Core.

### Rejected alternatives

System.Random (sequence depends on call order and on the runtime version); floating point sin-based hashes (platform dependent).

## ADR-009 Store settings as JSON preset files and ship starters

### Status

Accepted 2026-10-31.

### Context

Konstantin asked whether to start from the named settings, from custom JSON files or from a sample uploader.

### Decision

A setting is a JSON preset file in the presets folder. The tool ships with starter presets built from the client's named settings, the GUI saves the current state as a new preset, and the sample uploader is the palette feature of ADR-007.

### Rationale

A preset file is the cheapest unit to author, review and version, and Claude in the Project can draft one from a description, which turns 20 settings from a coding task into a review loop.

### Consequences

Presets are reviewed through contact sheets, because a JSON diff says nothing about the look. The schema is documented in PRESET_SCHEMA.md.

### Rejected alternatives

Settings compiled into code (every new setting needs a build); a database (no benefit at 20 files).

## ADR-010 Export individual PNG files and no atlas

### Status

Accepted 2026-10-31.

### Context

The client imports the tiles into Unreal with his own script.

### Decision

The exporter writes one PNG per tile into a folder per set, following SPEC.md, and does not produce an atlas. The contact sheet is written next to the folder, never inside it.

### Rationale

The client's script consumes individual files, so an atlas would be unused work. Keeping the contact sheet outside the folder keeps it out of the script's scan.

### Consequences

An atlas can be added later as an exporter option without touching the core.

### Rejected alternatives

Paper2D tile set texture (not how the client imports).

## ADR-011 Build the GUI with Avalonia instead of WPF

### Status

Accepted 2026-10-31. Konstantin agreed in chat on the condition that the Windows delivery is not put at risk, and this entry supersedes the GUI part of ADR-003 only.

### Context

Claude Code writes and verifies the code on a Linux machine, and the WPF SDK exists only on Windows. With WPF the whole window would be written blind, a binding error or a layout defect would surface on Konstantin's machine first, and every GUI iteration would wait for a Windows round trip. Konstantin asked whether a Linux test build is possible without moving the project too far from its contract.

### Decision

TileGen.App is an Avalonia 11 application. It is one code base that runs on Linux during development and is published as a self-contained single executable for the Windows client. The test suite builds the main window on the headless platform and writes a screenshot, so a GUI change is reviewed from an image the way a generation change is reviewed from a contact sheet.

### Rationale

Avalonia is XAML and MVVM in C#, so the review stays in the reviewer's strongest language and the GUI.md control set maps one to one. Avalonia renders through SkiaSharp, which TileGen.IO already uses, so the native dependency set does not grow. A window that can be run and captured where the code is written is checked on every change instead of once per handoff, which is cheaper than any amount of care in blind XAML.

### Consequences

The client deliverable stays a self-contained Windows executable, published with dotnet publish for win-x64. The layering check in REVIEW_CHECKLIST.md searches the core for Avalonia namespaces as well as System.Windows. Controls that WPF has and Avalonia lacks are built from primitives, which is a cost that so far affects nothing in GUI.md. If a blocking Avalonia defect ever appears on Windows, the view models are free of Avalonia types, so a WPF view layer could replace the views without touching the logic.

### Rejected alternatives

WPF built on a Windows machine reachable from Linux (verifies that it builds, not how it looks, and needs a machine Konstantin would have to set up); WPF views over a platform-neutral view model library (stays inside ADR-003, but every view defect reaches the reviewer before the author); running WPF under Wine (unreliable for .NET 8 WPF).

## ADR-012 Make garbage agree along tile edges across the variants of a set

### Status

Accepted 2026-10-31.

### Context

The first Windows run of the GUI showed moss patches on the floor stopping dead at every boundary between two different variants in the room preview, because each variant's coverage mask wrapped only with itself. Konstantin offered two ways out: make the garbage connect, or keep it away from the edges.

### Decision

Along the tile edges the coverage mask follows a field shared by the whole set, seeded from the base seed and built with the preset's authored mask scale and softness, and it crosses over to the variant's own field along an irregular curve drawn from a finer noise field of the variant, about half a mask cell from the edge on average. The variant's threshold is the k-th largest of a per-pixel limit, so the covered pixel count stays exactly round(coverage times pixels). A TileRequest therefore carries the set's base seed next to the variant seed.

### Rationale

Keeping garbage off the edges would leave a clean lane along every tile line, and on a mossy floor the eye reads those lanes as a grid, which is the repetition the variants exist to hide. A crossover along an irregular curve means a blob cut by it keeps a noise-shaped edge, which the eye reads as a moss edge, while a crossover along a straight band leaves ledges parallel to the seam that the eye reads as the grid. Drawing the curve from the variant's own seed keeps the cuts from repeating between tiles. It costs two more noise fields and one more sort per tile and nothing in the preset format, and the exact count keeps ADR-004's promise that the slider is honest.

### Consequences

The outermost strip of every variant of a set is the same garbage mask, so the variants differ less near the edges than in the middle, and the larger the mask scale, the wider that strip. A shared blob that neither neighbor continues ends as a half blob a short way into the tile, which is the one artifact that remains and is most visible at high coverage. Two earlier constructions were tried and dropped the same day: a straight distance band, which cut blobs along ledges parallel to the seams whatever its width, and a noise lattice whose edge lines were shared, which kept every contour organic but, with three or four mask cells per tile, repeated whole columns of moss along every seam. A test renders two variants and checks that their masks agree along the edges and differ in the center. Favorites and settings.json already store the base seed, so nothing changes in the files.

### Rejected alternatives

Clearing the edges (visible lanes); one shared mask for all variants (identical garbage in every cell, which the eye catches at once); a straight distance band between the shared field and the variant's field (ledges parallel to the seams); sharing the noise lattice's edge lines (whole columns of moss repeating with coarse masks, and no jitter on the mask scale); a shared threshold with no count correction (coverage off by up to a few percent near the edges, which would break the coverage test); edge-matched tile families in the Wang style (needs the game to place variants by edge type, which the client's import does not do).
