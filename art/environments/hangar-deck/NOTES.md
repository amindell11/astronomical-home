# Hangar deck

The approved D13 layout defines this model. The 2026-11-19 revision widens the upper inner pod corners and replaces raised screen lips with shallow sloped recesses. It preserves the control frames, exporter names, flat dark blue-grey paint and the old console's Freestyle ink.

All work was local. No file outside `art/environments/hangar-deck/` was edited. No git write, network request or Unity export was run. The shared exporter's `projected_ink` function already bounds ink to the reference image.

All six aspects have zero magenta in the top and bottom 3% bands or outside the window openings. The 16:9 hardware mask gains 32,795 pixels, removes none and adds none inside the windows.

The exact rendered-image requirement still fails. The 16:9 PNG changes 55,172 pixels, including 23,472 outside the permitted top and bottom backdrop regions. The model closes the gaps, but this render does not preserve every other approved pixel. The comparison and its locations are reported below; `--require-preserved` fails on this remaining difference.

## Rebuild and evidence

Run at the repository root with Blender 5.1:

```powershell
$deckRoot = Join-Path (Get-Location) 'art/environments/hangar-deck'
New-Item -ItemType Directory -Force -Path "$deckRoot/.blender", "$deckRoot/.temp" | Out-Null
$env:BLENDER_USER_RESOURCES = Join-Path $deckRoot '.blender'
$env:TEMP = Join-Path $deckRoot '.temp'
$env:TMP = $env:TEMP
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/build_deck.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_deck.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_recess.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_coverage.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_ink.py
python art/environments/hangar-deck/assemble_previews.py
python art/environments/hangar-deck/assemble_coverage.py --require-closed --require-preserved
python art/environments/hangar-deck/update_notes.py
```

`build_deck.py` reads the checked-in D13 generator for its dimensions. It executes the scene construction in memory, then replaces the massing surfaces. It does not load an existing `deck.blend`. The original D13 generator, model and camera reference remain unchanged.

`verify_recess.py` compares against `previews/recess-baseline.json`, measures the housing margins and checks the finished recess walls and camera rays. Remove the scratch resource and temporary folders after Blender exits. This revision was rendered headlessly with Blender 5.1.2 at 32 samples.

To rerun `verify_gap_geometry.py`, supply the pre-edit Blender file with `-- --reference PATH`. Final scratch cleanup removes this session's temporary snapshot; `previews/gap-geometry.json` retains its SHA256 and the completed audit.

`verify_deck.py -- --massing` regenerates the reference hardware mask. The comparison excludes the hologram, its light fan and the backdrop. `assemble_previews.py` uses Pillow, NumPy and SciPy for labels, the exact 50% camera overlay, the silhouette measurements and the maximum-area content rectangles.

## Camera, paint and ink

The game camera is at `(0, -18, 6.8)` and points along normalized `(0, 0.9936, -0.1132)`. Its lens is 33 mm, its sensor is 36 by 24 mm and sensor fit is AUTO, matching `console.blend`. At 1920 by 1080 this gives a vertical field of view of 34.113931 degrees. D13 used VERTICAL fit with an exact 34.11 degrees. The tiny projection difference follows the explicit instruction to match the old console's sensor and lens.

The palette uses Principled BSDF nodes with unlinked Base Color, Emission Color and Emission Strength values. `export_console.py:255-278` can read them directly. The four structure values are deep ink blue, blue-grey front, slate chamfers and cool upper planes. Wells and bezels use charcoal; keys and switches use cool grey; LAUNCH uses orange resin; accent strips use emissive cyan; screens use placeholder emissive blue. Legends have a pale inlay value.

The old scene was opened read-only to inspect the camera and linesets. `contract-inspection.json` records that inspection. The ink settings are:

| Lineset | Selection | RGB | Thickness | Position |
|---|---|---|---:|---|
| Outer ink contour | Visible silhouettes in hardware ink collection | 0.009, 0.004, 0.002 | 2.20 | CENTER |
| Internal ink folds | Visible creases in hardware ink collection | 0.009, 0.004, 0.002 | 1.15 | CENTER |
| Lit legend rim | Visible silhouettes in legend ink collection | 0.009, 0.004, 0.002 | 1.32 | OUTSIDE |

