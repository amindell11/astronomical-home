# Cockpit D / round 8

Derived directly from `round7/build_D7.py`; local Blender geometry and rendering only.

## Slim independent canopy

Struts are 0.38 wide x 0.30 deep, replacing 0.74 x 1.00 ribs.
Widths below include the projected return surfaces, measured perpendicular to the
strut axis from its mesh silhouette at the same image rows. Both sides mirror.

| Camera row | D7 rib width | D8 strut width | D8 / D7 |
|---|---:|---:|---:|
| 250 px | 85.2 px | 28.7 px | 33.7% |
| 350 px | 87.5 px | 29.3 px | 33.5% |
| 450 px | 89.7 px | 29.9 px | 33.4% |

Strut feet: ((-2.313033103942871, 2.6500000953674316, 1.2000000476837158), (2.313033103942871, 2.6500000953674316, 1.2000000476837158)). Tops: ((-6.714613914489746, 6.949999809265137, 10.050000190734863), (6.714613914489746, 6.949999809265137, 10.050000190734863)).
The struts stand behind the pods, in a separate rear canopy plane, and run from
short rear dash feet to the unchanged header. Their minimum Y is
2.503, behind the hologram clear box's rear face at Y=2.
No rib or closure is part of a pod housing. The crown sills and both sill miters
are removed, including the former top-to-pillar slab.

## Separate pods and open sides

Pod centres move inward by 0.80 each, from X=+/-6.35 to +/-5.55.
Y=0.25 and Z=2.45, physical height 4.40,
width 5.85, yaw 36, lean 38,
and bank -4 degrees are retained. This translation exposes the outer
housing silhouette and sky beyond it. The housing depth is reduced from 2.15 to
1.25, with a 0.42 shoulder and 0.20 outer flare,
so the rear shell no longer reaches the side pillars. The ledges move with the pods; their folded
heels and shared wing meshes sweep back into the unchanged centre and wing boundary.
Each pod grows from its folded control ledge and dash wing. There is no attachment
to a canopy strut, header or high side post. Side connections are confined to the
low dash bolster at Z=0.75.

Outer pillars change from 1.15 x 1.30 to 0.32 x 0.38,
and move to the frame edges. A single continuous beam replaces the old segmented
pillar/end caps, removing the detached dark wedges beside the old pod crowns.
Sky is exposed above and outboard of both housings.

Visible open window grows from **34.74%** to **41.11%** of the full frame
(720,271 to 852,413 pixels; **+6.37 percentage points**).
Same connected flat-sky test on both images, within RGB tolerance 2; hologram and fan excluded.
The now-connected side openings are included; disconnected exterior strips are excluded.

Actual contiguous sky pixels between housing and strut (horizontal scanlines):

| Y in camera | Left sky gap | Right sky gap |
|---|---:|---:|
| 570 px | 33 px | 32 px |
| 620 px | 32 px | 32 px |
| 680 px | 32 px | 32 px |
| 740 px | 32 px | 31 px |

The gap narrows at the foot where both structures land on the dash.


## Mirrored inner chamfers

The upper inner housing corner has U/V setbacks **1.35 x
0.95 units**. It cuts the face, shoulder and rear shell;
no cap fills the silhouette. SHIP and PRIMARY have corresponding **0.85 x
0.60** clipped corners, following the same facet slope.
All screen bounding widths/heights remain exact. The other screen corners retain
their small clips. Lower DETAIL and ORDNANCE are unchanged.

## Clearance and retained geometry

| Screen | True bounds | Width retained | Height retained | Camera rays clear | Light rays clear |
|---|---:|---:|---:|---:|---:|
| SHIP | 4.25 x 3.00 | 93.83% | 90.82% | 1309/1309 | 1899/1899 |
| DETAIL | 3.76 x 0.80 | 93.28% | 94.60% | 1301/1301 | 1881/1881 |
| PRIMARY | 4.25 x 1.60 | 94.02% | 89.48% | 1279/1279 | 1827/1827 |
| ORDNANCE | 4.25 x 1.60 | 93.52% | 93.01% | 1329/1329 | 1917/1917 |

Retention excludes perspective magnification. Dense camera and nine-point area-light
ray checks sample the actual clipped polygons. No screen is covered or shaded;
all control centres, including both weapon previous/next pairs, are visible.
Screen inner edges follow the struts in camera; detailed angles are in measurements.json.
Pod shells and control ledges are exact mirrors. One key light; no textures, text
objects or modifiers. The falling centre, launch housing, selector, three-step
pedestal, lens at Z=2.48, hologram and header are unchanged.

Fixed camera: (0.0, -18.0, 6.8), direction (0.0, 0.9936, -0.1132), vertical FOV
34.11 degrees, 1920 x 1080.

## Rebuild

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D8.py
```

`-- --preview` renders the fixed camera; `-- --check` performs geometry checks only.
The full build saves five views, blend, comparison, notes and measurements.
Local Python/Pillow assembles the comparison and counts rendered sky pixels.
