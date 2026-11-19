# Cockpit D — round 10

Built from `round9/build_D9.py`, retaining header C. The build has one design,
with no A/B variants or variant argument. All scene geometry is native Blender
mesh/curve geometry, rendered headlessly with local Blender 5.1.2. No network
resources, textures, text objects or modifiers were used.

## Movement and composition

Each pod, its complete folded ledge, control well and ledge keys moved **1.22
units outward in X** from round 9 C: **X = ±5.77 → ±6.99**. Y = 0.25 and
Z = 2.45 are unchanged. At the fixed 1920 × 1080 camera, the pod origin moves
**115.30 pixels per side**, or **6.005% of frame width**. Corresponding housing
vertices move 96.78–130.00 pixels because their camera depths differ.

The move is deliberately at the lower end of the requested range to preserve
the 40-pixel screen margin. Pod size, depth, height, yaw, lean, bank and chamfers
are unchanged. Screens remain in their original positions on the pod faces;
none was shrunk, rotated independently or moved rearward.

The slim struts follow the pods, retaining their projected alignment and sky
gap. Their feet move from X = ±2.54571 to **±3.83601**, retaining Y = 2.65,
Z = 1.20. The visible sky gap between each pod housing and its strut is
**32.67–33.24 pixels** at image rows 570, 620, 680 and 740. The C brow keeps its
raised flat centre, short diagonal steps, level shoulders and clipped ends;
the junctions follow the struts, extending the flat centre span. All brow
height datums and step dimensions are unchanged.

Both wings share the translated ledge perimeter and the original centre
boundary at X = ±1.55. The outer wing boundary moves outward by 1.22 units;
the inner wing facets widen to close the span. The centre housing, launch,
three-step pedestal and their forward fall remain unchanged. Open sides stay
open, and the pods remain in front of the window.

## Clear centre, before and after

These camera measurements use the same horizontal scanline, **y = 680 px**,
in both rounds. They measure between facing silhouettes, including housing
depth. The strut interval describes the central canopy opening; the hologram
is intentionally inside it.

| Measure | Round 9 C | Round 10 | Increase |
|---|---:|---:|---:|
| Between pod housing silhouettes | 628.84 px | 837.18 px | 208.33 px |
| Between inner strut silhouettes | 494.93 px | 703.01 px | 208.09 px |
| Minimum separation of housing X bounds | 6.16579 units | 8.60579 units | 2.44 units |

The designated hologram clear volume remains 5 × 4 × 4 units, centred at
(0, 0, 4.5). The struts remain behind its rear boundary, the header above it,
and the projector lens top at Z = 2.4925 remains below its Z = 2.5 floor.

## Screen margins and aspect checks

Distances below are from the actual chamfered screen polygon to its nearest
frame edge. Positive values are clear pixels; negative values are the maximum
extent beyond the frame. All listed nearest edges are the outboard left/right
edge for these two formats.

| Screen | True size | 16:9, 1920 × 1080 | 16:10, 1920 × 1200 |
|---|---|---:|---:|
| SHIP | 4.25 × 3.00 | **45.39 px left** | **−56.23 px left** |
| DETAIL | 3.76 × 0.80 | 148.36 px left | 58.18 px left |
| PRIMARY | 4.25 × 1.60 | **45.39 px right** | **−56.23 px right** |
| ORDNANCE | 4.25 × 1.60 | 88.60 px right | **−8.22 px right** |

**The main 16:9 composition passes the 40-pixel hard limit.** The narrower
16:10 view clips SHIP, PRIMARY and a small part of ORDNANCE. This is the
requested crop demonstration of the same scene, with no aspect-specific
redesign, camera movement or FOV change. All ship keys, switches and weapon
step keys remain wholly in frame at 16:10, despite the screen crop.

At the requested 2560 × 1080 wide check, all screens and controls remain
inside the frame. The outboard screen margins are 365.39 px for SHIP and
PRIMARY, 468.36 px for DETAIL and 408.60 px for ORDNANCE. The vertical framing
matches 16:9. All three camera renders use a **34.11° vertical FOV**, position
(0, −18, 6.8), and direction (0, 0.9936, −0.1132).

