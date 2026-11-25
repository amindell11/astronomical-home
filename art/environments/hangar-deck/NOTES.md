# Hangar deck

## Blinking status lamps, 2026-11-23

After flying the indicator build the owner found it too many lamps and too bright, so the status lamps lose their meaning. The left group is back to three lenses on its 0.92 seat, at x -0.27, 0 and 0.27, with the original names and materials; the geometry matches the pass before the indicators. All six status lenses carry an `export_role` (`StatusLamp1`–`StatusLamp3` for the left group and `StatusLamp4`–`StatusLamp6` for the right, each left to right), so each exports as its own part and as a manifest lamp with a glow card. Unity blinks them on Perlin noise in their exported colours. The manifest is back to twelve lamps. The "Ship and screen status lamps" section below is superseded.

## Window etching follows the visible frame, 2026-11-23

The owner asked for the window etchings to follow the edge of the window. Before this pass, `build_glass.py` laid the marks out against each pane's glass outline. That outline is hidden under the strut centre lines, the header and the sill. The marks therefore sat at an inset from edges nobody can see, and about a third of the stroke area was on glass behind hardware.

`add_glass` now ray-casts the hardware from the game camera on a 2-pixel grid inside each pane outline and traces the open sky. It simplifies the boundary to straight edges and fits a line to each. It keeps the frame edges: header, gussets, struts and pillars. Edges with open sky above them, meaning the sill and console tops, are dropped. Only the right-hand panes and the right half of the centre pane are traced. The left side is their mirror image, so tracing noise cannot break the deck's symmetry. Each pane's traced frame is recorded as `visible_frame` in `previews/window-etching-layout.json`.

All marks sit at a constant 22-pixel inset from that visible frame:

- Corner brackets mark every inset corner that turns by more than about 11.5 degrees, including both ends of each header gusset. Leg length is unchanged.
- The broken lines run along the inset strut and pillar edges, using the existing length rule.
- The tick row and hexagon cluster run along the longest header edge with the existing offsets. The centre cluster sits at the right end by the right strut. Side and outer clusters sit at the inboard end on both sides.
- No marks follow the sill, which the console hides.

Stroke strengths, widths, feather, hexagon sizes and the hologram keep-out are unchanged. `add_glass` asserts that each mark's ends and midpoint see open sky, so the build fails if a mark would land on hardware.

`build_deck.py -- --no-render`, `export_glass.py`, `verify_glass.py` and `verify_glass_projection.py` pass. Each checked view still has zero glass-ahead-of-hardware and zero tinted hardware pixels; the 204 uncovered window-edge pixels in each wide view are unchanged. A dense ray check found 7,461 of 21,510 stroke samples behind hardware with the old layout and none of 15,021 with the new one. The 4096 by 2048 mask has 20,395 nonzero texels, all inside strokes. Its SHA-256 is `6eb7d977dfcd0330cf2ec5fb4d3eb8af45610988405c0fe0290340de57cd8136`. `WindowEtching.png` and `ViewportGlass.fbx` in the Unity console visuals were re-exported, and the console prefab was rebuilt.

## Ship and screen status lamps, 2026-11-23

Superseded by "Blinking status lamps" above. The owner turned the two pod status groups into indicators. The left group now has five lenses, one per offered ship, named `Deck | Left ship 1 lens` to `Deck | Left ship 5 lens` from left to right. They keep the old lens size, socket scale and 0.27 pitch, at x -0.54 to 0.54 on a status seat widened from 0.92 to 1.29. The seat still clears the ramp screws at x ±0.73. The right group keeps its geometry: its three lenses stand for the ship, primary and ordnance screens.

Each lens carries an `export_role` (`ShipLamp1`–`ShipLamp5`, `SlotLamp1`–`SlotLamp3`). The exporter's `part_names` lists them, so each exports as its own part and also as a manifest lamp with a glow card, like the Launch lamps. Unity colours them at runtime, so their Blender material is only a placeholder. The manifest now lists fourteen lamps.

Verifier results are unchanged in kind. `verify_deck.py` now stops on `KeyError: 'ShipLamp1'`, its parts palette having no entry for the new roles. `verify_pedestal.py` still stops on the corner indicator material. Neither verifier was edited.

## Trial warm gusset lamps, 2026-11-22

A trial the owner asked for on top of the corner strips, kept in its own commit so it can be dropped. One small warm-white lamp per side sits on the flat header face just outboard of each overhead strip's outer end, where the strut top meets the header, centred at `(±6.54, 3.325, 10.24)`. It uses the console's usual lamp style: a 0.30 by 0.075 lens in a socket with a lip at 0.7 scale. Its material is `Deck | gusset warm white lamp emission`, linear `(1.0, 0.82, 0.62)` at strength 0.7, dimmer than the orange Launch key. The lens name ends in ` lens`, so it exports as a lamp with a glow card; the manifest now has twelve lamps and two downlights. Verifier results match the corner-strip pass.

## Header corner strips and indicators, 2026-11-22

The owner found the two upper corners of the 16:9 view too dark and chose this detail from two options. Each outboard header cassette gets a short cold-white strip under its service hatch, centred at x ±11.19, 0.16 above the cassette's lower edge. The lens is 1.56 by 0.055 with `Deck | corner cold strip emission`, linear `(0.28, 0.36, 0.48)` at strength 0.65, the header strips' colour. It sits in a dark bezel 1.70 by 0.13. The lens name ends in ` lens`, so the exporter lists it as a lamp and Unity gives it a glow card. The manifest now has ten lamps and two downlights.

Beside the inner cassette's hatch, at x ±9.75 and mid-height, a small seat holds three indicator pips: one green and two cold white, linear `(0.025, 0.70, 0.055)` and `(0.45, 0.72, 1.0)` at strength 0.9. They are body finish solids, painted into the emission map, with no glow cards. Nothing adds a real light, because the console's eight light slots are full.

The rebuild ran the same verifiers as the orange-key pass. Coverage, ink, glass and glass projection pass. `verify_deck.py` still stops on `KeyError: 'LaunchLampLeft'`. `verify_pedestal.py` now stops earlier, with `KeyError: 'Deck | corner indicator green emission'`: the pips join the body, and its protected-surface comparison has no entry for a material added after `pedestal-baseline.json`. Neither verifier was edited.

## Orange Launch key again, 2026-11-22

The owner chose to go back to the orange key. `08 | Launch broad beveled button` uses `Deck | orange launch resin` again, linear `(0.95, 0.20, 0.018)` with emission strength 1 and black base colour, as it did before the black-key pass. `19 | Launch legend` is back on the shared `Deck | ivory inlaid legends` material, and both objects are back in their ink collections. The black resin and yellow legend materials are no longer built. Everything else stays: the `LaunchLampLeft` / `LaunchLampRight` roles and lamps, the pedestal strips and ringed lens, and the projector.

The rebuild ran `build_deck.py`, the pedestal-recipe verifiers and the three assemblers. `verify_coverage.py`, `verify_ink.py`, `verify_glass.py` and `verify_glass_projection.py` pass. Two verifiers fail for reasons older than this pass. `verify_pedestal.py` reports `protected_objects_exact` for only the two Launch lamp lenses, because their `export_role` properties postdate `pedestal-baseline.json`; every material in that baseline matches, the orange resin included. `verify_deck.py` stops with `KeyError: 'LaunchLampLeft'` because its parts-sheet palette has no entry for the lamp roles; the lamp-role pass added one only in memory. Neither verifier was edited.

The shared exporter wrote `Console.fbx`, `ConsolePaint.png` and `ConsoleEmission.png`. `console-export.json` is unchanged. Unity's `HangarConsoleBuilder` then rebuilt the prefab.

## Separate Launch lamp roles, 2026-11-21

The owner wants the two red lamps beside LAUNCH to blink blue when pressed. `build_deck.py` now registers the existing lenses in `PARTS` as `LaunchLampLeft` and `LaunchLampRight`, with matching `export_role` properties. Their names remain `Deck | Left Launch armed red lens` and `Deck | Right Launch armed red lens`. Both remain in `finish_lamps` under those names. Their sockets and guards remain in `ConsoleBody`.

Each lens is still one connected, closed, consistently wound solid. The scene comparison permits only the two role changes. All 51 objects otherwise match exactly, including geometry, transforms, material assignments, UVs and properties. Materials, preview lights, camera settings, glass shader, collection memberships and ink settings also match. Registration preserves the lamps' existing collection memberships because the usual `register()` helper would add visible ink. `previews/launch-lamps-audit.json` records the comparison.

