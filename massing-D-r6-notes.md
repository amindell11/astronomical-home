# Cockpit D / round 6

Built from `round5/build_D5.py` using the supplied sketch and round 5 renders. All work is local.

## The five changes

1. **Wing blocks removed.** Both raised wing access slabs and the old separate ship-key mounting pad are gone. The wings are now faceted structural surfaces sharing the ledge edges and the centre's boundary vertices, with a continuous front fascia and underside.
2. **Continuous console and control ledges.** Each pod folds forward by 34 degrees into a 2.05-deep raked ledge. Its surrounding wing sweeps inward to X=+/-1.55 and shares the centre's front, control-back, crown and rear stations. The left ledge contains two ship-selection keys below SHIP/DETAIL; the right has four chunky switches below the weapons. Each control well is a real opening in the deck, 0.16 deep, with the key tops 0.018 below the surrounding plane. The knob and launch retain their round 5 positions.
3. **Shorter weapon pair.** PRIMARY and ORDNANCE are both 4.25 x 1.85. The pair's upper extent remains local V=3.70. Centres are V=2.775 and 0.305; the 0.62 inter-screen frame remains. The released 0.50 becomes a sloping lower heel from the carrier into the right control ledge. Each weapon still has its previous/next keys beside it. SHIP and DETAIL keep their sizes and positions.
4. **Clear screen geometry.** Removed the 0.72 inward shoulder flare and the inner-side shoulder expansion. Apertures open toward the screen: the outer lip is behind the blue plane, with only a 0.008 nominal recess. Rib fronts and housing edges are at local normal D=-0.10, while screens are at D=-0.008. The single area key is centred in front of the cockpit at (0.0, -10.0, 10.8); screens receive no canopy/shoulder band. Camera rays and key-light rays are checked separately, so emission does not substitute for geometric clearance.
5. **Flush window rim.** This follows the reviewer's interpretation of "flush": a physically shared pod/rib/sill frame. Both pod origins and angles stay fixed. The canopy ribs move to become the inner jambs of those frames, and the header extends rearward to receive them. There is no rib passing across a pod face.

## Exact pod-to-rim construction

- **Left pod:** inner side local U=+2.925. Its straight edge from V=-1.07 to V=3.63 shares D=-0.10 with the outward face-edge of the left canopy jamb. A triangular miter fills the upper chamfer to V=4.08. The left sill follows the pod crown, from the inner to the outer corner at V=4.08, D=-0.10, and returns 0.95 behind the face, with a 0.24 rise. Its inner end butts directly into the jamb.
- **Right pod:** the exact reflected joint at U=-2.925, with the same V extents, normal depth, miter, crown sill and return. Its weapons sit within that frame. The outer top chamfers are also closed against each sill. The sills are the lower window rims directly behind the pod crowns, not a disconnected beam below the console.
- The jamb's long axis is each pod's local V axis, so the adjacent screen edges follow it. Jamb centres run from (+/-3.342, 1.904, 0.925) to (+/-8.188, 7.196, 10.050), replacing round 5's (+/-3.20, 1.65, 1.06) to (+/-7.10, 4.15, 10.05). This rearward rake is needed to follow the unchanged pod plane in 3D. The header's rear edge moves from Y=4.75 to Y=7.65 to receive the tops; its front edge and height stay fixed. Exact world joint coordinates are in `measurements.json` under `rim_joins`.

## Screen retention and clearance

| Screen | True size | Width retained | Height retained | Camera rays clear | Key-light rays clear |
|---|---:|---:|---:|---:|---:|
| SHIP | 4.25 x 3.50 | 95.21% | 88.84% | 1341/1341 | 1953/1953 |
| DETAIL | 3.76 x 0.92 | 94.66% | 93.64% | 1305/1305 | 1881/1881 |
| PRIMARY | 4.25 x 1.85 | 95.39% | 87.19% | 1329/1329 | 1917/1917 |
| ORDNANCE | 4.25 x 1.85 | 94.85% | 91.96% | 1329/1329 | 1917/1917 |

Retention is sqrt(1 - dot(screen_axis, centre_to_camera_unit_vector)^2), the same convention as round 5; it excludes distance magnification. Required minima are 80% width / 75% height. Camera visibility samples include near-outline points and each clipped screen interior. Light tests sample the area light's centre and eight rim positions against screen interiors and edges; zero rays are blocked. Screen/strut projected edge differences are 0.67 to 0.92 degrees. All control centres are visible. The measurements file records these results, mirrored shells, noncrossing wing triangles, clearance and all 28 locked round 5 constants. All checks pass.

## Preserved facts

- Pod angles are **exactly round 5's**: yaw magnitude 36 degrees, lean 38 degrees, bank parameter -4 degrees. Left yaw/lean/bank is +36/38/+4; right is -36/38/-4. Zero lower-screen break; no independent screen rotation. Origins remain (+/-6.35, 0.25, 2.45); shell heights remain exact mirrors.
- The centre still falls toward the viewer. The launch housing and its 9-degree rake are unchanged. The three-step pedestal and lens centre Z=2.48 are unchanged; lens top Z=2.4925 stays below the hologram clear volume. The hologram geometry and transform are unchanged.
- Fixed 1920x1080 camera at (0.0, -18.0, 6.8), direction (0.0, 0.9936, -0.1132), vertical FOV 34.11 degrees. No camera move.
- Flat colours, subtly differentiated adjacent planes, one area key plus uniform world ambient. No image textures, scene text, or modifiers.

## Rebuild

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D6.py
```

`-- --preview` renders just the fixed camera. `-- --check` runs the geometry checks. The full run saves the blend, five views, comparison, notes and measurements. The comparison uses the supplied picked sketch, round 5 camera, and round 6 camera. Local Python/Pillow is used only to lay out that comparison.
