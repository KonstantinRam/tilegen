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

## ADR-013 Add a blocks structure family for coursed masonry

### Status

Proposed 2026-11-03 and accepted the same day by the client's go-ahead, with the understanding that the references guide the look and are not matched exactly.

### Context

The client sent three dungeon references (reference/dungeon_0.PNG, dungeon_1.PNG and dungeon_3.PNG) that show stone walls laid in courses. The courses have different heights, the blocks in a course have different widths, some blocks span two courses, the corners are rounded and the mortar is dark. The pattern family lays uniform rows and columns, so it draws bricks and not this. The cells family draws polygons, so its stones are never rectangular. The field family has no units at all. ADR-002 says that a structure none of the families can express is added as a family in code.

### Decision

Add a fourth family named blocks. Its parameters are rows (the integer number of courses per tile), columns (the average number of blocks per course), sizeVariation (from 0 to 1, how far course heights and block widths move away from uniform), tallChance (from 0 to 1, the share of blocks that extend into the next course), mortarWidth and mortarHeight (as in the pattern family) and cornerRadius (as a fraction of the tile, rounding every block corner). The layout is built once per tile in two passes. The first pass draws from the set seed: the course heights are random weights normalized to sum to one, and every course receives one edge block that crosses the tile's vertical edge, reaching at least 15 percent of its width to either side, which may be tall and then serves as the edge block of the next course as well. The second pass draws from the variant seed: the free span of every course between the two halves of its edge block is cut into blocks of random width, skipping the tall blocks carried from the course above, and a block may extend into the next course only where that course's edge block leaves room. Per pixel the sampler finds the course in a cumulative table, finds the block by binary search over the block starts, computes the rounded box distance to the block border inset by half the mortar with Noise.Sqrt, and returns height, edge weight, cell id and interior distance like the other families, plus a flag that marks the edge block as shared so that the material takes its detail grain from the set seed there. Rows, columns, sizeVariation and tallChance never carry a range: the loader rejects one, the GUI shows these rows without a lock, and the schema says why.

### Rationale

Wrapping holds by construction, because the course heights sum to one, the blocks of every course partition the unit interval, and the edge block is stored unwrapped and matched by adding one to the pixel coordinate. The per pixel path is a table lookup and basic arithmetic, so ADR-008 holds, and Noise.Sqrt is the square root the cells family already uses. Sharing the course heights and the edge blocks between the variants of a set follows the reasoning of ADR-012: in the game two different variants sit side by side, and a course line that jumps at every tile boundary or a block cut into two different halves would give the grid away, while an edge block drawn from the set seed, grain included, is the same block on both sides of the seam. Keeping the pattern family untouched means every earlier export reproduces byte for byte, which a new parameter on pattern could not promise. A Python mock-up in reference/blocks_demo.py, rendered as 2 by 2 wrapped grids in reference/blocks_demo_fieldstone.png and reference/blocks_demo_ashlar.png, settled the layout before the port, and the dungeon preset renders it in out/Dungeon_contact.png.

### Consequences

The schema gains a family with seven fields, four of them without a range, and ParameterDescriptor gains a CanRange flag that the GUI rows read. A TileRequest's set seed now reaches the material samplers, not only the garbage mask. The course lines run the full width of the tile, which the references also show, so a horizontal tile seam looks like any other joint. The first course of a tile never receives a tall block from the last course, because the layout is built in course order, and the contact sheet shows that this is invisible. The lit top edges of the references are not reproduced, because SPEC.md rules out a light direction, and edgeDarkening with cornerRadius stands in for them. A preset author keeps tint and edgeDarkening of a blocks material without a range, because a variant that moved them would show a brightness step across the block that a seam cuts, and the dungeon preset is written that way. The test suite gains an edge agreement test for blocks, a loader test for the rejected range, a round trip test for the new fields and a GUI test for the rows without a lock.

### Rejected alternatives

Extending the pattern family with the new fields (the uniform layout and the random layout share no code, and the reproduction promise for old exports is cleaner with a separate family). A binary space partition of the tile (its first cuts form a full length vertical joint and a full length horizontal joint at the tile edges, which gives the grid away). The cells family with a Chebyshev distance (the cells become axis aligned but their sizes cannot be set per course and their corners meet at arbitrary offsets, so they never read as coursed masonry). Drawing the whole layout from the variant seed (every tile boundary would show jumping course lines and mismatched half blocks). Forcing a vertical joint at the tile edge in every course (a full height joint at every tile boundary is the grid itself).

