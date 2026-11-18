# Cockpit D / round 4

The screens now lean with the window ribs and rise inward toward the raised centre. Started from `round3/build_D3.py`. The only modelling changes are three pod construction angles; all other geometry, materials and lighting remain from round 3. Measurement, overlay and comparison output code was added.

## Mirrored pod angles

| Construction parameter | Round 3 | Round 4 |
|---|---:|---:|
| Yaw magnitude | 52° | 36° |
| Back lean from vertical | 36° | 38° |
| Bank parameter | +16° | -4° |

Left yaw/lean/bank = **+36° / 38° / +4°**; right = **-36° / 38° / -4°**. These use the original script's construction order (yaw, back lean, then bank of the two face axes), not Blender Euler rotations. The bank sign is reversed. A direct +16° to -16° flip at the old yaw/lean overshot the rib angle by about 16°. Reducing yaw and using a smaller reversed bank gives the final alignment with comfortable screen retention.

The face carriers, deep housings, shoulders, screen apertures, clipped screen corners, control rail and pod inserts all use the same revised basis. No screen is independently skewed inside its shell. All screens on a pod remain coplanar; lower-screen break stays 0°. The short corner cuts follow their housing chamfer family; the long inner side is the rib-alignment reference.

Both pod origins remain X=+/-6.35, Y=0.25, Z=2.45. Each carrier remains 5.85 x 5.6, depth 2.15, shoulder depth 0.62, shoulder expansion 0.38. Shells have identical Z bounds 1.058 to 5.923. Maximum mirrored shell vertex error: 0.00000000 units. The two screen layouts differ only as required by their specified sizes and original positions.

## Measured in the fixed camera image

Angles are undirected line slopes measured from horizontal, normalized to -90° to +90°. Image X points right and image Y down: **positive falls to the right; negative rises to the right**. Thus the left rib is positive and the right rib negative. Exact projected mesh vertices at 1920 x 1080 determine the screen edges; struts use their projected longitudinal axes. The largest inner-edge/rib difference is **0.34°**, below the 5° target.

| Screen | Inner edge | Adjacent rib | Difference | Bottom edge | Wing apron | Difference |
|---|---:|---:|---:|---:|---:|---:|
| SHIP | +68.91° | +68.83° | 0.08° | -11.04° | -12.84° | 1.80° |
| DETAIL | +69.16° | +68.83° | 0.34° | -13.34° | -12.84° | 0.50° |
| PRIMARY | -68.91° | -68.83° | 0.08° | +8.36° | +12.84° | 4.48° |
| ORDNANCE | -68.91° | -68.83° | 0.08° | +11.98° | +12.84° | 0.86° |

The wing reference is the **lower front edge of the broad pod apron immediately below each pod** (`Left/Right | broad forward dash root`, root vertices 4-5). These are real inherited edges, not added guide geometry. The outline overlay draws the exposed outer 65% of each line; its inboard continuation meets beneath the cowl. The unchanged centre-cowl shoulder edge, at the crown between |X|=1.55 and 3.40, measures **-10.37° left / +10.37° right** and is also overlaid. Both structural slopes agree with the new screen-bottom direction.

Overlay colours: gold = ribs; pink = screen inner edges; green = screen bottom edges; orange = wing aprons; ivory = centre-cowl shoulders. Thin muted outlines show the full clipped screen perimeters. Pixel endpoints and 3D structural endpoints are recorded in `measurements.json` for reproducibility. Different-height screens sample the same perspective line family, so their bottom slopes need not be numerically identical.

## Sizes, readability and clearance

| Screen | Exact plane size | Width retained | Height retained | Visibility rays |
|---|---:|---:|---:|---:|
| SHIP | 4.25 x 3.50 | 95.21% | 88.86% | 1341/1341 clear |
| DETAIL | 3.76 x 0.92 | 94.67% | 93.65% | 1305/1305 clear |
| PRIMARY | 4.25 x 1.60 | 95.37% | 87.46% | 1329/1329 clear |
| ORDNANCE | 4.25 x 1.60 | 94.89% | 91.71% | 1329/1329 clear |

Retention follows the round 3 convention: project each unit screen axis perpendicular to the sightline from the screen centre to the camera, using sqrt(1 - dot(axis, sightline)^2). This removes distance magnification. Every screen exceeds 80% width and 75% height. All blue outlines remain inside the image; dense ray checks include the interior and near-chamfer edges and detect no screen occlusion. Every control also passes its visibility check. Virtual mirrored apertures match to within 0.002 pixels.

## Preserved structure

No strut lean or landing changes were needed. Landings remain ((-3.2, 1.65, 1.06), (3.2, 1.65, 1.06)); tops remain ((-7.1, 4.15, 10.05), (7.1, 4.15, 10.05)). Rib width/depth remains 0.74/1. The ribs stay outside the hologram's 5-unit-wide clearance slab. Hologram placeholder and the 5 x 4 x 4 clear volume centred at (0.0, 0.0, 4.5) are unchanged. Projector lens remains at (0.0, 0.0, 1.8).

The centre still falls forward and outward. Cowl stations, root height (0.7), stepped pedestal rings and launch housing are unchanged. The launch still rises 9° rearward, falling toward the viewer. Broad pod roots follow their revised housings automatically through the original construction, so their projected wing/apron slopes also agree. No additional object was translated or resized.

Camera remains (0.0, -18.0, 6.8), looking along (0.0, 0.9936, -0.1132), vertical FOV 34.11°, 16:9 at 1920 x 1080. One area key light and the original uniform ambient. Flat colours, block controls, explicit facets; no textures, scene text or modifiers. Labels appear only in the review overlay and comparison sheet.

## Rebuild

From the supplied folder:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D4.py
```

`-- --preview` renders only the fixed camera. `-- --check` runs geometry, projection, alignment and visibility checks without rendering. The full build saves the blend, five renders, annotated camera and three-panel comparison. Postprocessing uses the local Python/Pillow path named at the top of the script. All work uses local files; no network is required.