## Controls and cropping

Both weapon previous/next pairs are now on the **inboard frame of the right
pod**, beside their respective screens. Their local U coordinate is −2.65.
PRIMARY uses V = 1.63 / 1.07, keeping both keys below the upper inner chamfer;
ORDNANCE uses V = 0.28 / −0.28. Each key retains its 0.43 × 0.40 face and
0.10 depth. The projected key envelopes are roughly 44–46 px wide and
37–40 px tall. No screen overlaps a key, and every complete key face is
visible. The ship keys and four switches translate with their ledges.

The selector knob remains at the round 9 position **(2.15, −1.20, 1.285)**.
Its socket is now **144.90 pixels horizontally clear** of the nearest ledge
switch. Its fitted lower collar follows the widened wing beneath it.

Cropping is mirrored on the two sides:

| Part | Main 16:9 crop | 16:10 crop | Wide check |
|---|---|---|---|
| Deep housing / outer shoulder | Outboard envelope extends **98.31 px** past each side; upper outer shell and shoulder are cut | **215.91 px** past each side | Housing fully in frame, **221.69 px** side margin |
| Cyan outer accent | Fully visible, **6.51 px** side margin | Entire strip outside the side edge | Fully visible |
| Folded ledge | **30.54 px** side margin; outer front tip extends **10.35 px below** the bottom edge | Outer end extends **72.73 px** past the side; front tip **11.50 px below** bottom | Side margin **350.54 px**; same **10.35 px** bottom crop as 16:9 |

Cropping an accent or a ledge side was permitted, not required. The main
view retains both cyan strips while clearly cutting through the housings.
At 16:9 the nearest full-control margin is **54.70 px** for the outboard
switch; the ship key pair has at least **66.46 px**. All four weapon step
keys have at least **334.23 px** to their nearest frame edge.

## Verification

| Screen | Width retained | Height retained | Camera rays clear | Key-light rays clear |
|---|---:|---:|---:|---:|
| SHIP | 95.90% | 89.89% | 1309/1309 | 1899/1899 |
| DETAIL | 95.53% | 93.77% | 1301/1301 | 1881/1881 |
| PRIMARY | 96.03% | 88.53% | 1279/1279 | 1827/1827 |
| ORDNANCE | 95.69% | 92.11% | 1329/1329 | 1917/1917 |

Retention measures foreshortening independently of perspective magnification.
Dense camera sampling covers each screen including near its chamfered edges;
key-light sampling uses the light centre and eight points around its disk.
All screens exceed the required 80% width / 75% height retention. All ten
ship/weapon key faces pass 360 visibility samples each, and every vertex of
their complete geometry stays in the main frame. The saved scene has one key
light, zero modifiers, mirrored pod shells and continuous, noncrossing wing
triangles. The hologram geometry and transform compare unchanged against
round 9; locked dimensions, camera settings and header C constants also pass.

`D10-measurements.json` contains all projected bounds, margins, ray counts,
control bounds, alignment angles and before/after measurements. Main and
aspect renders, plus all four inspection views, were visually reviewed.

## Files and rebuilding

- `D10-camera.png`: fixed camera, 1920 × 1080.
- `D10-camera-16x10.png`: same scene, 1920 × 1200.
- `D10-camera-21x9.png`: same scene, 2560 × 1080.
- `D10-compare.png`: picked sketch, round 9 C and round 10, with labels.
- `D10-three-quarter.png`, `D10-top.png`, `D10-front.png`, `D10-side.png`:
  orthographic inspection views at 1920 × 1080.
- `D10.blend`: fixed game camera active for rendering at 1920 × 1080.
- `build_D10.py`: scene, named design constants, verification and renders.
- `assemble_D10.py`: comparison-sheet assembly using local Pillow.

From the workspace root:

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D10.py
python out/assemble_D10.py
```

`-- --check` validates without rendering. `-- --preview` writes the main
camera and blend at reduced samples. The full build uses 32 Cycles samples.
The source in `round9/` and both supplied references remain untouched.