Both lenses retain `Deck | armed red emission` and linear RGB `(1.0, 0.025, 0.008)`. Their world centres remain `(-1.4300, -3.8984, 1.3203)` and `(1.4300, -3.8984, 1.3203)`. Their local sizes remain `(0.1500, 0.1600, 0.0300)`.

### Export result

The unmodified shared exporter completed with exit code 0 and wrote `Console.fbx`, `ConsolePaint.png` and `ConsoleEmission.png` into directory-local scratch output. Both current lamp names appear in its `lamps` list with unchanged positions, sizes and colours. The exporter reports world-axis bounding-box sizes of `(0.1500001, 0.1561806, 0.0993687)` for these tilted lenses. Its manifest has eight lamps and two separate downlights.

Neither new part appears in that export. `export_console.py` ignores `export_role` and uses its hard-coded `part_names` dictionary. Unlisted lenses still join `ConsoleBody`. To export the two separate parts, that dictionary needs exactly these entries:

```python
'LaunchLampLeft': ['Deck | Left Launch armed red lens'],
'LaunchLampRight': ['Deck | Right Launch armed red lens'],
```

The shared exporter is unchanged. Its manifest always writes beside the script, so this run used a byte-identical scratch copy inside this directory. The exporter SHA-256 was `24a060d1868e8d72b8c2fa6cf1803e2a70fb6b2d92512231ef24fc65a14b6d10`. `previews/launch-lamps-export.json` preserves the manifest, missing-part result and output hashes.

### Verification

The hardware remains at 24,296 triangles in 31 meshes. All solids are closed, consistently wound and clean. The mesh audit found no positive-volume intersections across 33 checked pairs or during control travel. Six-aspect coverage, ink, glass geometry and glass projection checks pass. Every checked view has zero glass-ahead-of-hardware pixels and zero tinted hardware pixels.

This run rebuilt with `build_deck.py -- --no-render`, then rendered the ten saved-scene views with the unchanged baked glass mask connected in memory. It ran `verify_deck.py`, `verify_coverage.py`, `verify_ink.py`, `verify_glass.py`, `verify_glass_projection.py`, `render_finish5.py` and `render_glass.py`. The earlier finish verifiers still freeze superseded geometry or materials, so the exact scene comparison checks preservation against the scene at the start of this task.

The verification runner added `LaunchLampLeft` and `LaunchLampRight` to the mesh verifier's parts-sheet palette in memory, using `ff4040` and `ff8585`. All geometry checks remained unchanged. The parts-sheet assembly likewise adds the two callouts and room for their labels in memory. Neither verifier nor assembler source was edited. The sheet distinguishes authored roles from the shared exporter's current missing mappings.

The camera, construction, mask, coverage, ink, neutral geometry, glass and review previews are regenerated. Historical originals, references and the baked etch mask remain frozen. All 58 PNGs validate. `previews/launch-lamps-verification.json` records the checks and final source, scene and image hashes. All Blender processes started for this task exited. All authored writes stayed inside this directory. No network, git write commands or Unity assets were used.

Automatic approval review rejected the checked, directory-local cleanup command with "blocked by policy". `.lamp-scratch`, `.temp`, `.blender` and `__pycache__` remain. They hold temporary scripts, logs, the scene-comparison snapshot, exporter outputs, render inputs, Blender resources and Python caches.

## Black Launch key and yellow lettering, 2026-11-21

The owner chose a black key with the existing regular LAUNCH letters in warm yellow. The mock-up's coloured rim and altered letter shapes are not used.

`08 | Launch broad beveled button` now uses `Deck | near-black launch resin`, with linear base colour `(0.006, 0.008, 0.012)` and emission strength `0`. Its letter recess walls use the same resin. `19 | Launch legend` uses its own new material, `Deck | warm yellow launch legend emission`, with linear emission `(1.0, 0.72, 0.18)`, strength `1.20`, and black base colour. The shared ivory legend material is unchanged.

The lettering retains its exact vertices, faces, size, spacing and transform. Both Launch objects are excluded from their ink collections. The key's letter-shaped recesses otherwise draw ink across the emissive letters, even when the legend itself has no ink. Removing those two collection memberships prevents that outline, fix-ladder rung 1. No outline geometry, coloured rim or border was added. The three linesets and all other collection memberships are unchanged.

The scene comparison in `previews/launch-audit.json` verifies all 51 objects' geometry, transforms, names, roles and properties against `previews/launch-baseline.json`. All 19 other Principled materials, the glass shader and preview lights match exactly. The two red armed lamps, guards, housing and pedestal are unchanged. The baseline is the frozen scene before this colour pass.

`previews/deck-launch-compare.png` contains labelled before and after crops from the 1920 by 1080 game camera. Each crop uses pixels `(750, 885)` through `(1170, 1040)` and exactly 3x nearest-neighbour enlargement. `deck-launch-original.png` preserves the previous camera image.

The mesh, control-travel, six-aspect coverage, ink, glass geometry and glass projection checks pass. The hardware remains at 24,296 triangles in 31 meshes, with no positive-volume intersections across the 33 checked pairs or during control travel. Coverage has zero alpha loss and zero unexplained gaps. Glass remains behind the hardware, with zero tinted hardware pixels.

The six camera views, four construction views, masks, parts sheets, coverage and ink views, neutral geometry view, glass previews and review sheets are regenerated. The baked etch mask and historical originals remain frozen. The current camera renders use that mask in memory with the existing glass emission and preview lighting. No preview-only changes were saved. `previews/launch-verification.json` records all checks, 58 verified PNGs and final source and scene hashes.

For this finish, run `build_deck.py`, then `verify_deck.py`, `verify_coverage.py`, `verify_ink.py`, `verify_glass.py` and `verify_glass_projection.py`. Run `render_finish5.py` and `render_glass.py`, then assemble with `assemble_coverage.py --require-closed --finish4`, `assemble_glass.py` and `assemble_pedestal.py`. Use the directory-local Blender resources and temporary settings below. The older finish and pedestal snapshot verifiers freeze the orange Launch material. The pedestal verifier passed before this edit; `launch-audit.json` records exact preservation against that verified scene after the material change.

All authored writes stayed inside this directory. No network, git write commands or Unity assets were used. All Blender processes started for this task exited. Automatic approval review rejected the checked, directory-local cleanup command with "blocked by policy". `.temp` and `.blender` remain, containing temporary scripts, logs, render inputs and Blender resources. No `__pycache__` folders remain.

## Pedestal edge lights and ringed lens, 2026-11-21

The owner's two projector references show a cleaner transition from the lens to the pedestal. This pass changes that hardware only. The beam and hologram remain preview placeholders.

Eight inlaid white-cyan strips follow the upper front lips. Each tier has one short strip at each front chamfer. The lowest tier also has two mirrored 0.63-unit front strips. All strips are 0.035 units tall, recessed 0.010 into matching cuts, with their faces 0.001 above the riser. Their new material has constant, unlinked linear emission `(0.55, 0.85, 1.0)`, strength `0.80`, and black base colour. They belong to `ConsoleBody`; no new object ends in ` lens`, and the ten exporter lamps are unchanged.

The top now has a closed dark annulus with 64 segments, an outer radius of 0.55 and an opening radius of 0.447. Its bevelled lip reaches Z 2.525. The lens face is recessed 0.0325 below that lip. `09 | Projector lens` retains its exact name, `ProjectorLens` role, transform, 0.43 radius and original cyan emission. Its three concentric grooves are 0.015 wide and 0.007 deep, at outer radii 0.365, 0.290 and 0.215. The centre remains bright, and the entire lens is one connected closed solid. Its bounding-box centre is `(0.0000, 0.0000, 2.4800)` both before and after.

The three stepped tiers, dark slots and fasteners remain. All 49 other scene objects, every existing Principled material, the glass shader and all three linesets match the captured scene exactly. The body retains 344 unchanged connected components. Boolean reconstruction changes some triangle splits and floating-point coordinates elsewhere in the joined structure. A bidirectional, material-aware comparison of the protected surfaces measures a maximum difference of 0.00000583 units. No protected shape or paint was intentionally changed.

### Review and checks

`previews/deck-pedestal-compare.png` places labelled before and after crops from the 1920 by 1080 game camera beside both owner references. Camera crops are enlarged exactly 3x with nearest-neighbour sampling. Both use identical preview lighting and placeholders. `deck-pedestal-original.png` and `pedestal-reference-1.png` / `pedestal-reference-2.png` are frozen inputs.

The hardware has 24,296 triangles in 31 meshes. All solid meshes are closed and consistently wound, with no loose vertices, zero-area faces or modifiers. The separate-object intersection and control-travel checks pass. The lens is one connected positive-volume solid. The edge-strip audit verifies eight mirrored closed inlays, their colour, restrained strength and thickness.