Each lineset has opacity 1 and ROUND caps. Scene line thickness is 1 with ABSOLUTE mode. The exporter can find `Lit legend rim` by name and its collection is assigned. No bevel or subdivision modifiers remain in the deliverable.

## Exporter changes required later

These changes are documented only. Line numbers refer to `art/environments/hangar-console/export_console.py` inspected for this slice.

1. Lines 66-70: keep the existing ` lens` name rule and the projector exception, but allow explicitly lamp-free source scenes to emit empty `lamps` and `downlights` arrays. This slice intentionally has neither. Do not treat the cyan strips as lamps.
2. Line 79: map `SelectorKnob` to only `08 | Selector faceted grip`. This slice has one knob object, without the old separate `08 | Selector pointer`. All other named part maps at lines 76-104 remain valid.
3. Lines 114-119: replace the hard requirement for image-texture nodes on the old left-screen materials. The new glasses use the placeholder blue material and contain no reference pictures. Either author new reference images for the new layouts or retire that reference-image dependency when the builder reads authored screen dimensions.
4. Lines 208-216: apply the local-XY display mapping to all four glasses, and reuse or replace an existing `DisplayUV` layer instead of adding duplicate layers. Each glass now has an explicit plane-local frame. Map the whole bounding rectangle to 0..1; keep UI content inside the content rectangle listed below and leave all surrounding pixels as backing colour. This preserves the current UV-based input mapping without stretching content into the cut corners.
5. Lines 467-470: export each glass's width and height plus its normalized content-rectangle bounds. Existing width and height calculations remain correct because all four glass meshes use local XY coordinates. The content rectangle is needed by the display layout builder, not by the FBX transform.
6. Lines 458-459 and 475: stamp the actual `deck.blend` source and verified provenance, and describe placeholder screens rather than the old static artwork.

The manifest stays beside the shared exporter. This task does not run the exporter or write its manifest because its output directory is outside the permitted folder.

The hardware filter at lines 34-37, the rest of the part maps, pivot construction at lines 108-110 and 425-432, color graph reader, carried-control ink pass and FBX coordinate conversion need no new shape-specific rules.

## Builder and display changes required later

Line numbers refer to `src/Asteroids3D/Assets/Scripts/Editor/UI/Hangar/Hardware/HangarConsoleBuilder.cs`.

1. Lines 29-30: keep the manifest path beside the shared exporter when integration switches to this source. The Unity visuals paths may remain unchanged if the new export replaces the old console assets in the later integration slice.
2. Lines 32-35: accept empty lamp arrays for this explicitly lamp-free slice. Keep arrays present. The glow loops at lines 206-212 and `AssignGlows` already handle empty arrays; `HangarConsole` rejects null arrays, not empty arrays.
3. Lines 37-52, 165-174 and 288-293: remove the mandatory old left-screen reference-image imports and resolution dependency if those pictures are retired. Size all four display targets from the new screen dimensions. If new reference pictures are retained, replace them with images authored for these planes and their content rectangles.
4. Lines 275-285 and the `ExportScreenGlass` data class: consume the content rectangle as well as glass width and height. Pass it to the display construction calls at lines 165-174. The content rectangle is axis-aligned in glass-local coordinates; the clipped portions remain backing colour.
5. `Displays/HangarDisplayBuilder.cs:33-62,99,192,199`: adapt the existing main, detail and weapon layouts to those content rectangles. Its current main and detail placements use the old picture's pixel grid. Keep the full-glass render target and place/scale content within the safe rectangle, so `HangarConsoleRaycaster` can continue to convert full-glass UVs into render-target pixels without a new remapping path.

No changes are needed to `AuthoredUp`, `Control`, their call sites at lines 185-194, or legend parenting at lines 113-120. Blender-local +Z is the outward cap axis. Export with `bake_space_transform=True`, `axis_forward='-Z'`, `axis_up='Y'` maps it to Unity-local +Y. `AuthoredUp` verifies that this axis has the largest cap area. `Control` chooses its inward sign using the camera direction. The knob turns around that same axis. Legends are placed in their control's frame and the builder parents them while preserving world placement.

