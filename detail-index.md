# Outpost kit: the detail pass, 2026-11-15

The step between colour and the in-engine proof. The owner cleared it on 2026-11-15 and asked for "detail and ink weathering like on the ships (cross hatches and stuff)", because the stations were "a bit too cartoony".

| File | What it is |
|---|---|
| `detail-before-after.png` | The repair dock at the closest zoom, before and after the pass |
| `detail-screen-gimbal-copper.png`, `detail-screen-kite-ice.png`, `detail-screen-drum-verdant.png` | A full 1280x720 screen per station |
| `detail-backdrops-sheet.png` | Every station on four locales at the widest zoom |
| `detail-states-sheet.png` | Open, in use and dark |
| `detail-gimbal.blend`, `detail-kite.blend`, `detail-drum.blend` | The detailed models, in the blockout layout: an empty per part, a mesh per colour |
| `detail-colour.json` | The settings block, with `hatch` and `brush` added |
| `detail-stations.py`, `detail-compose.py`, `detail-tuner-template.html`, `detail-build_tuner.py` | The scripts and the tuner page |

## What the pass adds

- **Small windows on every hull.** Half the blockout's size, in two rows. The crew pods have separate panes.
- **Hardware.** Straps and trim bands on plates, raised panels, pipe runs with brackets on both faces of the repair dock's ring, cooling fins, crew hatches, handrails, masts with a lamp, bearing caps, finger grips on the repair arms. The kite's sails are a grid of cells with one left out per face. The drum's magazines have straps, a stencil band, access panels and clamps to the rings.
- **Thin outline on small parts.** Hardware carries an outline a third as heavy as the hull's, or the outline swallows it.
- **Ink marks as geometry.** Clusters of hatch strokes, some crossed, and long scratches, laid on plate faces from a fixed seed per plate. They sit in their own meshes named `/ mark`, the way the ships keep ink in an `Ink` role.
- **Shadow hatching.** One set of broken, wobbly strokes in the half-tone and shadow, and a crossing set in shadow only, each kept to its own patches of hull. It is drawn in object space, so it stays put on the hull as the station turns.
- **Brush texture.** The ships' own tile, `art/ships/crimson/textures/painted-brush-source.png`, box-projected at 7 u per repeat and read the way `ship_paint` reads it.

Which plate gets which strap, panel or mark comes from a seed built from the plate's own numbers, so a rebuild gives the same station and no two plates match.

## How to regenerate

```
blender -b --factory-startup --python detail-stations.py -- <passes dir> passes brush=<brush tile> gimbal kite drum
blender -b --factory-startup --python detail-stations.py -- <dir> blockout gimbal kite drum
python detail-compose.py <passes dir> <captures dir> <sheets dir> <gate 3 passes dir>
python detail-build_tuner.py <passes dir> <captures dir> detail-colour.json <out.html>
```

`plain` builds without the detail, and `largewin` brings back the blockout's windows.

## What the pictures are and are not

- Hatching and brush are masks rendered in Blender and applied by `detail-compose.py` and the tuner page. They show what the look could be. They are not the game's `DrawnSurface` shader, which today takes its ink and shading from painted texture layers.
- Getting this look into the game needs UVs and a paint step for stations, as the ships have. That is tooling the #935 freeze covers, and it needs the owner's word.
- Triangle counts are in the comment on #1384 that links here.