## ADR-014 Add a timber structure family for half-timbered walls and plank floors

### Status

Accepted 2026-11-03 on the client's request, with the understanding that the reference guides the look and is not matched exactly.

### Context

The client sent two references of a medieval wall (wood_clay.PNG and wood_clay_1.PNG in the reference folder at the time): dark wooden posts and rails with pegs around sand colored plaster panels, a diagonal brace, and a shadow along the frame, and said the panels may as well be white, gray or wood. The pattern family can draw a grid with wide mortar, but its mortar is one flat height, so the beams would carry no grain, and nothing in the four families draws a diagonal or a plank with ends and knots.

### Decision

Add a fifth family named timber with rows and columns (panels per tile), beamWidth, braceChance, grain, pegSize, planks (a boolean) and plankWidth. Posts stand on the lines x equals k over columns and rails on y equals j over rows, so the tile edges run through the middle of a post and a rail. Beams take the height 0.25 plus grain, where grain is value noise with three cells along the wood and 48 across, folded into streaks, so a preset puts its wood colors in the lower part of the ramp and its panel colors in the upper part. A panel is plaster at height 1, or boards of about plankWidth with a groove between them, two plank ends with a middle piece on some boards, and a knot on some boards. A brace runs corner to corner under the frame in a share braceChance of the panels, in either direction. Pegs sit on the posts beside every rail. The frame, its grain and its detail come from the set seed and are identical in every variant of a set. Panels, braces, boards, plank ends and knots come from the variant seed. With beamWidth 0 there is no frame, which with planks gives a plank wall or floor; the panels and boards then come from the set seed too, because they reach the tile edges, and the plank ends and knots keep at least their own size away from every edge, so they alone differ between variants. Only braceChance carries a range.

### Rationale

The frame is what every tile shares with its neighbor, so it must not depend on the variant, and placing it on the lattice lines makes the tile edge the middle of a beam, which is the one place a seam is invisible. Grain comes from value noise with a separate integer cell count per axis, which wraps like every other lattice noise and needs one overload and no new primitive. A brace lies inside its panel and ends under the frame, so it never reaches an edge and may differ per variant. A frameless plank floor whose boards are shared would repeat every tile with nothing else to vary, so the middle piece between two plank ends gives each variant its own boards without touching the edges, and the contact sheet shows that the repeat is gone. Per pixel the family computes distances to lattice lines, one diagonal and one board boundary plus two noise lookups, so ADR-008 holds.

### Consequences

Six presets ship. The booleans ridged and planks have no GUI control and are set in the preset file, which GUI.md now says. The frame is regular, so every tile shows the same posts and rails, and the look of a set comes from braces, panels, stains and garbage. The lit edges of the reference are not reproduced, because SPEC.md rules out a light direction, and edgeDarkening draws the shadow along the frame instead. A timber material with beamWidth 0 and plaster is one flat shared panel and should use the field family instead. The test suite gains an edge agreement test for timber on every edge and a round trip test for the new fields.

### Rejected alternatives

The pattern family with wide mortar (no grain on the beams, no braces, and one mortar height cannot carry wood). A separate planks family (boards, grooves, ends and knots are the infill of a timber panel, so one family covers the wall and the floor). Drawing the frame from the variant seed (every seam would cut a post into two different posts). Sharing the boards of a frameless floor without plank ends (the floor repeated every tile).

## ADR-015 Add a scatter structure family for stones and mounds on a ground

### Status

Accepted 2026-11-03 on the client's request, with the understanding that the reference guides the look and is not matched exactly.

### Context

The client sent a swamp reference (swamp_terrain_1.PNG in the reference folder at the time): rounded mud mounds and pebbles of very different sizes, overlapping, with wide dark gaps between them that carry green specks. A probe with the cells family at full jitter and a wide edge turned the mounds into angular polygons separated by a net of uniform width, because Voronoi cells fill the plane and share every border, and the cells cut by a tile edge showed at every seam. ADR-002 says that a structure none of the families can express is added as a family in code.

### Decision

