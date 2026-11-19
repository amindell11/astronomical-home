# Hangar deck, slice 1

The approved D13 outlines define this model. This slice adds closed construction, shallow recesses, control wells, moving-part frames and the exporter names. It uses flat dark blue-grey paint and the old console's Freestyle ink. Finish work remains for the next slice.

All work was local. No exporter, builder, Unity asset or file outside `art/environments/hangar-deck/` was edited. No export into Unity was run.

## Rebuild and evidence

Run at the repository root with Blender 5.1:

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'art/environments/hangar-deck/.blender'
$env:TEMP = Join-Path (Get-Location) 'art/environments/hangar-deck/.temp'
$env:TMP = $env:TEMP
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/build_deck.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python-exit-code 1 --python art/environments/hangar-deck/verify_deck.py
python art/environments/hangar-deck/assemble_previews.py
python art/environments/hangar-deck/update_notes.py
```

`build_deck.py` reads the checked-in D13 generator for its dimensions. It executes the scene construction in memory, then replaces the massing surfaces. It does not load an existing `deck.blend`. The original D13 generator and notes are unchanged. `massing/D13.blend` and `massing/D13-camera.png` were regenerated with its `--preview` option; its own checks passed.

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

These changes are documented only. Line numbers refer to the unchanged `art/environments/hangar-console/export_console.py` inspected for this slice.

1. Lines 66-70: keep the existing ` lens` name rule and the projector exception, but allow explicitly lamp-free source scenes to emit empty `lamps` and `downlights` arrays. This slice intentionally has neither. Do not treat the cyan strips as lamps.
2. Line 79: map `SelectorKnob` to only `08 | Selector faceted grip`. This slice has one knob object, without the old separate `08 | Selector pointer`. All other named part maps at lines 76-104 remain valid.
3. Lines 114-119: replace the hard requirement for image-texture nodes on the old left-screen materials. The new glasses use the placeholder blue material and contain no reference pictures. Either author new reference images for the new layouts or retire that reference-image dependency when the builder reads authored screen dimensions.
4. Lines 208-216: apply the local-XY display mapping to all four glasses, and reuse or replace an existing `DisplayUV` layer instead of adding duplicate layers. Each glass now has an explicit plane-local frame. Map the whole bounding rectangle to 0..1; keep UI content inside the content rectangle listed below and leave all surrounding pixels as backing colour. This preserves the current UV-based input mapping without stretching content into the cut corners.
5. Lines 467-470: export each glass's width and height plus its normalized content-rectangle bounds. Existing width and height calculations remain correct because all four glass meshes use local XY coordinates. The content rectangle is needed by the display layout builder, not by the FBX transform.
6. Lines 458-459 and 475: stamp the actual `deck.blend` source and verified provenance, and describe placeholder screens rather than the old static artwork.
7. Line 477: make the manifest destination follow the new scene/output contract rather than writing beside the old exporter. The matching builder manifest path must change with it. No old exporter should be run against this scene before that output-path change, because it would write outside this task's allowed directory.

The hardware filter at lines 34-37, the rest of the part maps, pivot construction at lines 108-110 and 425-432, color graph reader, carried-control ink pass and FBX coordinate conversion need no new shape-specific rules.

## Builder and display changes required later

Line numbers refer to `src/Asteroids3D/Assets/Scripts/Editor/UI/Hangar/Hardware/HangarConsoleBuilder.cs`.

1. Lines 29-30: read the new deck manifest when integration switches to this source. The Unity visuals paths may remain unchanged if the new export replaces the old console assets in the later integration slice.
2. Lines 32-35: accept empty lamp arrays for this explicitly lamp-free slice. Keep arrays present. The glow loops at lines 206-212 and `AssignGlows` already handle empty arrays; `HangarConsole` rejects null arrays, not empty arrays.
3. Lines 37-52, 165-174 and 288-293: remove the mandatory old left-screen reference-image imports and resolution dependency if those pictures are retired. Size all four display targets from the new screen dimensions. If new reference pictures are retained, replace them with images authored for these planes and their content rectangles.
4. Lines 275-285 and the `ExportScreenGlass` data class: consume the content rectangle as well as glass width and height. Pass it to the display construction calls at lines 165-174. The content rectangle is axis-aligned in glass-local coordinates; the clipped portions remain backing colour.
5. `Displays/HangarDisplayBuilder.cs:33-62,99,192,199`: adapt the existing main, detail and weapon layouts to those content rectangles. Its current main and detail placements use the old picture's pixel grid. Keep the full-glass render target and place/scale content within the safe rectangle, so `HangarConsoleRaycaster` can continue to convert full-glass UVs into render-target pixels without a new remapping path.

No changes are needed to `AuthoredUp`, `Control`, their call sites at lines 185-194, or legend parenting at lines 113-120. Blender-local +Z is the outward cap axis. Export with `bake_space_transform=True`, `axis_forward='-Z'`, `axis_up='Y'` maps it to Unity-local +Y. `AuthoredUp` verifies that this axis has the largest cap area. `Control` chooses its inward sign using the camera direction. The knob turns around that same axis. Legends are placed in their control's frame and the builder parents them while preserving world placement.

The existing warm emission tint at line 302, the old spill offsets at lines 138-141 and 175-178, and the separately imported viewport etching at lines 73-80 should be reviewed when finish and game integration are scoped. They do not prevent the shape and part contract from working, so this slice does not prescribe new lighting or window artwork.
## Measured geometry

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

Each glass is one planar eight-vertex polygon at local Z = -0.008, with its normal along local +Z. It triangulates to six triangles at export. Its local X and Y dimensions retain D13's exact sizes. Corners use the 0.20-unit clip, except SHIP's top inboard corner and PRIMARY's top inboard corner, which use 0.85 horizontal by 0.60 vertical setbacks.

A largest plain rectangle means the maximum-area rectangle aligned with the glass's local X/Y axes and contained in its clipped outline. Bounds are given as left, right, bottom, top relative to the glass centre. These are geometric limits; typography may add padding inside them. The normalized bounds use the whole glass bounding rectangle as 0..1.

| Glass | Full size | Content size | Local bounds L, R, B, T | Normalized bounds L, R, B, T |
|---|---|---|---|---|
| MainScreenGlass | 4.25 x 3.00 | 3.724999 x 2.629412 | (-1.925000, 1.799999, -1.500000, 1.129412) | (0.047059, 0.923529, 0.000000, 0.876471) |
| DetailScreenGlass | 3.76 x 0.80 | 3.360000 x 0.800000 | (-1.680000, 1.680000, -0.400000, 0.400000) | (0.053192, 0.946808, 0.000000, 1.000000) |
| PrimaryScreenGlass | 4.25 x 1.60 | 3.200000 x 1.600000 | (-1.275000, 1.925000, -0.800000, 0.800000) | (0.200000, 0.952941, 0.000000, 1.000000) |
| OrdnanceScreenGlass | 4.25 x 1.60 | 3.850000 x 1.600000 | (-1.925000, 1.925000, -0.800000, 0.800000) | (0.047059, 0.952941, 0.000000, 1.000000) |

| Glass | World centre of glass plane | World local-X vector | World local-Y vector | Outward plane normal |
|---|---|---|---|---|
| SHIP | (-7.338260, 0.926358, 3.399378) | (0.781803, 0.621098, 0.054969) | (-0.417429, 0.455865, 0.786091) | (0.463181, -0.637514, 0.615662) |
| DETAIL | (-6.436613, -0.058311, 1.701421) | (0.781803, 0.621098, 0.054969) | (-0.417429, 0.455865, 0.786091) | (0.463181, -0.637514, 0.615662) |
| PRIMARY | (7.630460, 1.245464, 3.949642) | (0.781803, -0.621098, -0.054969) | (0.417429, 0.455865, 0.786091) | (-0.463181, -0.637514, 0.615662) |
| ORDNANCE | (6.837345, 0.379320, 2.456069) | (0.781803, -0.621098, -0.054969) | (0.417429, 0.455865, 0.786091) | (-0.463181, -0.637514, 0.615662) |

For a plane centre C and the listed unit vectors U, V and N, a glass point is C + xU + yV and its plane equation is N dot (P - C) = 0. The two left glasses share the left pod plane; the two right glasses share the right pod plane. Screen positions are not independently rotated.

### Mesh report

| Object | Closed | Manifold | Triangles |
|---|---|---|---:|
| `Deck \| joined closed structure` | Yes | Yes | 5,124 |
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

The model has 28 mesh objects: 24 closed manifold solids and four intentional planar glasses. Hardware total: **6,376 triangles**. All solid windings are consistent and signed volumes are positive. There are no loose vertices or unapplied modifiers. Zero-area faces: 0.

An independent Boolean-volume audit checked 30 object pairs whose world bounding boxes overlap. It found 0 intersections above 0.0000001 cubic units. All eight moving controls and their carried legends were also checked against the body at half and full travel, using the existing builder's travel fractions: 0 intersections. The report is `previews/mesh-audit.json`.

The audit found no triangles below 0.0000000001 square units.

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

The entire scene, including evaluated preview curves and the backdrop, has **6,708 triangles**, below the 70,000-triangle limit.

### D13 departures and comparison

- Pod bezel lip rises to local depth +0.035 around exact D13 glass at -0.008; exterior pod profile stays at -0.10.
- Hidden band undersides, pod cavity walls, joined heels and ramp returns close the open massing.
- Weapon keys retain their exact visible tops; their surrounding wells provide real depth.
- Pedestal dark riser strips omitted as surface detail reserved for slice 2.
- Thin inlaid key chevrons and launch lettering added as required moving legends.
- Camera uses old console AUTO sensor, 36 x 24 mm and 33 mm lens: vertical FOV 34.113931 degrees.
- Ship, selector and launch well floors are -.125 in their local frames to clear existing press travel.
- Raised housing skirts terminate inside the band front return; eight decorative switches extend invisibly into their well floors.
- Well cutter mouths expand 0.3 percent to prevent coincident edges at the bevel boundary.

The camera overlay is an exact 50% blend at 1920 by 1080. The comparison sheet places the two native-resolution renders side by side with labels. The false-colour parts sheet includes each exporter role and its corresponding index.

The unlit hardware masks have intersection-over-union 99.5663%. They differ at 4,662 of 2,073,600 image pixels. The 95th-percentile boundary distance is 2.000 pixels; the largest is 74.000 pixels. These values include filled massing gaps. They are not a claim that every pixel is identical. See `previews/silhouette-comparison.json`, `previews/silhouette-difference.png` and the overlay for the exact comparison.

### Owner questions

No owner decision is required to finish slice 1. The next slice can decide surface finish, lamps, panel lines, decals and the final screen artwork.
