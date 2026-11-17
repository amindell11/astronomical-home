# Station dress in the game, 2026-11-17 (#1593)

In-engine captures and the scratch tools for #1593's PR. Every still is the flight camera rendered at 1280x720 with 4x MSAA on High Fidelity, in the Preview Host's Combat playtest on the Bastion trial sector (the third row, which places two repair docks, two mod stations and an armory).

| File | What it is |
|---|---|
| `fd7-vs-engine-copper-open.png`, `fd7-vs-engine-verdant-open.png`, `fd7-vs-engine-copper-dark.png` | fd7's tiles above the in-engine tiles, per station and dress, at size 25. The poses differ: fd7 drew each station from one Blender view, and the game tilts each station by its placed point. |
| `dress-<locale>-<state>-z<size>.png` | Every station in every dress (Shoal, Veil, Bastion red hull, Bastion bone hull), open, in use and dark, at sizes 15 and 25, on Copper Turquoise and Verdant Giant. |
| `sector-bastion-copper-<kind>.png` | The first station of each kind as the Bastion sector dressed it on its own, before any redressing, with the HUD. |
| `stills-log.txt` | Where each station was placed and which palette it rolled. |
| `calibration-log.txt`, `calibration-swatch-round5.png` | The light calibration through the game's post: inputs and measured screen colour per round. Round 5 is the one shipped. |
| `station-dress-BuildDressedStations.cs` | Editor script, extending `gate4d-BuildOutpostStations.cs`: dressed materials, imports and model prefabs, `StationModel` on the plain models, each body's model reference, the faction dress, the service lights. Run in a batch editor with `station-dress-dress_batch.ps1`. |
| `station-dress-dress_pipeline.sh` | Per station kind: copy the builder's dressed `.blend` into `art/stations/<station>/<faction>/`, then `station_uv`, `station_paint --hatch`, and `gate4d-export_station.py`. |
| `station-dress-DressCalibration.cs`, `station-dress-DressStills.cs` | Capture scenarios for `CaptureScenarioPlayModeTests -WithGraphics`. |
| `station-dress-dress_sheet.py`, `station-dress-proof_sheet.py` | Sheet composers. |
| `station-dress-compare_blends.py` | Checks the builder against the shipped `.blend` files: mesh names, vertex counts and bounds. |

## How the stills were made

- "In use" is the ship parked at the station's centre, in its berth. The in-use flare was left out on #1590, so a station in use has no light change of its own. The fly-in zones were disarmed for the captures so the mod station and armory didn't open their picks.
- "Dark" switches the station's `Lit` off, which is what the hunt's release does.
- Each dress is put on by the same calls `FindStation.Setup` makes, on the placed stations: the body is switched off, the model swapped, the palette worn, the body switched on.
- The station builder is `fd3-stations.py` at `15af122` and the exporter is `gate4d-export_station.py` at `9823b48`, both on `evidence/outpost-kit`.