The existing warm emission tint at line 302, the old spill offsets at lines 138-141 and 175-178, and the separately imported viewport etching at lines 73-80 should be reviewed when finish and game integration are scoped. They do not prevent the shape and part contract from working, so this slice does not prescribe new lighting or window artwork.
## Measured geometry

### Pod corners and recessed screens, 2026-11-19

Both upper inner housing chamfers now use 1.02 by 0.72 setbacks, parallel to the unchanged 0.85 by 0.60 glass clips. The middle and rear shell follow that same outline, removing the old notch. The top is lowered by 0.023076 local units to compensate for banking, so the wider corner does not increase the pod height. The front housing bounds remain Z = 1.057527 to 4.738953 on both sides. The other three housing corners already follow their glass clips at 45 degrees and retain their outlines.

The pod face stays at local depth -0.10. Each glass moves from -0.008 to -0.20, placing it 0.10 units below the face. The raised screen lips are removed. Each charcoal tray flares outward at 45 degrees, with 0.008 units of clearance around the glass at its depth and a floor 0.015 units behind it. The mouth is an exact perpendicular offset of the clipped glass outline. The face bars between the two mouths are 0.044 units wide on the left and 0.084 on the right. Weapon step keys keep their frames and travel; their wells now taper outward toward the frame opening.

Recess cuts run after the structural unions. Boolean coordinates are welded within 0.000001 units during construction and within 0.00001 before final triangulation. This removes duplicate vertices from Boolean rounding without changing visible contours. The final mesh must pass the closed-manifold and zero-area checks before it is saved.

Corner margins below are measured at 1920 by 1080. Each value is the shortest pixel distance from the midpoint of a glass clipped corner to the projected outer perimeter of its pod face. They measure housing coverage, including the recess, rather than the dark wall alone. Large values occur beside the other display or the weapon keys. Left and right in this table mean image left and image right. The exact projected polygons and measurements are in `previews/recess-audit.json`.

| Screen | Corner | Previous margin, px | Updated margin, px |
|---|---|---:|---:|
| SHIP | bottom left | 128.06 | 121.82 |
| SHIP | bottom right | 53.77 | 60.98 |
| SHIP | top right | 9.57 | 30.79 |
| SHIP | top left | 18.35 | 25.78 |
| DETAIL | bottom left | 28.38 | 21.58 |
| DETAIL | bottom right | 25.14 | 19.12 |
| DETAIL | top right | 75.01 | 69.65 |
| DETAIL | top left | 85.37 | 78.21 |
| PRIMARY | bottom left | 52.42 | 59.47 |
| PRIMARY | bottom right | 124.42 | 120.23 |
| PRIMARY | top right | 18.35 | 25.78 |
| PRIMARY | top left | 9.57 | 30.79 |
| ORDNANCE | bottom left | 54.26 | 61.54 |
| ORDNANCE | bottom right | 82.38 | 75.18 |
| ORDNANCE | top right | 125.65 | 121.41 |
| ORDNANCE | top left | 52.87 | 59.97 |

The two upper inner margins increase from 9.57 to 30.79 pixels. All sixteen glass corners were checked. The smallest updated corner margin is 19.12 pixels at DETAIL's bottom right. Glass depth changes account for the small perspective changes at the other corners; their sizes and orientations are unchanged.

The finished mesh has charcoal material on all 32 screen walls. Wall normals measured at their midpoints give angles from 44.999488 to 45.000385 degrees to the pod face.

The six camera renders use the supplied game projection, position and direction. For each view, the audit casts rays to 715 points around and inside every glass against the finished body. All rays reach the glass without a body intersection. Every glass vertex also remains inside its projected mouth. The minimum projected glass-to-mouth clearances are listed below. These margins keep Freestyle ink away from the glass boundary.