Add a sixth family named scatter with count (lattice cells per tile), density (the share of cells that hold a stone), size (the stone radius in cells), sizeVariation, jitter (how far a stone may leave its cell center), layers (one to three, each further layer with cells half the size, stones half the size and three quarters of the density), groundHeight and warp. Every cell of every layer may hold one stone, a disc with a dome height that rises fast at the rim and flattens at the top, and a pixel takes the tallest dome among the stones of the 3 by 3 neighborhood of its cell in every layer, or the ground where no stone covers it. The radius stays under one cell, so the neighborhood holds every stone that can reach a pixel. The pixel coordinates pass through a domain warp of amplitude warp times half a coarse cell before the lookup, which turns the discs into blobs. Every cell is drawn twice, once from the set seed with the authored density, size and size variation, and once from the variant seed with the variant's values; where either stone could reach the tile edge, allowing for the warp and for one pixel, the cell keeps the shared stone, otherwise the variant's. The ground, the warp and the shared stones take the set's detail grain. Count, jitter, layers, groundHeight and warp never range, while density, size and sizeVariation may, because the stones along the edges use the authored values.

### Rationale

A pixel inside a stone depends only on that stone, unlike a Voronoi cell, whose shape depends on its neighbors, so sharing the stones that reach the edge is enough to make two variants agree there, and the test checks all four edges with the shipped jitter on. The maximum over domes draws overlap the way pebbles lie on each other, and empty cells give the gaps the reference has and the cells family cannot. The warp moves the shared and the variant stones alike and comes from the set seed, so it keeps the edge agreement. Per pixel the family computes at most 27 distances with Noise.Sqrt and two noise lookups, so ADR-008 holds and a tile renders well within the preview budget.

### Consequences

Seven presets ship. The stones along the edges repeat in every tile of a set, and the contact sheet shows that this reads as ground rather than as a grid, because the interior differs and the warp bends everything. Density, size and sizeVariation are the first ranged parameters whose range reaches only the interior of a tile, and PRESET_SCHEMA.md says so with the family. A stone's dark outline comes from the dome and from edgeDarkening, and the green specks of the reference come from a green stop low in the ramp, which the detail noise samples in the gaps.

### Rejected alternatives

The cells family with full jitter and a wide edge (polygons, a net of uniform width, and seams). Poisson disc sampling (needs a global rejection pass that neither wraps nor splits into shared and variant stones per cell). Drawing all stones from the variant seed (a stone cut by a seam would differ on either side).

## ADR-016 Add a columns structure family that stands in front of another structure

### Status

Accepted 2026-11-03 on the client's request for a fantasy temple with columns, with the understanding that the reference guides the look and is not matched exactly.

### Context

The client sent a side view of a hall (column.png in the reference folder at the time): pale round columns with a capital and a base in front of a dark stone wall. No family draws a column, and a column only makes sense in front of a wall that one of the existing families already draws. Every material has one structure, so a column family needs a way to carry the structure behind it.

### Decision

Add a seventh family named columns with count, width, capital, base, flare, flutes and behind, where behind is a full structure object of any other family. The columns stand at evenly spaced axes that never touch a vertical tile edge and run from the top edge to the bottom edge, with a flat abacus and a flared capital at the top and a flared base and a flat plinth at the bottom, so that the plinth of one tile meets the abacus of the tile below as one block. A shaft takes a round profile with no light direction, full on the axis and falling to nothing at the silhouette, with optional flutes as a sawtooth across it. The structure behind takes heights 0 to 0.45 and the columns 0.55 to 1, so one ramp carries both. The sampler of the structure behind is created through a factory with seeds derived from the column sampler's own seeds, and its sample passes through where no column covers, with its interior distance cut by the distance to the nearest column so that edge darkening draws a shadow beside each column. Along a silhouette the tint and the darkening of a blended pixel come from the column and the wall in proportion to their shares, so that a mortar pixel next to a column depends on nothing drawn from the variant seed. The parameters of the structure behind nest under the path behind in settings.json, in the enumeration, in the edit and in the GUI rows with the label prefix Behind. No field of the columns family carries a range, the column ids come from the set seed, and the columns are identical in every variant of a set.

### Rationale

Nesting keeps the wall authoring in the families that already draw walls and makes a column a layer that can stand in front of ashlar, bricks, timber or plaster alike, which is cheaper than a column option in every family. Keeping the columns away from the vertical edges and identical in every variant means a seam never cuts a column into two different columns, and the top and bottom edges run through the abacus and the plinth, which are flat and the same everywhere. The ramp split follows the timber family, where beams and panels share one ramp in two bands. The blocks family now marks its mortar as shared as well, because the course line along the top and bottom edges is mortar in every variant and the switch between the shared and the variant grain then always lies on a block rim, which the columns test on all four edges required.

### Consequences

