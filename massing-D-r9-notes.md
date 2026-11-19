# Cockpit D — round 9

Built directly from `round8/build_D8.py`, using local headless Blender 5.1.
The three variants share the same cockpit, fixed camera, light, materials and
hologram. Only the header mesh and its three blue inlays change between variants.

## Pod movement and the fixed-camera limit

Each pod and its folded control ledge moved **0.22 units outward in X**, from
X = ±5.55 to **±5.77**, keeping Y = 0.25 and Z = 2.45. The origin moves
**20.79 camera pixels per side** (1.083% of the 1920 px width).
Individual shell vertices move 17.45–23.44 px because their depths differ.
The inner opening is roughly 40 px wider. The wing meshes still sweep from the
translated ledges into the original centre boundary.

**This is deliberately smaller than the requested 6–8% per side.** The R8 shell
already comes within 37.41 px of either image edge. A 115.2–153.6 px lateral move
at the same depth would clip it; the finished version preserves the fixed camera,
pod depth, size and angles and leaves **16.67 px of outer margin**.
A 116 px origin move with about 25 px of margin would require roughly 3.76 units
outward plus 7 units rearward, substantially shrinking the pods on screen.
The limited translation is an explicitly documented framing compromise, not a
claim that the 6–8% target was met.

Both strut feet followed the pods: X = ±2.31303 became **±2.54571** (+0.23268
units outward), while their Y/Z remain 2.65/1.20. Their upper ends are solved
along the same projected inner-pod lines. Width/depth remain 0.38/0.30.
The silhouette gap is 32.79–33.36 px at camera rows 570, 620, 680 and 740;
rendered sky counts are in `D9-render-checks.json`. No upper side closures were added.

## Selector clearance

The knob centre moved from **(2.70, -2.45, 1.13)** to
**(2.15, -1.20, 1.285)**: 0.55 units inboard, 1.25 units rearward, and 0.155 up.
The socket is **26.46 px horizontally clear** of the nearest
ledge switch silhouette. The knob and switch dimensions are unchanged. A small
faceted lower collar fits the socket to the existing sloping wing so it does not
float. Both previous/next pairs remain beside their respective weapon screens.

## Three brows

- **A:** Chevron rising to a shallow centre peak; each strut meets the slight shoulder kink between the inner and outer slopes.
- **B:** Level centre span with outer facets falling toward the sides; both struts meet the two main kinks.
- **C:** Raised flat centre shelf, short diagonal step-downs and level outer shoulders with clipped ends; each strut meets the lower corner of a step.

The common visible junction is at X = ±5.84686,
Y = 3.90, Z = 9.02. The seam continues through the header depth in the plane of
each strut, so the physical strut lands on a facet change and the camera sees
the same meeting point. The upper datum remains Z = 10.62; the header is 24.8
units wide. The undersides are explicit sloping facets. All inlays follow the
local lower-edge slope; A's centre inlay bends over the peak. The strut inlays
were lowered together to remain visible below all three brows.

A uses a raised centre peak, not a centre dip. Its lower-edge centre/end heights
are 9.95/8.20. B holds the centre at 9.02 and falls to 7.95 at the ends. C raises
its central shelf to 9.80, steps down across 1.05-unit diagonals, then clips the
outer ends from the shoulders to 8.40.

My preference is **B**: the two clear canopy kinks directly receive the struts,
and the level centre leaves a calm frame above the hologram. A is the lighter,
more sweeping alternative; C makes the faceted construction most explicit.

## Verification

| Screen | True size | Width retained | Height retained | Camera rays clear | Key-light rays clear |
|---|---:|---:|---:|---:|---:|
| SHIP | 4.25 x 3.00 | 94.18% | 90.68% | 1309/1309 | 1899/1899 |
| DETAIL | 3.76 x 0.80 | 93.66% | 94.48% | 1301/1301 | 1881/1881 |
| PRIMARY | 4.25 x 1.60 | 94.36% | 89.33% | 1279/1279 | 1827/1827 |
| ORDNANCE | 4.25 x 1.60 | 93.88% | 92.87% | 1329/1329 | 1917/1917 |

Retention measures foreshortening, excluding perspective magnification. Dense
camera rays and nine-point area-light samples cover the clipped screen polygons.
All three variants pass. The 32 locked R8 constants compare unchanged, including
screen sizes, pod angles/height, camera, launch housing, pedestal and hologram
volume. The fighter geometry and transform also compare unchanged. The pedestal
steps remain visible, the lens stays below the clear-volume floor, and the struts
remain behind the clear box. The header stays above it. No textures, text objects
or modifiers were added. There is one key light.

`D9A/B/C-measurements.json` contain the detailed checks. `check_scene.py` compares
saved non-header geometry across all three blend files; the shared hashes are
recorded in `D9-variant-checks.json`.

## Files and rebuild

`D9-sheet.png` is the labelled R8/A/B/C 2 x 2 comparison at 2368 x 1440.
All individual renders are 1920 x 1080. The top and front views show variant A;
all non-header geometry is shared. Each blend opens on the fixed game camera.

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D9.py -- A
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D9.py -- B
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D9.py -- C
python out/assemble_D9.py
```

Variant A also writes the three-quarter, top and front views. `--check` validates
without rendering; `--preview` renders only the camera at reduced samples.
Main dimensions and angles are named constants at the top of `build_D9.py`.