| View | SHIP, px | DETAIL, px | PRIMARY, px | ORDNANCE, px |
|---|---:|---:|---:|---:|
| 16x10 | 5.43 | 6.24 | 4.51 | 5.78 |
| 3x2 | 4.58 | 5.26 | 3.81 | 4.88 |
| 4x3 | 4.08 | 4.68 | 3.39 | 4.34 |
| 16x9 | 5.38 | 6.18 | 4.47 | 5.73 |
| 21x9 | 5.38 | 6.18 | 4.47 | 5.73 |
| 32x9 | 5.38 | 6.18 | 4.47 | 5.73 |

The original 16:9 render is preserved in `previews/deck-recess-original.png`. `previews/deck-recess-compare.png` places it beside the updated render and includes four 3x crops of the upper inner corners. These are Blender camera renders with the existing placeholder glasses and hologram, not a new Unity capture.

### Exporter parts

| Exported part | Blender object |
|---|---|
| MainScreenGlass | `07 \| Main rectangular display` |
| DetailScreenGlass | `07 \| Detail rectangular display` |
| PrimaryScreenGlass | `30 \| Primary blank glass` |
| OrdnanceScreenGlass | `30 \| Ordnance blank glass` |
| PreviousButton | `08 \| Previous chunky keycap` |
| PreviousLegend | `08 \| Previous inlaid chevron` |
| NextButton | `08 \| Next chunky keycap` |
| NextLegend | `08 \| Next inlaid chevron` |
| SelectorKnob | `08 \| Selector faceted grip` |
| LaunchButton | `08 \| Launch broad beveled button` |
| LaunchLegend | `19 \| Launch legend` |
| PrimaryPreviousButton | `30 \| Primary Previous key` |
| PrimaryPreviousLegend | `30 \| Primary Previous ivory inlay` |
| PrimaryNextButton | `30 \| Primary Next key` |
| PrimaryNextLegend | `30 \| Primary Next ivory inlay` |
| OrdnancePreviousButton | `30 \| Ordnance Previous key` |
| OrdnancePreviousLegend | `30 \| Ordnance Previous ivory inlay` |
| OrdnanceNextButton | `30 \| Ordnance Next key` |
| OrdnanceNextLegend | `30 \| Ordnance Next ivory inlay` |
| ProjectorLens | `09 \| Projector lens` |
| ConsoleBody | `Deck \| joined closed structure` |
| ConsoleBody | `Deck \| left pod cyan strip` |
| ConsoleBody | `Deck \| left strut cyan strip` |
| ConsoleBody | `Deck \| right pod cyan strip` |
| ConsoleBody | `Deck \| right strut cyan strip` |
| ConsoleBody | `Deck \| header cyan strip -8.6` |
| ConsoleBody | `Deck \| header cyan strip 0` |
| ConsoleBody | `Deck \| header cyan strip 8.6` |

The body includes the housings, canopy, strut shoulders, wraparound band, raked panels, pedestal, sill and eight decorative switches. The seven cyan strips are separate static solids assigned to ConsoleBody; none has a name ending in ` lens`. The projector is the only object with that suffix and uses the exporter's existing exception.

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

### Mesh report

| Object | Closed | Manifold | Triangles |
|---|---|---|---:|
| `Deck \| joined closed structure` | Yes | Yes | 6,928 |
| `07 \| Main rectangular display` | No, planar glass | No, planar glass | 6 |
| `07 \| Detail rectangular display` | No, planar glass | No, planar glass | 6 |
| `30 \| Primary blank glass` | No, planar glass | No, planar glass | 6 |
| `30 \| Ordnance blank glass` | No, planar glass | No, planar glass | 6 |
| `08 \| Previous chunky keycap` | Yes | Yes | 56 |
| `08 \| Previous inlaid chevron` | Yes | Yes | 20 |
| `08 \| Next chunky keycap` | Yes | Yes | 56 |
| `08 \| Next inlaid chevron` | Yes | Yes | 20 |
| `08 \| Selector faceted grip` | Yes | Yes | 44 |
| `08 \| Launch broad beveled button` | Yes | Yes | 300 |
| `19 \| Launch legend` | Yes | Yes | 236 |
| `30 \| Primary Previous key` | Yes | Yes | 56 |
| `30 \| Primary Previous ivory inlay` | Yes | Yes | 20 |
| `30 \| Primary Next key` | Yes | Yes | 56 |
| `30 \| Primary Next ivory inlay` | Yes | Yes | 20 |
| `30 \| Ordnance Previous key` | Yes | Yes | 56 |
| `30 \| Ordnance Previous ivory inlay` | Yes | Yes | 20 |
| `30 \| Ordnance Next key` | Yes | Yes | 56 |
| `30 \| Ordnance Next ivory inlay` | Yes | Yes | 20 |
| `09 \| Projector lens` | Yes | Yes | 44 |
| `Deck \| left pod cyan strip` | Yes | Yes | 28 |
| `Deck \| left strut cyan strip` | Yes | Yes | 28 |
| `Deck \| right pod cyan strip` | Yes | Yes | 28 |
| `Deck \| right strut cyan strip` | Yes | Yes | 28 |
| `Deck \| header cyan strip -8.6` | Yes | Yes | 12 |
| `Deck \| header cyan strip 0` | Yes | Yes | 12 |
| `Deck \| header cyan strip 8.6` | Yes | Yes | 12 |

