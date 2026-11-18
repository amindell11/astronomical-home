# Cockpit D / round 3

Round 2 is the source. The fixed camera, heavy canopy, landed struts, faceted pod depth, blue screens and translucent ship remain. This revision replaces the raised outer wings and low centre trough with a draped cowl, a stepped projector and a raised launch spine. Pod housings are exact reflections of one geometry definition.

## Centre planes and direction of fall

Coordinates use +Y away from the viewer and +Z up. The sketch reads as a high projector crown feeding a console that reaches forward; it does not require every small surface to have the same slope. Round 2's most disruptive features were its low central break followed by a raised foreground shelf, and the strong outward climb. Both are removed.

The cowl crown is at Y=0.60. The central support beneath the pedestal is Z=1.38, falling through Z=1.17 at Y=-1.35 to Z=0.86 at Y=-3.40. There is no trough and counter-ramp. From the centre, shoulder stations at |X|=1.55, 3.40, 5.80 and 9.00 step progressively down: crown heights are 1.32, 0.98, 0.85, 0.78; front heights are 0.86, 0.58, 0.42, 0.36. The inner shoulder, wing and outer apron are separate geometric facets, explicitly triangulated and mirrored. No large single floor spans between the pods.

The centre's rear return eases down from the crown to Z=1.35 at Y=2.35; the outside rear rail blends to the strut feet. The pod-base aprons reach forward to Y=-3.85, while the central shelf reaches Y=-3.40. A two-plane fascia drops 0.30, then another 0.30 while returning away from the viewer.

The pedestal has five octagonal rings, with heights 1.12, 1.25, 1.53, 1.53, 1.74. Its wide base, sloping lower faces, horizontal terrace and smaller upper cap create real relief. The lens is at (0.0, 0.0, 1.8). Two inset blocks articulate its front face. The launch housing is a separate raised spine: its control plane at Y=-2.45, Z=1.2 rises 9 degrees rearward, hence falls toward the viewer. Its side walls fall outward to the cowl. A narrowing link meets the pedestal. The ship keys and selector sit on lower wing pads.

## Equal pods and symmetric slope

One set of construction angles: yaw magnitude 52 degrees, back lean 36 degrees, bank magnitude 16 degrees. Left yaw/bank are +52/-16; right are -52/+16. The width axis is yawed, the height axis leans, then the pair banks. These are construction angles, not Blender Euler values. All four screens share their pod's plane (lower break 0 degrees). The shallower angles replace round 2's 58/40/20 and additional 8-degree lower fold.

Both face outlines are 5.85 wide by 5.6 high, with the same chamfered silhouette, shoulder expansion, inward flare, 2.15 horizontal depth and origin Z=2.45. Each shell's world bounding box is 7.976 wide x 8.077 deep x 5.714 tall, spanning Z=0.657 to Z=6.371. The maximum shell reflection error is 0.00000000 units. The roots extend to Z=0.7 and reach 0.5 forward from the shell. The right pod uses a larger separator and a lower blank filler around its smaller screens. Control keys are in its outer rail, outside the screen boundaries. Blue outer inserts are mirrored.

Perspective slopes are compared at mirrored positions on the two planes, rather than comparing unrelated edges at different heights. Different screen heights naturally sample different positions on the same vanishing-point family. Their virtual mirrored rectangles project identically after reflection in the centre of the image; the housing silhouettes do too.

## Screen and visibility checks

| Screen | True size | Width retained | Height retained | Visibility rays |
|---|---:|---:|---:|---:|
| SHIP | 4.25 x 3.50 | 91.07% | 86.01% | 1341/1341 clear |
| DETAIL | 3.76 x 0.92 | 89.88% | 91.28% | 1305/1305 clear |
| PRIMARY | 4.25 x 1.60 | 91.39% | 84.53% | 1329/1329 clear |
| ORDNANCE | 4.25 x 1.60 | 90.39% | 89.10% | 1329/1329 clear |

Retention is the length of each unit screen axis projected perpendicular to the sightline from its centre to the fixed camera: sqrt(1-dot(axis,sightline)^2). Distance magnification is removed. Thresholds are 80% width and 75% height. Exact blue-outline sizes are unchanged. Dense rays sample each screen interior and near its chamfered edges; every screen must be unobscured and completely inside the image. Controls have individual visibility checks. Hologram bounds and strut clearance are checked. Detailed results are in measurements.json.

The camera remains (0.0, -18.0, 6.8), direction (0.0, 0.9936, -0.1132), vertical field of view 34.11 degrees, 1920 x 1080. The orthographic front is viewed from negative Y, the player's side. All review cameras frame the entire assembly. There is one area key light with uniform ambient; flat colours only, no textures, text or modifiers.

## Departures from the sketch

The fixed camera and the lens limit (Z=1.8) place the pedestal lower in the image than the drawing; the raised terraced housing uses the full allowed height. Screen dimensions are exact, so the right shell contains spare frame instead of stretching the weapon screens. The right filler is intentionally a plain solid panel. The forward shelf and outboard aprons have a sharper faceted separation than the drawing to make the corrected fall easy to inspect at massing stage. Both pods move outward to X=+/-6.35 so the canopy ribs do not cover any screen. The rear sill remains heavy but is lowered to Z=0.55, keeping its top below the central crown rather than forming a high bar behind the lens. Space stays a flat night blue; the ship is a translucent placeholder. This is an editable assembly of intersecting masses, not a watertight production mesh.

## Rebuild

Run `& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D3.py` from the supplied folder. `-- --preview` renders the fixed camera only; `-- --check` runs geometry and visibility checks without rendering. The full build saves the blend, five 1920 x 1080 renders and the three-image comparison using local Pillow. All inputs are local; no network is used.