Coverage passes at 16:10, 3:2, 4:3, 16:9, 21:9 and 32:9, with zero alpha loss and zero unexplained gaps outside the frozen openings. Ink, glass geometry and glass projection checks pass. The unchanged baked etch mask remains in use. The six camera views, four construction views, masks, coverage views, parts sheet, ink views, neutral geometry view, glass previews and existing review sheets are regenerated. Historical originals and reference directories remain frozen.

`previews/pedestal-audit.json` records preservation, lens and edge-strip checks. `previews/pedestal-verification.json` records the completed checks and final artifact hashes. `pedestal-baseline.json` is the frozen pre-edit scene snapshot and protected body surface; do not recapture it during a rebuild. Earlier finish verifiers freeze the old lens geometry, so `verify_pedestal.py` now checks this pass against its immediate predecessor using their existing snapshot and component routines.

### Rebuild

Use the directory-local resource and temporary settings from the recipe below, Blender 5.1 headlessly, and `--python-exit-code 1`.

1. Run `build_deck.py` to save the scene and regenerate ten views. `--no-render` saves geometry only.
2. Run `verify_pedestal.py`, `verify_deck.py`, `verify_coverage.py` and `verify_ink.py`.
3. Run `verify_glass.py`, `verify_glass_projection.py`, `render_finish5.py` and `render_glass.py`.
4. With ordinary Python, run `assemble_coverage.py --require-closed --finish4`, `assemble_glass.py` and `assemble_pedestal.py`.
5. Remove local Blender resource, temporary and Python cache folders after those processes exit.

This delivery rendered camera and construction views from the saved scene with the existing baked etch mask connected in memory. Glass emission, preview lights, camera and ink settings were unchanged; no preview-only material changes were saved. The procedural glass shader remains in `deck.blend`.

All authored writes stayed inside this directory. No network, Unity edits or git write commands were used. All Blender processes launched for this task exited. Automatic approval review rejected the checked, directory-local cleanup command with "blocked by policy". `.temp`, `.blender` and `__pycache__` remain. They contain the original scene used for comparison, render inputs, logs, temporary scripts, Blender resources and Python caches. The completed deliverables are the source files, `deck.blend` and `previews/`.

## Finish 7, two dim header strips, 2026-11-20

The six small header downlights and seven cold-white structural dashes are removed. Their lenses, sockets, lips and dash-only cuts are gone. The pod frame plates now continue across their old dash openings. The two red LAUNCH lamps and both three-LED status groups retain their exact geometry, transforms and materials.

Two mirrored strips sit in the sloped underside above the centre pane. Each lens is 5.40 units long, 0.16 tall and 0.022 deep. A 5.60 by 0.36 dark trough surrounds it. The lens front is recessed 0.010 units below the original underside plane. The trough replaces the four vents crossed by the strips; the surrounding cassette plates remain. The strips end clear of the struts.

The existing `Deck | warm white downlight emission` material name remains for continuity. Its unlinked Principled Emission Color is now linear `(0.28, 0.36, 0.48)`, with strength `0.65`. All other material values remain unchanged. Two rectangular preview lights, 45 watts each with the same colour, replace the six 165-watt preview spots. The key light and ambient settings remain unchanged. The exporter reads the colour directly, without multiplying by Blender emission strength.

### Final lamp list

Positions are world-space lens bounding-box centres. Sizes are local width, height and depth in model units. Colours are linear RGB before emission strength. These ten named lenses are the exporter lamp anchors, including exactly two downlights.

| Lamp object | Position X, Y, Z | Size W, H, D | Linear RGB |
|---|---|---|---|
| `Deck \| Header downlight 01 cold white lens` | (-3.2000, 3.6351, 9.9684) | (5.4000, 0.1600, 0.0220) | (0.280, 0.360, 0.480) |
| `Deck \| Header downlight 02 cold white lens` | (3.2000, 3.6351, 9.9684) | (5.4000, 0.1600, 0.0220) | (0.280, 0.360, 0.480) |
| `Deck \| Left Launch armed red lens` | (-1.4300, -3.8984, 1.3203) | (0.1500, 0.1600, 0.0300) | (1.000, 0.025, 0.008) |
| `Deck \| Left status 1 green lens` | (-3.9225, -1.8565, 1.8196) | (0.0600, 0.0475, 0.0300) | (0.025, 0.700, 0.055) |
| `Deck \| Left status 2 green lens` | (-3.6569, -1.8893, 1.8550) | (0.0600, 0.0475, 0.0300) | (0.025, 0.700, 0.055) |
| `Deck \| Left status 3 amber lens` | (-3.3912, -1.9221, 1.8904) | (0.0600, 0.0475, 0.0300) | (0.450, 0.720, 1.000) |
| `Deck \| Right Launch armed red lens` | (1.4300, -3.8984, 1.3203) | (0.1500, 0.1600, 0.0300) | (1.000, 0.025, 0.008) |
| `Deck \| Right status 1 green lens` | (3.3912, -1.9221, 1.8904) | (0.0600, 0.0475, 0.0300) | (0.025, 0.700, 0.055) |
| `Deck \| Right status 2 green lens` | (3.6569, -1.8893, 1.8550) | (0.0600, 0.0475, 0.0300) | (0.025, 0.700, 0.055) |
| `Deck \| Right status 3 amber lens` | (3.9225, -1.8565, 1.8196) | (0.0600, 0.0475, 0.0300) | (0.450, 0.720, 1.000) |

The separate projector lens remains at `(0.0000, 0.0000, 2.4800)`, size `(0.8600, 0.8600, 0.0250)`, linear colour `(0.035, 0.480, 0.720)`, strength `1.65`. No emissive structural dashes remain. Legacy `amber` identifiers in the status lamps still denote cold-white light.

### Review and rebuild

All 46 unrelated scene objects, 18 unrelated Principled materials, the three linesets and the glass shader match the frozen snapshot exactly. Both strips pass camera visibility checks. The hardware has 20,880 triangles in 31 meshes; its solids are closed, consistently wound and clean. The 33 tested object pairs and control-travel checks have no positive-volume intersections.

Coverage passes at 16:10, 3:2, 4:3, 16:9, 21:9 and 32:9, with zero alpha loss and zero unexplained gaps outside the frozen window openings. Ink checks pass. The rebaked etch mask retains SHA-256 `a011b0a4b2e01b25423251c0dd2a42810a46c2309a5a4f7efdfa8a0ac0133a0d`. Glass placement still has 0.10 units of depth clearance and zero glass-ahead-of-hardware pixels. The 204 pre-existing uncovered window-edge pixels in each wide view remain unchanged.

`previews/deck-finish7-compare.png` pairs the frozen previous 16:9 render with the new render. Two full-width header crops below them are enlarged exactly 2x. The frozen input is `deck-finish7-original.png`. Blender previews retain the existing placeholder screens and hologram. During camera renders, the preview backdrop moves behind the corrected glass; its exact transform is restored before saving. This delivery's camera pass sampled the verified baked etch mask in memory, retaining the original glass emission colour and strength. The saved procedural glass material is unchanged.

Use the directory-local Blender resource and temporary settings from the rebuild recipe below. Run Blender headless with `--python-exit-code 1`:

1. Run `build_deck.py` for the saved scene and ten camera/construction views.
2. Run `verify_finish7.py`, `verify_deck.py`, `verify_coverage.py` and `verify_ink.py`.
3. Run the local glass export recipe below, followed by `verify_glass.py`, `verify_glass_projection.py` and `render_glass.py`.
4. Run `render_finish5.py` for the existing neutral geometry preview.
5. With ordinary Python, run `assemble_glass.py`, `assemble_coverage.py --require-closed --finish4` and `assemble_finish7.py`.
6. Remove temporary exports, resource folders and caches after the processes exit.

`verify_finish7.py` compares protected objects, material values, linesets and the glass shader with `finish7-baseline.json`, checks closed solids, and casts camera rays across both lenses. Keep that baseline frozen. Earlier finish verifiers assert superseded lamp schedules. The finish 4 coverage mode still applies to the retained paint palette. `assemble_finish7.py` refreshes legacy review filenames with current labels; historical originals and reference directories stay frozen.

The final file check verified all 56 current PNGs. `previews/finish7-verification.json` records their sizes and hashes, the source and scene hashes, and the passing checks. The full build and completed preview passes exited 0. Freestyle retained its side-view empty-stroke warning and reported corrected duplicate edges; the construction views and neutral geometry view were inspected.

All authored files stayed inside this directory. No Unity assets, network calls or git write commands were used. Automatic approval review rejected temporary-folder cleanup with "blocked by policy". `.blender`, `.temp` and `__pycache__` remain. They contain Blender resources, temporary exports, render inputs, copied camera renders and Python caches. All Blender processes started for this task have exited.

