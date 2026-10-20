# Cloud-bank tiles (#1048): stills, measurements, scratch scripts

Never merged. Built from the PR branch (`task/hangar-fps-tiles`, commit `0c24a247a` minus a no-behaviour parameter cleanup) in pool worktree slot agent-2.

- `sheet_1080p.png` / `sheet_4k_crop.png`: before (live clouds, main `39eb522ef`) vs baked tile at half (4096², shipping) and quarter (2048²) density. Sky-only ortho-7 camera, High Fidelity, captured by `SkyStills.cs` (batch editor, `-executeMethod SkyStills.Capture`; `AuthorTiles` added the components to the scenes).
- `tiles_hangar.jsonl` / `tiles_hangar_table.txt`: `HangarPerfProbe` on the tile build (the run was closed externally partway through the 4K High Fidelity block; base and FarNebula leave-one-out arms landed).
- `tiles_flight_Locale_1.jsonl`: `FlightPerfProbe` Locale_1 on the tile build; the matching baseline is `flight_Locale_1.jsonl` on `evidence/hangar-fps`.
- `cloudbank_stats.py` (+ output): numpy port of the cloud-bank formula comparing the live 2D noise model over a wide area with the 4D-torus tile model across seeds (Locale_3 material values). Shows the per-tile layout variance is around the live model's mean, not a bias.
- `unity_batch.ps1` / `unity_runtests.ps1`: launchers run through `unity_access.ps1 -Action RunBatch` (the render tests are EditMode `RequiresGraphics`, which the test agent cannot run).