The model has 28 mesh objects: 24 closed manifold solids and four intentional planar glasses. Hardware total: **8,180 triangles** and 4,144 vertices. All solid windings are consistent and signed volumes are positive. There are no loose vertices or unapplied modifiers. Zero-area faces: 0.

An independent Boolean-volume audit checked 30 object pairs whose world bounding boxes overlap. It found 0 intersections above 0.0000001 cubic units. All eight moving controls and their carried legends were also checked against the body at half and full travel, using the existing builder's travel fractions: 0 intersections. The report is `previews/mesh-audit.json`.

There are 3 positive-area Boolean sliver triangles smaller than 0.0000000001 square units. They remain connected to their closed surfaces; none is a loose or zero-area face.

The inherited hologram and backdrop below are preview objects, excluded by the old exporter's existing name prefixes. They are not console construction. Their open planes are intentional visual staging.

| Excluded preview object | Closed/manifold | Evaluated triangles |
|---|---|---:|
| `19 \| Vanguard preview swept wing envelope` | Yes | 60 |
| `19 \| Vanguard preview wing perimeter` | No, preview surface | 128 |
| `19 \| Vanguard preview raised fuselage` | No, preview surface | 8 |
| `19 \| Vanguard preview fuselage ridge` | No, preview surface | 24 |
| `19 \| Vanguard preview fuselage ridge.001` | No, preview surface | 48 |
| `19 \| Vanguard preview tail fin` | No, preview surface | 1 |
| `19 \| Vanguard preview fin edges` | No, preview surface | 24 |
| `19 \| Vanguard preview tail fin.001` | No, preview surface | 1 |
| `19 \| Vanguard preview fin edges.001` | No, preview surface | 24 |
| `11 \| Neutral projector frustum` | No, preview surface | 2 |
| `19 \| Distant space` | Yes | 12 |

The entire scene, including evaluated preview curves and the backdrop, has **8,512 triangles**, below the 70,000-triangle limit.

### D13 departures and comparison

- Pod faces stay at -0.10; unchanged glass outlines sit at -0.20 in 45-degree charcoal recesses, without raised lips.
- Mirrored upper inner housing chamfers follow the screen slope; the rear shell follows the same outline without the old notch.
- Hidden band undersides, pod cavity walls, joined heels and ramp returns close the open massing.
- Weapon keys retain their exact visible tops; their surrounding wells provide real depth.
- Pedestal dark riser strips omitted as surface detail reserved for slice 2.
- Thin inlaid key chevrons and launch lettering added as required moving legends.
- Camera uses old console AUTO sensor, 36 x 24 mm and 33 mm lens: vertical FOV 34.113931 degrees.
- Ship, selector and launch well floors are -.125 in their local frames to clear existing press travel.
- Raised housing skirts terminate inside the band front return; eight decorative switches extend invisibly into their well floors.
- Well cutter mouths expand 0.3 percent to prevent coincident edges at the bevel boundary.
- Header, pod shoulders, band and canopy side walls continue to X = +/-27 for wide cameras.

