# Perf pass 2026-11-06: main e9e139b7b vs baseline 21d2b346d (#1049)

Never merged. Scratch harness and data for the perf pass written up on #267.

- Builds: Development, StandaloneWindows64, D3D11, built with `PerfPassProbeEditor.Build` (`batch.ps1` through
  `unity_access.ps1 -Action RunBatch`). Main in slot agent-2, baseline in slot agent-3.
- `PerfPassProbe.cs` -> drop under `Assets/` (Assembly-CSharp). Launched with `-perfPassProbe <out.jsonl>` (`run_probe.ps1`).
  High Fidelity, vsync off, no frame cap. 1080p windowed, 4K FullScreenWindow. Sequence:
  hangar live -> hangar arms -> launch (load pacing) -> 6 s arrival -> settle -> flight live at 1080p and 4K ->
  flight with the native profiler writing `.raw` -> frozen (`timeScale` 0.001) GPU arms: sky layers, cameras,
  renderer features, UI, post.
  Live rows: per-frame `unscaledDeltaTime` (p50/p95/p99/max, counts over 16.7/25/33/50 ms), FrameTimingManager main/render/GPU,
  `GC Allocated In Frame`, worst 12 frames, entity census before/after.
- Runs: `main_1`/`base_1` full (30 s live); then interleaved `base_2, main_2, base_3, main_3` (60 s live, frozen arms skipped).
  `summary.txt` is `summarize.py` over all six. `frame_intervals/` holds every live frame interval.
- `main_1_profile_report.txt`: `PerfPassProbeEditor.Analyze` over `main_1_profile.raw` (615 MB, kept local, not pushed).
  The editor loads only the last 300 frames of a capture.
- Machine: RTX 2000 Ada Laptop GPU, Core Ultra 7 155H, 3840x2160 @120 Hz desktop. NOT quiet: the owner's editor
  was open on the primary tree and 2-3 other sessions' Unity processes ran throughout, so CPU-tail numbers swing
  run to run (compare base_2 with base_3). GPU medians and the frozen arms repeat within ~0.5 ms.
- All shipped sectors fly Locale_3; the probe does not force a locale and does not reach a sector jump.
