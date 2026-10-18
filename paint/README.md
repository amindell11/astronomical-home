# ship-paint evidence (#1014, #868 slice 8)

`ship_paint` run on three ships in the build slot. Everything here is evidence only: none of the
per-ship settings, region assignments or masks land with #1014 (conversions are later PRs).

| Folder | What |
|---|---|
| `crimson/` | The benchmark. `compare-top.png` / `compare-side.png`: `ship_render --set compare --textured --size 1024`, main's atlas (left) beside the tool's composite (middle) with changed pixels (right). `painted-top.png`: the composite alone. Masks (`Shadow`, `Light`, `Ink`, `Emission`), the run's `ship_paint.json`, and the slot-local `paint.json`, `palettes.json`, `lock.json`. |
| `nightshade/` | Re-derived against its current pale-blue palette with Crimson's rules; the old projected masks were set aside (the tool refuses masks without a baseline). `painted-top.png`, `painted-side.png`, masks, report, settings. |
| `vanguard/` | Evidence-only migration onto the tool layout from the study's in-game scene (`vanguard_source.py`): regions classified per face from today's atlas (`regions-top.png`), then painted. `painted-top.png`, `painted-side.png`, masks, report, settings. |
| `lineup/` | Unity capture (batch editor on the slot project, `PaintPreviewLineup.cs` + `paint_lineup.ps1`) under the game's `Lighting.prefab` with shadow maps off and a flat ambient: row 0 today's chassis prefabs, row 1 copies with the paint swapped in. Nightshade's real hull carries the region slots, so it binds the bundle's `_PaintLayers` materials (the in-engine proof of the bundle); Crimson's and Vanguard's single-slot meshes show the tool's composite as their base map on the same UVs. `lineup-large.png`, `lineup-game-scale.png`, `lineup.log`. |
| `lineup-neutral/` | The same lineup under one camera-side key light and flat ambient, where the paint reads most clearly. |

The lineups in the first commit of this branch (`cd86231`) were captured with the chassis prefabs at their stored root rotation, which is not the flight pose, so they show the ships from behind; this commit's lineups pose every root with `GamePlane.Rotation` as the spawner does. Use these.
| `fixture/` | The smoke's fixture ship composite and the smoke log (`SHIP_SMOKE_PASS`). |
| `*.py` | The evidence-only helper scripts (region assignment, preview material, tolerance measure). |

## Crimson tolerance (the brief's measure)

Fraction of silhouette pixels whose largest channel delta exceeds 16/255, Workbench textured
compare at 1024, main's atlas vs the tool's composite:

| View | Silhouette px | Changed px | Fraction | Mean delta |
|---|---|---|---|---|
| top | 440194 | 83699 | 0.190 | 13.7/255 |
| side | 108059 | 16523 | 0.153 | 13.2/255 |

Known drifts, all predicted in the brief: painted shadow is achromatic in the shader (Crimson's cool
side-wall tint is gone); fin tops gain an inset line and rim; three silver parts the seed skipped by
name (Cockpit surround, Rear cockpit yoke, Engine support frame) gain the plate treatment; three
small red parts gain inked walls (service pod cap, dorsal spine, canopy transverse frame).