## Glass placement, 2026-11-20

The five canopy panes were positioned from the strut and pillar anchors with small Y offsets. Those offsets did not clear the complete hardware. The outer panes crossed the wide side structure, and parts of the centre and side panes crossed the header. At pixel centres, glass was ahead of hardware at 2,313 pixels in 16:9 and 57,199 pixels in each wide view. The previous coverage check skipped hardware rays, and the mark-only occlusion check never tested clear parts of a pane.

`build_glass.py` now scales the glass uniformly about the fixed game-camera eye. The scale is calculated from the hardware's farthest camera depth and the glass's nearest camera depth, leaving 0.10 units of clearance. The current factor is 1.274858742. This is fix-ladder rung 1: the whole glass depth range is behind the whole hardware depth range. The transformation preserves the pane outlines and etch positions in projection at every aspect ratio. It also preserves the five planar faces, local mesh, UV islands and shader. All 65 other scene objects, hardware materials, camera and ink settings match the pre-fix snapshot. `previews/glass-preservation.json` records that comparison.

The mask was not carrying a base film. All 26,743 nonzero texels of that pass's mask belonged to finite etch strokes. Empty areas, unused atlas space and the three-texel island margins are exactly black. UVs remain inside 0..1 without overlap, and every pane faces the camera. That pass's bake kept SHA-256 `a011b0a4b2e01b25423251c0dd2a42810a46c2309a5a4f7efdfa8a0ac0133a0d`; the current mask and its hash are in "Window etching follows the visible frame" above. The atlas dimensions below describe local mesh units; after the placement scale, world-space density is approximately 80.36 texels per unit.

The supplied game's broad pale wash did **not** reproduce with this scene, this baked mask and the supplied shader equation, even before the placement fix. All offending hardware pixels sampled black. The confirmed defect is pane placement, not a demonstrated explanation for that uniform wash. Runtime texture, mesh and material bindings were not changed or verified in Unity.

`previews/deck-glass-check.png` contains labelled 1920 by 1080, 2560 by 1080 and 3840 by 1080 game-camera renders over a bright backdrop. Glass uses flat RGB `(0.30, 0.38, 0.42)` with alpha equal to the baked mask times 0.6. There is no refraction, lighting contribution or additive etch shader. The preview-only sky is moved farther away in memory so it remains behind the corrected panes. The saved scene's backdrop is unchanged. The regular etching previews now use the same alpha-blended glass over the dark sky.

`verify_glass_projection.py` checks all 8,985,600 pixel centres across the three views. Each view has zero glass-ahead-of-hardware pixels and zero tinted hardware pixels. The independent continuous depth bound also prevents overlap between pixel centres. The report is `previews/glass-projection-audit.json`; `glass-projection-before.json` records the failing original placement. There are 204 pre-existing uncovered window-edge pixel centres in each wide view, unchanged by this fix. They have no glass and therefore no glass tint. The existing sampled window-coverage check still passes.

### Verification and rebuild

Keep Blender resources and temporary files inside this directory, as in the rebuild recipe below. Use headless Blender with `--python-exit-code 1`.

1. Run `build_deck.py -- --no-render`.
2. Open `deck.blend` without saving and run `../hangar-console/export_glass.py -- --assets <temporary-output-directory>`. Copy `WindowEtching.png` to `previews/deck-window-etching-mask.png`.
3. Run `verify_finish5.py -- --glass-placement` and `verify_finish4.py -- --glass-placement`. This explicit option permits only the canopy's transform and world centre to differ from the frozen finish records. Local geometry, UVs, materials and all other preservation assertions remain active.
4. Run `verify_recess.py -- --finish4`, `verify_glass.py`, `verify_glass_projection.py`, `verify_deck.py`, `verify_coverage.py` and `verify_ink.py`.
5. Run `render_glass.py`. With ordinary Python, run `assemble_glass.py`, `assemble_previews.py`, `assemble_coverage.py --require-closed --finish4`, `assemble_finish4.py` and `assemble_finish5.py`.
6. Remove the temporary exports, Blender resources and scratch directories after every process exits.

The temporary FBX was reimported into Blender. Its world vertices match within 0.000002 units and its UVs match exactly, as recorded in `previews/glass-export-audit.json`. The current results and hashes are recorded in `previews/glass-verification.json`. Earlier finish sections below describe their own deliveries and frozen evidence.

All listed checks passed. The six hardware silhouettes have zero added or removed pixels. The 42 hardware meshes retain 21,896 triangles; the 44 tested object pairs and the control-travel checks have no positive-volume intersections. The three bright renders show no pane-wide film or hardware tint.

Automatic approval review rejected scratch cleanup with "blocked by policy". `.glass-check`, `.temp` and `__pycache__` therefore remain inside this directory. They contain probe scripts, logs, temporary exports, render inputs and caches. No Unity assets, network calls or git write commands were used.

## Finish 5, clean console geometry

The current model removes the console's cracked, overlapping linework while retaining the finish 4 palette. The owner's report was "Also the console geometry looks a bit broken now". The supplied game crops identified the right pod base, wings and front band, switch-row ledges, and the hook below the left pod.

The cause was layered geometry. Inset plates copied corners from twisted surfaces, so their edges crossed the underlying faces. The pod heels fanned forward across the band. The right heel also used an obsolete half-unit height offset. The raised control surrounds combined shallow housings with skewed shoulder skirts. Small vents, cable saddles, plate margins and loose wear strokes added more nearly parallel lines.

The fix uses rung 1 of the fix ladder. The conflicting layers are removed, and the four replacement supports are planar solids. It does not hide the errors with ink settings or a colour change.

- The band and wings retain their broad original facets. Their overlay plates, utility covers, rear cable runs, vents and loose marks are removed.
- The ledges lose the under-switch vent and plate stack. Each raised control surround is now one deep solid, with the existing opening, control position and top outline preserved. Both sides use the same construction.
- Both pod bases are compact vertical supports below the housing edge. They replace the forward heel fans and bury their joins inside the band and shell. The left hook and doubled edge are gone.
- The strut shoulders retain their original faceted solids. Their overlapping armour plates, keepers and pins are removed. The struts above them are unchanged.

### Review evidence

`previews/deck-finish5-crops.png` compares the lower-left and lower-right quarters of the 1920 by 1080 game-camera render before and after. Each quarter is enlarged exactly 2x, with labelled rows. The companion `deck-finish5-geometry-crops.png` uses one neutral material so dark paint cannot conceal a broken edge. `deck-finish5-compare.png` shows both full frames. The current half-size and 21:9 views are `deck-finish5-half.png` and `deck-finish5-21x9.png`.

The geometry was reviewed in the neutral ink view and revised until the heel needles and short strokes below the raised surrounds disappeared. The finished console has no detached detail strokes, near-parallel plate outlines or skewed vent stacks in the reviewed areas. Remaining lines follow the facets, openings and clear steps of the form.

### Preservation

The construction audit records 389 unchanged inputs, 92 removed detail pieces and four replaced supports. There are no other changed inputs. This includes the protected pod shells and frames above their bases, header, upper struts, pedestal, LAUNCH housing and guards. All 65 scene objects other than the joined body match the captured scene exactly, including controls, lamps, screen glasses, window glass, camera and preview objects. Material values, ink settings and the glass shader also match exactly.

`previews/finish5-construction-audit.json` records the input comparison. `previews/finish5-audit.json` checks the saved scene against `finish5-baseline.json`. The two closed inward-facing pod cavity surfaces are unchanged. New connected surfaces have positive volume; the complete hardware solids are closed and consistently wound.

### Geometry checks

The hardware contains 21,896 triangles in 42 meshes. All 38 hardware solids are closed, consistently wound and positive-volume; the four screen glasses remain intentionally planar. There are no loose vertices, degenerate faces or modifiers. The independent audit checked 44 potentially intersecting object pairs and found no positive-volume intersections. Control travel also remains clear.

At the saved alpha threshold, all six silhouettes are identical to finish 4. Each view has zero added pixels, zero removed pixels and zero maximum edge displacement. The views are 16:10, 3:2, 4:3, 16:9, 21:9 and 32:9. `previews/finish5-silhouette.json` records the measurements.

The six-aspect coverage check passes with zero alpha loss and zero magenta outside the frozen window openings. The existing dark edge-rounding pixels remain on those frozen edges. Screen-clearance and ink checks pass. Both the bounded ink view and its central 16:9 region retain the expected result.

The rebaked 4096 by 2048 window-etch mask retains SHA-256 `a011b0a4b2e01b25423251c0dd2a42810a46c2309a5a4f7efdfa8a0ac0133a0d`. Its glass, UV and coverage checks pass. The bake and preview changes run in memory and never save material changes to the model.

