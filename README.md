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
