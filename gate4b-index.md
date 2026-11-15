# Outpost kit, gate 4 route B: baked brush and hatching, and the coast, 2026-11-15

The owner cleared route B on 2026-11-15 after seeing route A in the game: "go ahead with route B, also when they power down they shouldnt stop immediatel,y they should slowly coast to a stop". They picked a per-station bake, hatching fixed on the hull like the ships' ink weathering, and a Unity boot for these captures (#1384).

The Unity side and the new tools are on `task/outpost-kit-engine` at `713125d27ba84e0bd9fef92944bcdce58def8c3e`, with no PR. Nothing in the game references the stations yet.

| File | What it is |
|---|---|
| `gate4b-sheet-copper-z15.png`, `gate4b-sheet-copper-z25.png` | Copper Turquoise: every station, open, in use and dark, at orthographic size 15 and 25 |
| `gate4b-sheet-verdant-z15.png`, `gate4b-sheet-verdant-z25.png` | The same on Verdant Giant |
| `gate4b-compare-repair-copper.png` | Route A (flat colours) beside route B, same locale and zoom |
| `gate4b-compare-repair-copper-zoom.png` | The same, cropped and doubled, where the brush strokes and hatching show |
| `gate4b-station-coast-copper.mp4` | Each station for 20 s at size 15: 3 s open, 2 s in use, 11 s dark while the parts coast to a stop, 4 s relit while they spin back up |
| `gate4b-coast-mod-copper.gif` | The mod station's 20 s from that clip, denoised and shrunk |
| `gate4b-<locale>-<station>-z<size>-<look>.png` | The 36 full 1280x720 stills behind the sheets |
| `gate4b-bake-preview-<station>.png` | Blender's review render of each bake, top and tilted, shaded the way the shader composes the masks |
| `gate4b-export_station.py` | Blender export, now keeping `PaintUV` on the hull and dropping it from the contour meshes |
| `gate4b-BuildOutpostStations.cs` | Editor script (`run_script`): per-station painted hull materials, mask import settings, model prefabs |
| `gate4b-clip.cs`, `gate4b-place_models.cs`, `gate4b-clear_rocks.cs`, `gate4b-quality.cs`, `gate4b-state.cs` | Capture steps changed or added since route A; the rest are the `gate4-` scripts |

## How the bake works

- `art/tools/station/station_uv.py` gives every hull mesh a `PaintUV` layer: one 2048 atlas per station, all hull meshes at one texel density (22 to 29 texels per unit), saved into the station's `.blend` under `art/stations/`.
- `art/tools/station/station_paint.py` bakes the detail pass's own brush and hatch node setups, in each mesh's object space, onto that atlas, and writes `Shadow.png`, `Light.png` and `Ink.png` beside the source. It reuses `ship_paint`'s bake code. A render of the procedural setups against the baked masks matches stroke for stroke.
- In Unity each station has its own six hull materials with the shader's paint layers on, binding its masks. The strengths are on the material, so they can be tuned without a rebake: brush 0.86 for shadow and light, ink 0.26 (0.13 per stroke set, as accepted).

## How the captures were made

As for route A (`gate4-index.md`), plus: asteroids within 60 u of the ship are hidden and asteroid spawning is stopped, and staging zeroes the ship's velocity. The clip is again a locked 24 fps of game time with every game frame captured.

## What differs from the accepted detail pass

- The hatching is fixed on the hull, so it shows in lit areas as well as in shadow. The owner chose this over a shader change.
- The brush tile sits mostly below its midpoint, so the bake darkens more than it lightens: the hulls read about 8 to 9% greyer than route A. That matches the accepted compose formula.
- The game shader has two shading bands, not the preview's three.
