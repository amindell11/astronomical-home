# Hangar fps probe (evidence for the hangar-fps tracker issue)

Scratch measurement harness, never merged. Built from main `0a8d3dd63` (#1020 merged) in pool worktree slot agent-5.

- `HangarPerfProbe.cs` -> drop under `Assets/` (Assembly-CSharp). In a player it starts when launched with
  `-hangarPerfProbe <out.jsonl> -probeRes 1920x1080,3840x2160 -probeQuality 0,1,2 -probeFrames 150`;
  it waits for the hangar, then per resolution x quality level toggles each piece and records
  FrameTimingManager medians plus per-sampler GPU Recorder times, then quits.
- `HangarPerfProbeBuild.cs` -> drop under an `Editor/` folder; `probe_build.ps1` runs it through
  `unity_access.ps1 -Action RunBatch` (Development, StandaloneWindows64, Mono).
- `probe_full.jsonl` is the full run; `probe_val.jsonl` the first 1080p Performant validation pass;
  `probe_full_table.txt` is `tabulate.py probe_full.jsonl`.

Machine: RTX 2000 Ada Laptop GPU (D3D11), Core Ultra 7 155H, Unity 6000.1.8f1, desktop 3840x2160.
1080p ran windowed, 4K ran FullScreenWindow. vsync forced to 0 for every arm except `base_tierVsync`.

## Flight follow-up (2026-10-19)

Built from main `4703faec1` in slot agent-5 with the same `HangarPerfProbeBuild.cs` / `probe_build.ps1`.

- `FlightPerfProbe.cs` -> drop under `Assets/`. Launched with `-flightPerfProbe <out.jsonl> -probeLocale <Locale_N>`
  (`run_probe.ps1` passes the rest: `-probeRes 1920x1080,3840x2160 -probeQuality 0,2 -probeFrames 150 -probeSettle 8`).
  It waits for the hangar, overwrites the sector config's locale scene name in memory (both shipped configs say
  Locale_3), invokes HangarScreen's launch button, waits for the sector to load and the splash to hide, makes the
  player invulnerable, lets the fight run 8 s, then near-freezes the sim (`Time.timeScale` 0.001) so every arm and
  every resolution x quality matrix renders the same frame. After the frozen matrices it unfreezes and records
  `live_base` / `live_no_sky` / `live_base_again` per cell as a check on the frozen numbers.
- `flight_Locale_1.jsonl`, `flight_Locale_3.jsonl` are the runs; `flight_Locale_N.png` is the frozen frame (1080p).
- `flight_table.txt` is `summarize.py` over both runs; `flight_full_table.txt` is `tabulate.py` per run.
- Arms whose GPU frame is shorter than the CPU frame (`no_sky`, light `only_sky:*`, the floor) are not GPU-bound and
  wander up to 2x with the laptop GPU's clock state; the leave-one-out arms stay GPU-bound and repeat within 0.1 ms.
