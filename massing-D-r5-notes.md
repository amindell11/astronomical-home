# Cockpit D / round 5

Started from `round4/build_D4.py`. Three finishing changes, retaining round 4's camera, screen planes, pod shells and canopy.

## The three fixes

1. **Filled weapon pod.** PRIMARY and ORDNANCE are both **4.25 x 2.10**, increased equally from 1.60 tall. PRIMARY keeps its centre at local V=2.65; ORDNANCE moves to V=-0.07. The pair now runs from V=-1.12 to 3.70, exactly the full extent of SHIP + DETAIL on the left. The blank lower filler is removed. No residual strip remains. Each weapon retains its previous/next key pair beside it. SHIP remains the largest individual screen: nominal rectangular area 14.875 square units versus 8.925 for each weapon.
2. **Taller stepped projector.** Lens centre **Z=2.48**, up from 1.80. Lens top is 2.4925, below the clear volume's Z=2.50 floor. Three inclined, faceted risers have exposed octagonal ledges at Z=1.69 and 2.08 and a top at Z=2.42. Dark front insets distinguish the three risers. The hologram mesh, its rotation and position, and the 5 x 4 x 4 clearance volume are unchanged. Only the beam's lower endpoints rise; its upper endpoints remain fixed.
3. **Designed wings.** Each wing now has a physical 0.18-unit drop toward the viewer over a 0.12-unit run, fading into the unchanged centre and outer edge. The main crown and outer outline retain round 4's lines. One chamfered raised access block per side follows the cowl's front/outward fall; its top slopes down 0.16 per outward unit and up 0.14 per rearward unit. The panel footprints and stepped wing geometry are exact X reflections. Existing ship keys and selector remain on their front landing pads.

## Screen retention and visibility

| Screen | True size | Width retained | Height retained | Visibility |
|---|---:|---:|---:|---:|
| SHIP | 4.25 x 3.50 | 95.21% | 88.86% | 1341/1341 clear |
| DETAIL | 3.76 x 0.92 | 94.67% | 93.65% | 1305/1305 clear |
| PRIMARY | 4.25 x 2.10 | 95.37% | 87.46% | 1333/1333 clear |
| ORDNANCE | 4.25 x 2.10 | 94.78% | 92.64% | 1333/1333 clear |

Retention uses round 4's convention: project each unit screen axis perpendicular to the sightline from that screen's centre to the camera, sqrt(1 - dot(axis, sightline)^2). It excludes distance magnification. All screens exceed 80% width and 75% height. Dense camera ray samples include screen interiors and points near the clipped outlines; none are occluded. All control centres pass their camera visibility checks.

## Preserved round 4 geometry

- **Pod angles are exactly round 4's:** yaw magnitude 36 degrees, back lean 38 degrees, bank parameter -4 degrees. Left yaw/lean/bank = +36 / 38 / +4; right = -36 / 38 / -4. Original construction order is retained, with zero lower-screen break. No screen has an independent rotation.
- Pod origins, 5.85 x 5.60 carriers, shoulder geometry, 2.15 depth and root feet are unchanged. Mirrored shell error: 0.00000000 units; equal world heights.
- Canopy landings ((-3.2, 1.65, 1.06), (3.2, 1.65, 1.06)), tops ((-7.1, 4.15, 10.05), (7.1, 4.15, 10.05)), widths and depths are unchanged. Struts stay outside the 5-unit-wide hologram slab.
- Centre cowl, launch spine and launch's 9-degree rake still fall toward the viewer. The wing steps and raised panel planes retain forward and outward fall.
- Fixed camera: (0.0, -18.0, 6.8), direction (0.0, 0.9936, -0.1132), vertical FOV 34.11 degrees, 1920 x 1080.
- Flat colours, explicit facets and block controls. One area key light with uniform world ambient. No textures, scene text or modifiers.

## Rebuild and evidence

Run from the supplied folder:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D5.py
```

`-- --preview` renders the fixed camera only; `-- --check` runs geometry and visibility checks without rendering. The full run saves the blend, five views, comparison and measurements. Comparison composition uses the local Python/Pillow path named at the top. No network is used. `measurements.json` records projected edges, screen retention, ray visibility, pod symmetry, clearance bounds and validation results.
