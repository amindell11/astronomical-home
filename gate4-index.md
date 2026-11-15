# Outpost kit, gate 4: the stations in the game's renderer, 2026-11-15

The three stations from the accepted detail pass, drawn with `DrawnSurface` and `DrawnContour`, moving, and filmed through the game's own flight camera. Owner rulings for this gate are on #1384 (2026-11-15): route A (flat colours and contours first), a proof on model prefabs that leaves the shipped prefabs alone, sources under `art/stations/`, and all three extra movement ideas.

The Unity side is on `task/outpost-kit-engine` at `53371f3920615c0ee941372b4b513dc07ab43652`, with no PR. Nothing in the game references it yet.

| File | What it is |
|---|---|
| `gate4-sheet-copper-z15.png`, `gate4-sheet-copper-z25.png` | Copper Turquoise: every station, open, in use and dark, at orthographic size 15 and 25 |
| `gate4-sheet-verdant-z15.png`, `gate4-sheet-verdant-z25.png` | The same on Verdant Giant |
| `gate4-compare-repair-copper.png` | The accepted detail-pass still beside the in-engine repair dock, same locale |
| `gate4-station-motion-copper.mp4`, `.gif` | Each station for 10 s at size 15: 6 s open, 2 s in use, 2 s dark |
| `gate4-<locale>-<station>-z<size>-<look>.png` | The 36 full 1280x720 stills behind the sheets |
| `gate4-repair-station.fbx`, `gate4-mod-station.fbx`, `gate4-armory.fbx` | The exports, as imported on the task branch |
| `gate4-export_station.py` | Blender export: drops the preview collections, names materials by colour, builds contour meshes, splits the service lights into chase bands |
| `gate4-BuildOutpostStations.cs` | Editor script (`run_script`): materials, import settings, model prefabs with `StationMotion` wired |
| `gate4-calibrate.py`, `gate4-swatch.cs` | The light-colour calibration through the game's post |
| `gate4-enter_combat.cs`, `gate4-launch.cs`, `gate4-clear_enemies.cs`, `gate4-hud.cs`, `gate4-place_models.cs`, `gate4-zoom.cs`, `gate4-look.cs`, `gate4-shot.cs`, `gate4-clip.cs`, `gate4-sheet.py` | The capture steps, run as `unity command eval_file` against the slot editor |

## How the captures were made

- The Preview Host's Combat playtest on sector 1, with the locale picked and the player invulnerable (`PreviewHostBootstrap.EnterPlayMode`), launched from the hangar. Quality is High Fidelity, the desktop default.
- Wave spawners off and other ships hidden, the HUD canvas at alpha 0, each station's model prefab placed on the parked player ship at a fixed tilt of (31.5, 20.17, 29.38), the same tilt as today's repair station model.
- The flight camera's follow component is paused and its orthographic size set to 15 or 25. Each still is that camera rendered to 1280x720, so the game's post (ACES, bloom, film grain, vignette) is in the picture.
- The clip runs at a locked 24 fps of game time (`Time.captureFramerate`), with every game frame captured, so speeds are true.
- The brief names "one of Locale_1 to 3 and one of Locale_4 to 7". Those names no longer exist. Copper Turquoise and Verdant Giant were picked overnight because the detail-pass stills used them.

## What the materials are

- Hull: one `DrawnSurface` material per colour in `detail-colour.json`, texture strength 0, so the colour is the material's palette colour. Shading uses the game's own asteroid and ship values (threshold 0.55, softness 0.18, cast shadows 0.96, palette lighting) with the owner's shadow tint (0.18, 0.18, 0.31 linear). The soft edge stands in for the preview's half-tone band.
- Outline: contour meshes with joined normals per part, 5 px on the hull and 1.6 px on the "fine" hardware.
- Ink marks: the `/ mark` meshes in the ink colour.
- Lights: black body plus emission. ACES turns saturated light colours pale, so the emission inputs are calibrated so the screen colour lands on the accepted one. Window #FEAC39 lands at (231,173,68), armory red #FF3B52 at (238,62,83), gold #FFE25A at about (228,220,104). The repair green #5BFF86 lands at (90,212,103): same hue, darker, because a brighter green turns mint under ACES.

## What it does not show

- No brush texture and no hatching. That is route B (unwrap and bake), which needs the owner's word on station tooling (#935 freeze).
- No swap: `FindStationBody`, colliders and triggers are untouched. The `Lit` question for the swap is on #1384.
- Speeds are inspector fields on the model prefabs. Preview Host tuning comes with the swap.