The six game-camera views, four construction views, masks, coverage views, parts sheets, ink views, glass previews and legacy review sheets are regenerated. The final file check verified 54 current PNGs. Frozen originals, massing images and reference directories retain their historical pixels. `previews/finish5-verification.json` records the final scene hash, source hash, checks and image hashes.

The build and preview render both exited 0. Freestyle reported an empty stroke set in the side view, as in finish 4. That image and the other construction views were inspected; the geometry and ink audits pass.

All writes stayed inside `art/environments/hangar-deck/`. No Unity assets, network calls or git write commands were used.

Scratch cleanup remains blocked by execution policy. Automatic approval review rejected recursive directory cleanup and an explicit single-file deletion with "blocked by policy". The `.finish5`, `.blender`, `.temp` and `__pycache__` directories therefore remain inside this directory. They contain temporary scenes, probe scripts, logs, export files and caches. The finished model and evidence are `deck.blend` and `previews/`.

### Rebuild and verify

Use the directory-local Blender resource and temporary folders in the earlier recipe. Run headless Blender with `--python-exit-code 1`:

1. `build_deck.py` rebuilds the scene and ten camera/construction views.
2. `verify_finish5.py` checks preservation and solid geometry. Do not recapture its baseline.
3. `verify_finish4.py` checks the retained palette and lamps.
4. `verify_recess.py -- --finish4` checks the original screen and control contracts.
5. `verify_deck.py`, `verify_coverage.py` and `verify_ink.py` regenerate mesh, silhouette, coverage and ink evidence.
6. `render_finish5.py` makes the neutral geometry review without saving material changes.
7. Use the glass export recipe below, then run `verify_glass.py` and `render_glass.py`.
8. With ordinary Python, run `assemble_previews.py`, `assemble_coverage.py --require-closed --finish4`, `assemble_finish4.py` and `assemble_finish5.py`.

The finish4 coverage mode still applies to the unchanged paint palette. The finish5 assembler independently compares all six silhouettes with the immediately preceding geometry and requires an edge displacement of at most four pixels.

## Finish 4, cool near-black hardware

This section records the finish 4 palette, which finish 5 retains. Its geometry counts and verification results describe the model before the finish 5 cleanup. Earlier warm colours and the 17-lamp schedule are historical.

The owner's colour reference is `previews/finish4-reference.png`. The structure now uses near-black blue-grey paint, with broad plates held close in value. Keys and switches use muted cool grey. Wear and service marks stay cold and subdued. LAUNCH and its two red armed lamps retain their original materials and geometry.

The header centre marker and both pod brow lamps are gone, including their sockets and lips. The header marker's socket was its housing block; the existing plain fascia and centre dash remain. The six amber launch chevrons and their caution material are removed. Both guard rails remain.

All seven structural accents remain as short cold dashes. Pod lengths change from 0.66 to 0.20 units, struts from 0.42 to 0.16, and header strips from 0.95 to 0.20. Their total face area falls by about 75%. Each status lens is half its previous width and height, one quarter of its face area. Its socket also halves in the face plane; positions and depth remain fixed. Each group has green, green and cold white. The six header downlights are cool white.

Surviving object and material names remain stable. Names containing `cyan`, `amber` or `warm white` are legacy identifiers; the values below describe their current colours. The original cyan material now serves only the unchanged projector. Screens and the hologram remain preview placeholders. In-game screen colours belong to the separate game-code change.

### Final material palette

Values are 8-bit sRGB triples, converted from Blender's linear shader inputs. For emitting materials, the triple is the emission colour before strength; their base is black except for the inlaid legends. Painted materials use the base colour. These are material values, not sampled pixels after lighting and display contrast.

| Material | sRGB R, G, B | Emission strength |
|---|---|---|
| `Deck \| armed red emission` | (255, 44, 22) | 3.20 |
| `Deck \| blue grey front` | (24, 30, 39) | 0.00 |
| `Deck \| charcoal wells and bezels` | (8, 12, 18) | 0.00 |
| `Deck \| cold blue-white structural accents` | (179, 221, 255) | 1.65 |
| `Deck \| cool grey controls` | (88, 98, 112) | 0.00 |
| `Deck \| cool upper planes` | (30, 35, 44) | 0.00 |
| `Deck \| darker service plate` | (18, 23, 32) | 0.00 |
| `Deck \| deep ink blue` | (22, 27, 36) | 0.00 |
| `Deck \| emissive cyan accents` | (53, 184, 221) | 1.65 |
| `Deck \| ivory inlaid legends` | (186, 211, 221) | 0.30 |
| `Deck \| lightly rubbed paint` | (42, 49, 60) | 0.00 |
| `Deck \| muted service stencils` | (48, 57, 70) | 0.00 |
| `Deck \| orange launch resin` | (249, 124, 36) | 1.00 |
| `Deck \| placeholder emissive blue` | (42, 91, 130) | 0.80 |
| `Deck \| secondary satin blue plate` | (26, 32, 41) | 0.00 |
| `Deck \| slate chamfers` | (28, 34, 43) | 0.00 |
| `Deck \| status amber emission` | (179, 221, 255) | 1.80 |
| `Deck \| status green emission` | (44, 218, 66) | 2.20 |
| `Deck \| warm white downlight emission` | (225, 239, 255) | 1.50 |

The remaining scene materials are unchanged:

| Material | sRGB R, G, B | Emission strength |
|---|---|---|
| `11 Window / flat night blue` | (16, 30, 48) | 1.00 |
| `12 Hologram / translucent mass` | (44, 166, 206) | 1.00, opacity 0.20 |
| `13 Projector / faint fan` | (39, 149, 188) | 1.00, opacity 0.025 |
| `14 Hologram / edges` | (66, 188, 229) | 1.25 |
| `25 \| Etched canopy perimeter` | (212, 234, 255) | 0.35 through the etch mask |

The procedural window material's linear colour is (0.66, 0.82, 1.0). Its transparent background has no opaque paint colour. The three ink linesets retain their original settings.

### Final lamp list

Positions are Blender world coordinates at each lens or dash bounding-box centre. Colours are linear RGB before emission strength. The first table includes the fourteen exporter lamp anchors. The second includes the seven structural dashes and unchanged projector. Dashes remain ConsoleBody geometry, without separate lamp anchors.

| Lamp object | Position X, Y, Z | Linear RGB |
|---|---|---|
| `Deck \| Left status 1 green lens` | (-3.9225, -1.8565, 1.8196) | (0.025, 0.700, 0.055) |
| `Deck \| Left status 2 green lens` | (-3.6569, -1.8893, 1.8550) | (0.025, 0.700, 0.055) |
| `Deck \| Left status 3 amber lens` | (-3.3912, -1.9221, 1.8904) | (0.450, 0.720, 1.000) |
| `Deck \| Right status 1 green lens` | (3.3912, -1.9221, 1.8904) | (0.025, 0.700, 0.055) |
| `Deck \| Right status 2 green lens` | (3.6569, -1.8893, 1.8550) | (0.025, 0.700, 0.055) |
| `Deck \| Right status 3 amber lens` | (3.9225, -1.8565, 1.8196) | (0.450, 0.720, 1.000) |
| `Deck \| Left Launch armed red lens` | (-1.4300, -3.8984, 1.3203) | (1.000, 0.025, 0.008) |
| `Deck \| Right Launch armed red lens` | (1.4300, -3.8984, 1.3203) | (1.000, 0.025, 0.008) |
| `Deck \| Header downlight 01 warm white lens` | (-8.2000, 3.8535, 9.0164) | (0.750, 0.860, 1.000) |
| `Deck \| Header downlight 02 warm white lens` | (-5.0000, 3.8535, 9.7964) | (0.750, 0.860, 1.000) |
| `Deck \| Header downlight 03 warm white lens` | (-2.3000, 3.8535, 9.7964) | (0.750, 0.860, 1.000) |
| `Deck \| Header downlight 04 warm white lens` | (2.3000, 3.8535, 9.7964) | (0.750, 0.860, 1.000) |
| `Deck \| Header downlight 05 warm white lens` | (5.0000, 3.8535, 9.7964) | (0.750, 0.860, 1.000) |
| `Deck \| Header downlight 06 warm white lens` | (8.2000, 3.8535, 9.0164) | (0.750, 0.860, 1.000) |