Eight presets ship. The parameter path grammar gains one nested level, which the resolver, the enumeration, the edit and the loader handle recursively, and a path such as base.structure.behind.rows is one more row in the GUI. The family behind the columns is set in the preset file, because the GUI has no selector for it. A preset author keeps the detail strength of a columns material under about 0.2, because the detail noise is added after the ramp bands are assigned and would otherwise cross the gap between them. The capital and base are the same simple shapes on every column, and ornament stays out until a setting needs it.

### Rejected alternatives

A columns option on the blocks family (ties columns to one wall family). A second material layer under the garbage (the garbage mask is a coverage mask and cannot place a column). Drawing columns with their own ramp (two ramps per material would change settings.json for every family).

## ADR-017 Let a garbage material be transparent, give cells their own colors, and add star glints

### Status

Accepted 2026-11-03 on the client's request for a fairy dust garbage, with the understanding that the references guide the look and are not matched exactly.

### Context

The client sent two references (dust.png and dust_1.png in the reference folder at the time): a glitter trail of small sparkles in many pastel colors with four point star glints, and a soft magenta glow. A garbage material is opaque wherever the mask covers it (ADR-004), so the floor could never show between the sparkles, and a ramp gives one color per height, so every sparkle would share one hue. No family draws a glint.

### Decision

Three additions, each small and general. A material gains transparency, from 0 to 1: a garbage material with transparency lets the base show through where its structure has no feature, by scaling the blend weight with 1 minus transparency times 1 minus the structure's presence. Presence is 1 inside a feature and 0 on empty ground; the scatter family reports its dome or star shape, so a sparkle fades into the floor at its rim, and every other family reports 1 minus its edge weight, so mortar and borders are the gaps. The measured coverage still counts the mask, so the slider keeps its meaning and the coverage test still holds. A material gains cellColors, a boolean: when true the ramp is read at the cell's random value instead of the height, so every cell takes its own color from the ramp, and the height scales the brightness between a half and full. The scatter family gains stars, the share of stones drawn as four point stars with the ramp's end color instead of domes. Transparency never ranges, because a haze at the tile edges must match between variants.

### Rationale

Transparency as a material field makes any structure a stencil over the base, so the same machinery gives sparkles, rubble on a floor through a transparent mortar, or a haze through a partly transparent ground, and the mask keeps deciding where the garbage lies. Cell colors serve the sparkles and also the stone walls of mixed color that the dungeon reference showed, at the cost of one boolean. Stars are a shape in the family that already places small features at random, so a glint costs one field and no new family.

### Consequences

Nine presets ship. The enchanted preset carries the dust on a dark slate floor and a dark stone wall at transparency 0.8, so a faint magenta haze remains where the dust lies. A preset author who sets transparency on an opaque family such as field sees no change, because that family has no empty ground. The booleans ridged, planks and cellColors have no GUI control. Presence is a new field of StructureSample, and a new family decides what its gaps are.

### Rejected alternatives

A sparkle family of its own (the scatter family already places particles, and the colors and the transparency are material concerns). A separate glow layer (the mask softness and a partly transparent ground give the glow). An alpha channel in the output (the output stays opaque RGB by the spec, and transparency only decides how the two materials of one tile blend).

## ADR-018 Add a crystal structure family

### Status

Accepted 2026-11-03 on the client's request for protruding crystals in many colors, usable as a base and as a garbage, without a reference image.

### Context

The client asked for a crystal style for crystal caves. The scatter family draws round stones, the cells family draws flat facets, and nothing draws a pointed prism that stands out of the ground, so a crystal cave could only be approximated by flat colored cells, which the magic cave preset already does for its wall.

### Decision

Add an eighth family named crystal with count, density, shards, size, aspect, sizeVariation and facet. Every lattice cell may hold a cluster: a center and two to shards shards radiating from it, each a prism of a random direction, drawn as a normalized random vector so that no trigonometric function is needed, with a length in cells, a width as a share of the length, a pointed tip over the last third and a root that starts a fifth behind the center so that the cluster has no hole. A shard's height is a flat top face over the middle of its width, a short bevel, and two side faces of unequal darkness, with the darker side drawn per shard, rising a little toward the tip, so it reads as a protruding prism without a light direction shared by every shard. A pixel takes the shard it lies in, preferring a shard it lies fully inside over the rim of another, and where no shard covers it a soft dark knob at the cluster center, which takes the ramp's bottom value, or the ground. Clusters are drawn twice per cell, from the set seed with the authored density, size, aspect and size variation and from the variant seed with the resolved values, and the cell keeps the shared cluster where either could reach the tile edge, as the scatter family does. With cellColors a shard takes its own ramp color, and the ground and the mortar of every cell family now take the ramp's color at 0, so the first stop is the ground color and a transparent garbage's haze has that color. Count, shards and facet never range.