The camera overlay is an exact 50% blend at 1920 by 1080. The comparison sheet places the two native-resolution renders side by side with labels. The false-colour parts sheet includes each exporter role and its corresponding index.

The unlit hardware masks have intersection-over-union 96.3213%. They differ at 40,834 of 2,073,600 image pixels. The 95th-percentile boundary distance is 14.000 pixels; the largest is 74.000 pixels. These values include filled massing gaps. They are not a claim that every pixel is identical. See `previews/silhouette-comparison.json`, `previews/silhouette-difference.png` and the overlay for the exact comparison.

### Screen-shape extension

The header, outer pod housings and wraparound band continue to X = +/-27. Folded side walls join the header to the lower structure, leaving one additional side window on each side. These pieces are Boolean-unioned into the existing ConsoleBody object. The lower return continues down and outward behind the console and pod housings. The header continuation overlaps the existing beam behind its visible faces. The construction views are refitted to show the wider model.

Against the saved original deck hardware mask, the 1920 by 1080 silhouette has intersection-over-union 96.7272%. Changed pixels: 36,326 of 2,073,600. The 95th-percentile boundary displacement is 14.000 pixels; the maximum is 65.000 pixels. This comparison uses `previews/reference/deck-mask.png`, not the older D13 massing. The original rendered camera view is preserved beside it. See `previews/aspect-comparison.json` and `previews/deck-extension-difference.png`.

Moving-control frames and cap geometry are unchanged: True. Screen-glass outlines, orientations and frames are unchanged, with only local depth changed: True. The object names, part roles, palette, ink linesets and lamp-free construction remain intact.

| View | Resolution | Horizontal FOV | Vertical FOV |
|---|---|---:|---:|
| 16x9 | 1920 x 1080 | 57.220918 | 34.113930 |
| 21x9 | 2560 x 1080 | 72.054745 | 34.113930 |
| 32x9 | 3840 x 1080 | 94.979104 | 34.113930 |
| 16x10 | 1920 x 1200 | 56.742882 | 37.302549 |
| 3x2 | 1620 x 1080 | 56.742882 | 39.601404 |
| 4x3 | 1440 x 1080 | 56.742882 | 44.099881 |

The table above gives effective fields of view after the 1.01 diagonal scale. All views use the same camera position and direction.

### Game projection and measured edge coverage

`deck_projection.py` applies the supplied projection to all camera previews, masks, recess checks and magenta evidence. Narrower views increase the vertical FOV by the supplied tangent ratio, then divide that tangent by 1.01. Blender uses horizontal sensor fit and `shift_y = m12 / (2 * aspect)`. A negative shift looks farther down. The 16:9 camera retains AUTO fit and its 33 mm lens.

| View | Initial vertical FOV | Effective vertical FOV | Unity m12 | Blender shift_y | Top 3% magenta | Bottom 3% magenta | Above header magenta | Below console magenta | Outside windows magenta |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 16x10 | 37.64941878 | 37.30254856 | -0.03600000 | -0.01125000 | 0 | 0 | 0 | 0 | 0 |
| 3x2 | 39.96621172 | 39.60140372 | -0.05625000 | -0.01875000 | 0 | 0 | 0 | 0 | 0 |
| 4x3 | 44.49804587 | 44.09988083 | -0.09000000 | -0.03375000 | 0 | 0 | 0 | 0 | 0 |
| 16x9 | 34.11393000 | 34.11393000 | 0.00000000 | 0.00000000 | 0 | 0 | 0 | 0 | 0 |
| 21x9 | 34.11393000 | 34.11393000 | 0.00000000 | 0.00000000 | 0 | 0 | 0 | 0 | 0 |
| 32x9 | 34.11393000 | 34.11393000 | 0.00000000 | 0.00000000 | 0 | 0 | 0 | 0 | 0 |