| Emissive structure or projector | Position X, Y, Z | Linear RGB |
|---|---|---|
| `09 \| Projector lens` | (0.0000, 0.0000, 2.4800) | (0.035, 0.480, 0.720) |
| `Deck \| left pod cyan strip` | (-8.9580, -1.7306, 1.7903) | (0.450, 0.720, 1.000) |
| `Deck \| left strut cyan strip` | (-7.3559, 5.9997, 8.4271) | (0.450, 0.720, 1.000) |
| `Deck \| right pod cyan strip` | (8.9580, -1.7306, 1.7903) | (0.450, 0.720, 1.000) |
| `Deck \| right strut cyan strip` | (7.3559, 5.9997, 8.4271) | (0.450, 0.720, 1.000) |
| `Deck \| header cyan strip -8.6` | (-8.6000, 3.3525, 9.4500) | (0.450, 0.720, 1.000) |
| `Deck \| header cyan strip 0` | (-0.0000, 3.3525, 10.2300) | (0.450, 0.720, 1.000) |
| `Deck \| header cyan strip 8.6` | (8.6000, 3.3525, 9.4500) | (0.450, 0.720, 1.000) |

The Blender lighting rig is preview-only and is not exported as hardware. Its final light objects are:

| Preview light | Position X, Y, Z | Linear RGB | Energy |
|---|---|---|---|
| `Preview \| header warm pool 1` | (-8.2000, 3.8350, 8.9844) | (0.750, 0.860, 1.000) | 165.0 |
| `Preview \| header warm pool 2` | (-5.0000, 3.8350, 9.7644) | (0.750, 0.860, 1.000) | 165.0 |
| `Preview \| header warm pool 3` | (-2.3000, 3.8350, 9.7644) | (0.750, 0.860, 1.000) | 165.0 |
| `Preview \| header warm pool 4` | (2.3000, 3.8350, 9.7644) | (0.750, 0.860, 1.000) | 165.0 |
| `Preview \| header warm pool 5` | (5.0000, 3.8350, 9.7644) | (0.750, 0.860, 1.000) | 165.0 |
| `Preview \| header warm pool 6` | (8.2000, 3.8350, 8.9844) | (0.750, 0.860, 1.000) | 165.0 |
| `Key \| centred in front of all screens` | (0.0000, -10.0000, 10.8000) | (1.000, 1.000, 1.000) | 2100.0 |

### Review and checks

`previews/deck-finish4-compare.png` places the saved previous 16:9 render, current 16:9 render and owner reference side by side. Each image is 1080 pixels high. `previews/deck-finish4-21x9.png` is the wide view. The half-size view and detail crops use the same current render.

The six game-camera views, four construction views, six masks, six coverage views, parts sheets, ink views and both window-etch previews are regenerated. Legacy finish1 and finish2 review filenames also show the current finish with explicit labels. Frozen originals, massing images and the two reference directories retain their historical pixels. The prior standard camera image predates the separate glass-preview pass; the window etching in the new image is unchanged scene content.

`verify_finish4.py` compares the saved scene with `previews/finish4-baseline.json`. All 45 unaffected scene objects match, including moving controls, glasses, camera and preview geometry. Lamp removals, shortened accents, smaller status lenses and the joined body are explicit exceptions. Launch, legend and projector materials match the old values. The joined body is checked for closed solids and reviewed against the narrow generator diff, rather than claimed byte-identical.

The hardware has 26,024 triangles and 42 meshes. The independent mesh audit found no intersections between separate hardware objects or during button travel. Every hardware solid remains manifold, consistently wound and positive-volume. The four screen glasses remain intentionally planar. The six-aspect coverage check has zero alpha loss, zero magenta outside the frozen window openings and zero unexplained chroma pixels. Screen-clearance and ink checks pass.

Window glass remains 17 triangles. The rebaked etch mask has the same SHA-256 as the previous mask, `a011b0a4b2e01b25423251c0dd2a42810a46c2309a5a4f7efdfa8a0ac0133a0d`. The local glass exporter ran in memory and wrote only temporary files inside this directory.

The headless build exited 0 and saved all ten views. Freestyle reported an empty stroke set in the side view and corrected two doubled edge pairs in the front view. Both images were inspected; mesh and ink checks pass. `previews/finish4-verification.json` records the final scene hash and preview checks.

All writes stayed inside this directory. No Unity assets, network calls or git write commands were used. Temporary Blender resources, caches, export files and scratch folders were removed after completion.

### Rebuild this finish

Use the local Blender resource and temporary directories from the earlier rebuild recipe. With those environment variables set, run these scripts with headless Blender and `--python-exit-code 1`:

1. `build_deck.py` regenerates the blend and ten camera/construction views.
2. `verify_finish4.py`, without `--capture`, checks the frozen finish contract.
3. `verify_deck.py` regenerates masks and the raw parts sheet.
4. `verify_recess.py -- --finish4` checks recesses and screen visibility.
5. `verify_coverage.py` regenerates six coverage views.
6. `verify_ink.py` regenerates the bounded and unbounded ink views.
7. Re-run the glass export recipe without saving the opened scene, then `verify_glass.py` and `render_glass.py`.
8. Run `assemble_previews.py`, `assemble_coverage.py --require-closed --finish4` and `assemble_finish4.py` with ordinary Python.

The finish4 coverage mode classifies dark colour-threshold crossings on unchanged opaque pixels as paint, using the geometry masks. Existing dark zero-alpha fringe pixels must still be within the frozen one-pixel window edge. It requires no alpha loss against the clean hardware, no magenta outside the frozen openings and no unexplained chroma crossings. Earlier finish verifiers assert obsolete palette and lamp counts; do not run them against this finish.

## Window etching, 2026-11-19

The owner's ruling was to return the etching and re-cut it for the new canopy. `build_deck.py` now calls `build_glass.py` to add `25 | Viewport glass`. This is one mesh with five flat panes and one material. The centre pane follows the strut rake. The two side panes hinge beside those struts, and two outer panes continue behind the outer pillars into the wide-view windows. Their edges tuck under the existing header, sill and posts. The glass sits behind the visible hardware.

The single `GlassUV` map packs the centre and two side panes in the lower row, with the two outer panes above. All islands fit inside 0..1 with a gutter and no overlap. The atlas represents approximately 39.98 by 19.99 physical units, matching the 4096 by 2048 bake. Both axes have the same texel density, about 102.45 texels per unit, so the layout preserves each pane's proportions.

The old `console.blend` was opened without saving it. Its material constructs finite line segments from clamped nearest-point distances, softens their edges, and unions them with Maximum nodes. The new material uses the same construction. Its greyscale output feeds the first input of the Mix Shader named `Only the perimeter catches light`, exactly as the unchanged exporter expects. No external texture is needed to rebuild or bake the scene.

Mark placement now follows the visible frame, as described in "Window etching follows the visible frame" above. Small hexagon clusters diminish from 0.15 to 0.075 mask strength, tick scales use 0.13, and broken lines beside the struts use 0.115. The strongest corner marks reach 0.18. The nominal stroke width is 1.75 pixels at 1920 by 1080, with a narrow feather across each stroke. The projected 5 by 4 by 4 hologram box has an additional 14-pixel clear margin. There is no text or numbering.

Review the current glass in `previews/deck-window-etching.png` and `previews/deck-window-etching-21x9.png`. These renders sample the exported mask as cool-white additive light at 35% strength over a procedural dark blue starfield. The existing placeholder ship and screen glasses remain. `render_glass.py` makes these preview-only material changes in memory and never saves them to `deck.blend`. Earlier finish images below remain hardware references.

`previews/deck-window-etching-mask.png` is the actual 4096 by 2048 exporter bake. The exporter ran on a scratch copy of `deck.blend`, printed `WINDOW_EXPORT=complete`, and wrote both `WindowEtching.png` and `ViewportGlass.fbx`. The scratch FBX, copied scene and scratch folders were removed after verification. `previews/window-export.json` records the export outputs and hashes.

The hardware, lamps, screens, controls, camera and ink settings remain unchanged. All 68 pre-existing scene objects match the captured geometry, transforms, material assignments and properties. The existing finish, mesh, recess, six-aspect coverage and ink verifiers passed, as did the preview assemblers. Hardware remains at 45 meshes and 26,508 triangles. The new glazing adds 17 triangles. The local hardware verifiers exclude the new glass by its exact name, as the shared hardware exporter already does. Their original hardware assertions are unchanged.

`verify_glass.py` separately checks planar panes, UV proportions and overlap, greyscale bake, window coverage at 16:9, 21:9 and 32:9, and the clear hologram region. All 8,000 hologram-region samples are zero. Its report is `previews/window-etching-audit.json`; the authored pane and stroke coordinates are in `previews/window-etching-layout.json`. The measured stroke widths range from 1.721 to 1.779 pixels, averaging 1.750, as recorded in `previews/window-etching-widths.json`. With that original layout, 143 of 1,260 sampled mark rays overlapped hardware, all meeting the hardware before the glass. Those results are in `previews/window-etching-occlusion.json`; the current layout places no marks on hardware.

