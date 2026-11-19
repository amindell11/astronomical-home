# Hangar deck

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

