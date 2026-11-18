# Cockpit D / round 7

Derived directly from `round6/build_D6.py`. All work is local.

## Shorter pods

Both mirrored housing profiles shorten from 5.60 to 4.40 units,
with the foot held at local V=-1.52. The crown drops from
V=4.08 to 2.88; the attached sill follows it, with a slimmer
0.16 rise (formerly 0.24). The rear housing crown and both chamfer
miters follow the shortened profile too, opening the pane all the way out to
the unchanged outer pillar. No old-height closure is left behind the pod.

SHIP/DETAIL's clear inter-screen gap tightens from 0.40 to 0.26 units;
PRIMARY/ORDNANCE's from 0.62 to 0.30. The top screen-to-crown margin is 0.18
(formerly 0.38). Screen centres, in local V: SHIP 1.20, DETAIL
-0.96, PRIMARY 1.90, ORDNANCE 0.00.
The previous/next key pairs follow their weapon screens. The lower weapon
heel retains its existing fold into the unchanged control ledge.

## Camera measurements

Pixel Y is measured downward from the top of the 1920 x 1080 image.
Both pods are exact mirrors and have identical Y measurements.

| Camera measurement | Round 6 | Round 7 |
|---|---:|---:|
| Highest pod/sill silhouette, Y | 408.1 px | 492.2 px |
| Fixed pod foot, Y (ledge excluded) | 951.2 px | 951.2 px |
| Pod/sill silhouette height | 543.1 px | 459.0 px |
| Sill top edge Y: outer / middle / inner | 452.2 / 428.2 / 408.1 px | 554.1 / 520.3 / 492.2 px |
| Open window / whole frame | 31.49% | 34.74% |
| Visible open-window pixels | 653,066 | 720,271 |

The crown silhouette drops **84.1 pixels**, or
**15.48%** of its previous camera height.
The top edge slopes, so its outer/middle/inner samples are also recorded above;
the outer endpoint itself sits just outside the image crop in both renders.

Open-window share rises **3.24 percentage
points**, a **10.29%** increase
in visible sky. Measured identically from the supplied D6 render and final D7
render: count connected flat-blue sky pixels in the left, centre and right
panes, within RGB tolerance 2 of the sampled sky. Divide by all 2,073,600
frame pixels. The hologram/projection fan and exterior strips outside the
canopy are excluded; this is visible sky, not a bounding-box estimate.
Per-pane counts and full-precision crown coordinates are in `measurements.json`.


## Screen retention and clearance

| Screen | True size | Width retained | Height retained | Camera rays clear | Light rays clear |
|---|---:|---:|---:|---:|---:|
| SHIP | 4.25 x 3.00 | 95.04% | 90.30% | 1337/1337 | 1953/1953 |
| DETAIL | 3.76 x 0.80 | 94.60% | 94.14% | 1301/1301 | 1881/1881 |
| PRIMARY | 4.25 x 1.60 | 95.19% | 88.94% | 1329/1329 | 1917/1917 |
| ORDNANCE | 4.25 x 1.60 | 94.79% | 92.50% | 1329/1329 | 1917/1917 |

Retention is sqrt(1 - dot(screen_axis, centre_to_camera_unit_vector)^2),
excluding perspective magnification, as in round 6. All four screens exceed
80% width and 75% height. Dense camera rays and area-light rays include edges
and interiors: no screen is covered or shadowed. All control centres remain
visible. `measurements.json` stores the detailed checks.

## Unchanged

Yaw 36 degrees, lean 38 degrees, bank -4 degrees; origins, widths, depth and
the lower pod edges. The full-length inner ribs still share the pod edge and
meet the lowered sill directly. No rib crosses a screen. Header and outer
pillars are unchanged. Both folded control ledges, wings, falling centre,
launch housing, selector, three-step pedestal and lens (Z=2.48) are unchanged.
The fighter and its clear volume centred near (0,0,4.5) are unchanged.

Fixed camera: (0.0, -18.0, 6.8), direction (0.0, 0.9936, -0.1132), vertical FOV
34.11 degrees, 1920 x 1080. One key light, unchanged;
flat materials, no textures, scene text or modifiers.

## Rebuild

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D7.py
```

`-- --preview` renders the fixed camera; `-- --check` runs geometry checks.
The full build saves five views, the blend, comparison and measurements.
Local Python/Pillow only assembles the comparison and measures the images.