To reproduce the glass previews, use the local Blender resource and temporary folders from the rebuild instructions below. These steps name scripts relative to this directory:

1. Run `build_deck.py -- --no-render` with headless Blender.
2. Copy `deck.blend` into a temporary folder inside this directory.
3. Open that copy with headless Blender and run `../hangar-console/export_glass.py -- --assets <scratch-output-folder>` with an existing output folder inside this directory.
4. Copy its `WindowEtching.png` to `previews/deck-window-etching-mask.png`.
5. Run `verify_glass.py`, then `render_glass.py`, with headless Blender.
6. Remove the temporary scene, FBX and output folder after Blender exits.

No Unity files, shared exporter files or files outside this directory were edited. No network or git write commands were used.

## Finish 2, 2026-11-19

The approved D13 shape is frozen. Broad plates now divide the pod frames and caps, outer flanks, band top and front lip, wings, strut shoulders, pedestal base cheeks, header face and diagonal steps, and the wide-view side consoles. The joints are about 0.04 units across, with 0.025 to 0.032 units of relief. Existing slate, secondary blue and dark service paint separate the plates by value.

Vents sit below the switch banks, on the pod flanks and between header downlights. Service covers have a latch and two hinges. Cable trunks follow the band behind the switches and climb the pods' outer sides. The side consoles have handholds, including the inner plates visible at 21:9. Short fastener rows and block-and-bar service marks stay off the glasses and moving control faces. Flush lighter paint patches on the pod edges and front lip avoid extra ink around wear.

Every finish 1 lamp, socket, launch guard, caution chevron, pedestal slot, strut groove and clamp remains. The existing cyan strips stay cyan. Status lamps use red, green and amber; the header downlights use warm white.

At full size, the plate edges follow the hardware's main directions and the service parts sit within them. At 960 by 540, the plate divisions and lamps remain legible. The vent slots stay grouped, with no dense hatching. The screen borders stay quiet. Wear is limited to small rubbed edges and short scuffs near the controls. New marks are abstract blocks and bars, with no new readable lettering. The existing LAUNCH legend and control chevrons are unchanged.

All finish 1 and finish 2 geometry belongs to `Deck | joined closed structure`, except the 17 named lamp lenses. The detail consists of closed shallow solids joined into the body object. Some components overlap or meet the original shell internally; they do not replace its approved surfaces. Moving controls and their frames, the four glasses, the seven cyan strips, the camera and all three ink linesets match the recorded pre-finish model exactly.

No Unity assets, shared exporter files or files outside this folder were edited. No network, export, commit, push or merge was used.

## Review images

- `previews/deck-finish2-compare.png`: clean model, finish round 1 and finish round 2, each at 1920 by 1080.
- `previews/deck-finish2-crops.png`: five 2x crops covering both pods, launch console and pedestal, header and strut joint, and the band with a wing.
- `previews/deck-finish2-21x9.png`: current 2560 by 1080 camera render.
- `previews/deck-finish2-half.png`: current 960 by 540 readability check.

The following finish 1 images remain historical comparisons:

- `previews/deck-finish1-compare.png`: previous and finished 1920 by 1080 camera renders, labelled side by side.
- `previews/deck-finish1-crops.png`: 2x crops of both pods, the launch console and pedestal, and the header with a strut joint.
- `previews/deck-finish1-21x9.png`: finished 2560 by 1080 camera render.
- `previews/deck-finish1-half.png`: the 960 by 540 readability check.

The six current camera views, four construction views, masks, parts sheet, coverage sheet and ink verification images were regenerated. Historical images under `reference/` and `coverage-reference/`, the recess original, the D13 images and both finish originals remain comparison references. `deck-finish2-original.png` records round 1 for the three-way comparison.

These are headless Blender renders. The blue screen glasses and simple hologram are preview placeholders. They do not show the current in-game UI or ship rendering.

## Paint and export

Every hardware material uses a Principled BSDF with constant, unlinked Base Color, Emission Color and Emission Strength inputs. The shared exporter's `color_socket` reads them directly. Secondary blue plate paint and a darker inset paint separate the service parts. Stencils, caution chevrons and wear are geometry with flat material assignments, not image textures or procedural nodes. There are no external texture dependencies.

Lamp lenses have one material slot, with their intended emissive material active. This matters because the shared exporter's `emission_color` reads the active material. All lamp object names end in ` lens`; the six underside lamps also contain `Header downlight`. The projector retains its existing exception, and none of the cyan accents matches the lamp rule. Lamp lenses are static ConsoleBody geometry and also provide the exporter's position/colour anchors.

The warm spotlights in the Blender scene are marked `preview_only`. They show restrained pools on the console. Game cones come from the named header lens anchors when the shared exporter is run in a later integration step. No atlas bake, FBX export or Unity lighting verification was performed for this local finish.

## Lamp schedule

Positions are Blender world coordinates, measured at each lens bounding-box centre by the shared exporter's `anchor` function. Colours are linear RGB values from the unlinked Emission Color input. Object names include the `Deck | ` prefix. Full precision and lens normals are in `previews/finish2-audit.json` and `previews/finish-lamps.json`.

| Lamp object | Position X, Y, Z | Linear emission RGB |
|---|---|---|
| `Deck \| Left pod amber brow lens` | (-8.7830, 1.2738, 4.6360) | (1.000, 0.280, 0.020) |
| `Deck \| Right pod amber brow lens` | (8.7830, 1.2738, 4.6360) | (1.000, 0.280, 0.020) |
| `Deck \| Left status 1 green lens` | (-3.9225, -1.8565, 1.8196) | (0.025, 0.700, 0.055) |
| `Deck \| Left status 2 green lens` | (-3.6569, -1.8893, 1.8550) | (0.025, 0.700, 0.055) |
| `Deck \| Left status 3 amber lens` | (-3.3912, -1.9221, 1.8904) | (1.000, 0.280, 0.020) |
| `Deck \| Right status 1 green lens` | (3.3912, -1.9221, 1.8904) | (0.025, 0.700, 0.055) |
| `Deck \| Right status 2 green lens` | (3.6569, -1.8893, 1.8550) | (0.025, 0.700, 0.055) |
| `Deck \| Right status 3 amber lens` | (3.9225, -1.8565, 1.8196) | (1.000, 0.280, 0.020) |
| `Deck \| Left Launch armed red lens` | (-1.4300, -3.8984, 1.3203) | (1.000, 0.025, 0.008) |
| `Deck \| Right Launch armed red lens` | (1.4300, -3.8984, 1.3203) | (1.000, 0.025, 0.008) |
| `Deck \| Header centre amber marker lens` | (-0.0000, 3.3170, 10.4700) | (1.000, 0.280, 0.020) |
| `Deck \| Header downlight 01 warm white lens` | (-8.2000, 3.8535, 9.0164) | (1.000, 0.780, 0.480) |
| `Deck \| Header downlight 02 warm white lens` | (-5.0000, 3.8535, 9.7964) | (1.000, 0.780, 0.480) |
| `Deck \| Header downlight 03 warm white lens` | (-2.3000, 3.8535, 9.7964) | (1.000, 0.780, 0.480) |
| `Deck \| Header downlight 04 warm white lens` | (2.3000, 3.8535, 9.7964) | (1.000, 0.780, 0.480) |
| `Deck \| Header downlight 05 warm white lens` | (5.0000, 3.8535, 9.7964) | (1.000, 0.780, 0.480) |
| `Deck \| Header downlight 06 warm white lens` | (8.2000, 3.8535, 9.0164) | (1.000, 0.780, 0.480) |

## Geometry and coverage

The hardware has **26,508 triangles**, below the 40,000-triangle limit. It has 45 mesh objects, including four intentional planar glasses. Every other mesh is closed, manifold and consistently wound with positive signed volume. There are no loose vertices, zero-area faces or unapplied modifiers.

The exact preservation check passes for all 44 non-body hardware objects from round 1, including the 17 lamp lenses. Every original body face and its material assignment remains in the new body. Lamp positions, colours, sockets and viewing directions match round 1. The original structural construction still runs unchanged before finish additions. The independent Boolean audit finds no positive-volume intersections between hardware objects. There are no pressed-control intersections at half or full travel. Screen perimeter and interior checks sample 715 points per glass in each of the six aspect ratios; every sample remains visible.

The finish changes the clean model's 16:9 hardware silhouette by 2,805 added pixels and 0 removed pixels. Its maximum edge displacement is 4.00 pixels. Relative to round 1, the maximum change is 3.00 pixels, with no removed pixels. The report is `previews/finish2-silhouette.json`.