The magenta renders hide the hologram, projector fan and original backdrop. A saturated magenta world is visible only to camera rays, so it does not tint the hardware. Bands contain `ceil(height * 0.03)` rows. Counts use saved PNG values R > 180, G < 80 and B > 180. The window-opening check tests the entire frame against the projected header lower edge, console upper surface and side-window boundaries. Its frozen baseline window mask also retains existing isolated antialias pixels within three pixels of a window, with every RGB channel below 80. Only those original pixels are included; this does not permit new pixels around the window. The JSON lists their coordinates, values and current differences, plus a looser chroma test and the raw counts before those baseline pixels are included. `previews/deck-coverage-sheet.png` labels all six full renders. Blender's projection matrix matches the supplied matrix within 0.000001.

The comparison reference is the camera image present at the start of this task, preserved in `previews/coverage-reference/deck-camera.png`, with its hardware mask beside it. Hardware intersection-over-union is 97.042319%. Changed pixels: 32,795 of 2,073,600. Added: 32,795; removed: 0. Added mask pixels inside frozen windows: 0, including 0 formerly saturated magenta pixels. Mask comparisons use alpha > 127. Boundary distance is 14.000 pixels at the 95th percentile and 65.000 pixels maximum.

`previews/gap-geometry.json` checks all 2,073,600 pixel-centre rays. It finds 32,795 backdrop-to-structure changes, no existing hit-position changes above 0.0001 units and no material or object changes. The maximum existing position difference is 1.1949505e-05 units. Non-body meshes, materials, ink, linesets and camera match exactly. The 3 raw normal differences come from unchanged overlapping or shared-edge triangles; exact triangle comparisons and nearby rays explain them in the report.

The rendered 16:9 RGB image differs at 55,172 pixels. The changed bounds are [0, 0, 1919, 1079], inclusive X/Y pixel coordinates. Top-region changes: 25,993 at [0, 0, 1919, 13]; bottom-region changes: 5,707 at [0, 922, 1919, 1079]. Window changes: 1,037; non-backdrop changes: 22,435; antialias boundary changes: 0. Changes outside the permitted top and bottom backdrop regions: 23,472. Individual changed-region bounds are in `previews/coverage.json`.

The literal rendered camera PNG also changes pixels outside the permitted backdrop regions; see rendered_rgb_16x9. Geometry preservation does not imply identical rendered RGB.

Of the disallowed changes, 18,652 differ by one RGB value. The larger changes cluster around the former top outline. `previews/deck-rgb-difference-amplified.png` shows absolute RGB differences multiplied by ten, with values capped at 255.

The earlier reference-frustum subtraction preserved sky already visible at 16:9 and left a seam between the cap and header. The original body, including its clipped extensions, is completed before the final unions. The new crown spans X = +/-27, Y = 3.35 to 7.65 and Z = 10.61 to 15, with 0.01 overlap at the header top. The new lower skirt spans X = +/-27 and Y = -2 to 2.35, reaching down to Z = -5. Its top rises from Z = 0.20 at the front to 0.65 at the rear, behind the console and pod housings. These closed solids join the existing body without new objects. Existing controls, recesses, pod corners, materials, names and linesets are preserved.

All six aspects pass the edge-band and window-opening checks.

### Black side bars

The shared exporter's projected Freestyle ink image causes the bars. Its `EXTEND` sampling repeats the 16:9 border texel onto geometry outside that image, turning pod outlines into horizontal black bands. The bars reproduce with assigned source materials, closed outward-facing solids and no paint bake. Missing materials and zero-thickness faces are not the cause.

The shared exporter's `projected_ink` function returns white outside the reference image, which leaves the original paint unchanged. The shared exporter owns FBX, atlas generation, part mapping and the manifest beside it. This task does not execute its top-level export code.

`verify_ink.py` extracts only `projected_ink` from the shared exporter with Python's AST. It compares the returned bounded socket with the same graph's pre-bound input in headless Blender. The left/right black-pixel counts are [2484, 2454] before the bound and [0, 0] with the shared function's returned socket. Changed central 16:9 pixels: 0. `previews/ink-verification.json` records the sample boxes and threshold. This tests the shader graph used for paint baking without running an atlas bake or Unity import.

`previews/local-export-verification.json` and `previews/local-export-manifest.json` record an earlier revision's local export. They do not verify this gap-closure revision. No new atlas bake, FBX export or Unity export was run.