### Rationale

Clusters of radiating shards are how crystals grow, and a flat top with two unequal side faces is the smallest shading that reads as a prism, while a random darker side per shard keeps the spec's rule against a light direction. The scatter family's sharing rule carries over unchanged, so the family is seam clean from the start and the test checks all four edges with the shipped jitter on. Reading the ground as the ramp's bottom color with cellColors fixes a leak that the crystal rims exposed, where an anti-aliased rim pixel took the height ramp's color.

### Consequences

Eleven presets ship, and the crystal cave preset uses the family as the wall's base and as the floor's transparent garbage. The enchanted and magic cave presets render their haze in the ramp's bottom color now, which is what their documentation already said. A cluster is at most one lattice cell across, so a large crystal needs a small count.

### Rejected alternatives

Flat cells with cellColors (no tips and no faces, already available). Scatter stones stretched into ellipses (no tips and no faces). Drawing shards with Math.Atan2 or Math.Cos (banned by ADR-008).

## ADR-019 Mask a transparent garbage by feature and share crystal clusters by shard

### Status

Accepted 2026-11-03 on the client's report that the crystal garbage reads as small confetti at preview and game scale.

### Context

The client asked for fewer, fatter and bigger crystals in the crystal garbage. Two things stood in the way. The garbage mask cut every feature per pixel, so a bigger cluster was more often a half cluster fading out at the mask's outline. And a bigger cluster more often reaches a tile edge, where the rule of ADR-015 and ADR-018 hands the whole cluster to the set seed, so the variants of a set shared most of their clusters and a room showed the same crystals in every tile.

### Decision

A transparent garbage whose structure places free standing features, which the scatter and crystal families do, draws each feature whole or not at all. The mask is read once per feature under its center, and a feature the mask leaves out is removed from the sampler before any pixel is drawn, so it never covers a feature that is drawn. A feature that reaches a tile edge takes its verdict from the shared field alone, which every variant of the set computes identically, and any other feature takes the variant's own mask, which includes the shared field near the edges. The haze of a partly transparent garbage still follows the mask per pixel, and the measured coverage still counts the mask. The verdict reaches the samplers as a filter passed through the sampler factory, and an opaque garbage passes none, so its outline follows the mask as before.

The crystal family's edge test follows the shard directions. A cluster is at the edge when the bounding rectangle of any shard, with the rim pixel the sampler draws, or the knob under the cluster can reach the outermost pixel ring, instead of a square reach of the longest shard in every direction. In a cluster at the edge only the center, the knob and the shards that reach the edge come from the set seed; every other shard is drawn again from the variant seed at the same center, and keeps the set's shard where the variant's would reach the edge. The aspect of a shard may now go up to 1.

### Rationale

A crystal, a coin or a leaf cut by a soft mask outline is a visible defect once the feature is large enough to read, while a feature that is present or absent looks placed. Reading the mask under the center costs nothing, because the samplers know every feature before the first pixel. Removing a hidden feature in the sampler rather than in the blend is what keeps it from punching a hole into a visible feature where the two overlap. The verdict for a feature at the edge must be the same in every variant, because the two tiles on either side of a seam must agree on it, and the shared field is the only mask every variant computes alike, while a feature away from the edges can follow the variant's own mask, which keeps the variants different. Sharing a cluster by shard keeps exactly what the seam needs and returns the rest to the variant, so a bigger crystal no longer means a repeated one.

### Consequences

Every transparent scatter or crystal garbage changes its look slightly, because dust glints, leaves, coins, bones, caps and petals are now whole. The crystal cave floor has five cells, up to four shards per cluster at a size of 0.85 cells and an aspect of 0.7, and the frozen fortress frost has five cells at an aspect of 0.55. An author who wants bigger crystals lowers the count and stops at five cells, because at four cells nearly every cluster reaches an edge and a room repeats the centers and the crossing shards in every tile. The test plan gains three cases: a single transparent cluster renders in full or not at all at half coverage, and the edges of two variants agree for a transparent crystal garbage and for a transparent scatter garbage over a flat base.

### Rejected alternatives

Reading the mask under the center in the blend without removing hidden features (a hidden cluster then shows the base through a visible one where they overlap). A per variant verdict for the features at the edge (the two tiles on either side of a seam would disagree). Keeping the whole cluster shared at the edge (the variants then repeat most clusters once the clusters are large). A feature free margin along the edges (the margin shows as lanes in a room).