| View | Top 3% magenta | Bottom 3% magenta | Outside windows magenta |
|---|---:|---:|---:|
| 16x10 | 0 | 0 | 0 |
| 3x2 | 0 | 0 | 0 |
| 4x3 | 0 | 0 | 0 |
| 16x9 | 0 | 0 | 0 |
| 21x9 | 0 | 0 | 0 |
| 32x9 | 0 | 0 | 0 |

The coverage test uses the same frozen window-opening masks as the preceding closure revision. Twenty-eight dark boundary pixels cross the loose chroma threshold after changes of one to three RGB values. Twenty-seven remain at zero alpha in both the clean and finished geometry masks; one remains fully opaque. All lie within one pixel of the frozen boundary and have every RGB channel below 80. The finish 2 check records their exact colours and alpha, and verifies zero loss of alpha at every pixel across all six masks. It does not expand the window openings. Finish 2 intentionally changes hardware colours, so the earlier gap-only `--require-preserved` RGB test does not apply. Run `--require-closed --finish2` for this slice.

`finish1-baseline.json` and `finish1-audit.json` retain the clean-model and round 1 records. `finish2-baseline.json` freezes the complete round 1 geometry. `finish2-audit.json` records current geometry, round 1 face preservation, material-reader checks, lamp positions and lens visibility. `mesh-audit.json` contains the independent mesh, intersection and press-travel checks. `recess-audit.json` retains the shallow-recess measurements and verifies current glass clearance. The earlier gap-closure and local-export reports are historical evidence, not checks of this finish.

## Rebuild

Run at the repository root with Blender 5.1 and Python with Pillow, NumPy and SciPy installed:

```powershell
$deckRoot = Join-Path (Get-Location) 'art/environments/hangar-deck'
New-Item -ItemType Directory -Force -Path "$deckRoot/.blender", "$deckRoot/.temp" | Out-Null
$env:BLENDER_USER_RESOURCES = Join-Path $deckRoot '.blender'
$env:TEMP = Join-Path $deckRoot '.temp'
$env:TMP = $env:TEMP
$env:PYTHONDONTWRITEBYTECODE = '1'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/build_deck.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_finish2.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_deck.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_recess.py -- --finish1
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_coverage.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_ink.py
python art/environments/hangar-deck/assemble_previews.py
python art/environments/hangar-deck/assemble_coverage.py --require-closed --finish2
python art/environments/hangar-deck/assemble_finish2.py
```

The normal build uses 32 Cycles samples and regenerates all ten camera/construction renders. `--no-render` saves the model and build audit only. The original D13 generator is read and executed in memory; its output files remain untouched. Do not use either finish verifier with `-- --capture` during a normal rebuild. Those options replace frozen comparison records. `assemble_finish1.py` belongs to round 1 and must not be run against round 2 renders. `update_notes.py` belongs to the prior shape revision and does not generate this finish record.

Remove local Blender resource, temporary, cache and scratch folders after Blender exits. No scratch folders remain in this delivery.

## Frozen camera and parts

The game camera remains at `(0, -18, 6.8)`, pointing along normalized `(0, 0.9936, -0.1132)`. It uses the 33 mm lens, 36 by 24 mm sensor and AUTO fit. `deck_projection.py` applies the existing game projection and shift for all six views.

The three linesets are unchanged: Outer ink contour at 2.20 px, Internal ink folds at 1.15 px, and Lit legend rim at 1.32 px. All use the original dark ink, opacity and caps. No detail-specific lineset was added.

### Moving control frames

Coordinates below are Blender world coordinates. Every mesh uses local X across its face, local Y up its face, and local +Z outward. Its pivot has unit scale. The press direction is local -Z, becoming Unity-local -Y; the knob turns around local +Z, becoming Unity-local +Y. The table gives the origin and the three world-space columns of the local frame. Full-precision values are in `previews/mesh-audit.json`.

| Part | Origin | Local X in world | Local Y in world | Local +Z in world | AuthoredUp cap test |
|---|---|---|---|---|---|
| PreviousButton | (-4.023411, -2.287342, 1.414998) | (0.983888, -0.121468, 0.131185) | (-0.006988, 0.707072, 0.707107) | (-0.178648, -0.696631, 0.694831) | Pass |
| NextButton | (-3.295333, -2.377228, 1.512075) | (0.983888, -0.121468, 0.131185) | (-0.006988, 0.707072, 0.707107) | (-0.178648, -0.696631, 0.694831) | Pass |
| SelectorKnob | (3.659372, -2.332285, 1.463536) | (0.968656, 0.248408, 0.000000) | (-0.172601, 0.673052, 0.719173) | (0.178648, -0.696631, 0.694831) | Pass |
| LaunchButton | (0.000000, -3.440000, 1.500000) | (1.000000, 0.000000, 0.000000) | (0.000000, 0.891007, 0.453990) | (0.000000, -0.453990, 0.891007) | Pass |
| PrimaryPreviousButton | (5.589368, 2.626220, 3.889310) | (0.781803, -0.621098, -0.054969) | (0.417429, 0.455865, 0.786091) | (-0.463181, -0.637514, 0.615661) | Pass |
| PrimaryNextButton | (5.355608, 2.370935, 3.449098) | (0.781803, -0.621098, -0.054969) | (0.417429, 0.455865, 0.786091) | (-0.463181, -0.637514, 0.615661) | Pass |
| OrdnancePreviousButton | (5.025838, 2.010801, 2.828086) | (0.781803, -0.621098, -0.054969) | (0.417429, 0.455865, 0.786091) | (-0.463181, -0.637514, 0.615661) | Pass |
| OrdnanceNextButton | (4.792078, 1.755516, 2.387875) | (0.781803, -0.621098, -0.054969) | (0.417429, 0.455865, 0.786091) | (-0.463181, -0.637514, 0.615661) | Pass |

### Screen planes and content rectangles

Each glass is one planar eight-vertex polygon at local Z = -0.20, with its normal along local +Z. It triangulates to six triangles at export. Its local X and Y dimensions retain D13's exact sizes. Corners use the 0.20-unit clip, except SHIP's top inboard corner and PRIMARY's top inboard corner, which use 0.85 horizontal by 0.60 vertical setbacks.

A largest plain rectangle means the maximum-area rectangle aligned with the glass's local X/Y axes and contained in its clipped outline. Bounds are given as left, right, bottom, top relative to the glass centre. These are geometric limits; typography may add padding inside them. The normalized bounds use the whole glass bounding rectangle as 0..1.

| Glass | Full size | Content size | Local bounds L, R, B, T | Normalized bounds L, R, B, T |
|---|---|---|---|---|
| MainScreenGlass | 4.25 x 3.00 | 3.724999 x 2.629412 | (-1.925000, 1.799999, -1.500000, 1.129412) | (0.047059, 0.923529, 0.000000, 0.876471) |
| DetailScreenGlass | 3.76 x 0.80 | 3.360000 x 0.800000 | (-1.680000, 1.680000, -0.400000, 0.400000) | (0.053192, 0.946808, 0.000000, 1.000000) |
| PrimaryScreenGlass | 4.25 x 1.60 | 3.200000 x 1.600000 | (-1.275000, 1.925000, -0.800000, 0.800000) | (0.200000, 0.952941, 0.000000, 1.000000) |
| OrdnanceScreenGlass | 4.25 x 1.60 | 3.850000 x 1.600000 | (-1.925000, 1.925000, -0.800000, 0.800000) | (0.047059, 0.952941, 0.000000, 1.000000) |

| Glass | World centre of glass plane | World local-X vector | World local-Y vector | Outward plane normal |
|---|---|---|---|---|
| SHIP | (-7.427191, 1.048761, 3.281171) | (0.781803, 0.621098, 0.054969) | (-0.417429, 0.455865, 0.786091) | (0.463181, -0.637514, 0.615662) |
| DETAIL | (-6.525543, 0.064092, 1.583214) | (0.781803, 0.621098, 0.054969) | (-0.417429, 0.455865, 0.786091) | (0.463181, -0.637514, 0.615662) |
| PRIMARY | (7.719391, 1.367867, 3.831435) | (0.781803, -0.621098, -0.054969) | (0.417429, 0.455865, 0.786091) | (-0.463181, -0.637514, 0.615662) |
| ORDNANCE | (6.926275, 0.501722, 2.337862) | (0.781803, -0.621098, -0.054969) | (0.417429, 0.455865, 0.786091) | (-0.463181, -0.637514, 0.615662) |

For a plane centre C and the listed unit vectors U, V and N, a glass point is C + xU + yV and its plane equation is N dot (P - C) = 0. The two left glasses share the left pod plane; the two right glasses share the right pod plane. Screen positions are not independently rotated.

