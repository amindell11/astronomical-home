# What changed after the GIF feedback?

The owner reported strange ship colors and blur around the ship, then clarified that the blur might be acceptable. The requested change was to fade the health bar with the GUI. This capture preserves the original ship/camera movement and blur, and adds a shared fade for the normal HUD and ship-space status bars.

`hud-fade.mp4` is the full-resolution color reference. The PNGs show the HUD visible, fading, hidden during travel, and restored. `hud-fade.gif` uses a maximum-coverage palette to retain the small ship's red paint; it is still a reduced-resolution preview.

# Why did the first GIF change the ship colors?

The raw peak PNG has red ship paint. The matching decoded frame of the original GIF turns much of that paint blue-grey. These are `original-raw-peak.png` and `original-gif-decoded-peak.png`.

The capture assembler's default export quantizes each frame to 128 colors with Pillow's median-cut method. Re-encoding the same frame with 256 colors and that method still loses the ship's red paint. For the original peak frame, mean absolute RGB error over red paint pixels was 48.01 at 128 colors, 46.08 at 256 colors, and 2.92 with a 256-color maximum-coverage palette. The pixel selection used the resized original, at x=220..259 and y=120..149, with R > 1.3G, R > 1.1B and R > 90. This is a color comparison, not a motion-blur quality measure.

The faint red-and-white trail exists in the original PNG too. The owner said it may be acceptable. No renderer or motion-blur policy change is included in this revision. A proposed separate ship-rendering comparison was withdrawn before it ran.

# What proves the fade?

The first attempt directly animated graphic color alpha. The capture failed because live HUD widgets write their own colors during Update or LateUpdate, restoring their opacity. `direct-color-fade-failed-summary.json` records that failure.

The correction uses CanvasGroup opacity on the root HUD and ship canvases. This is fix-ladder rung 1: widget color updates cannot overwrite parent opacity. Nested canvases inherit one fade. The probe preserves previous alpha and destroys only groups it added.

`capture-summary.json` records the passing capture, 1/1. Assertions require actual ship status bars under the fade groups, zero group opacity throughout travel, centered ship position, unchanged hull and matching heading. Frames 20, 37, 100 and 232 were inspected for visible, partial, absent and restored HUD/status bars. A separate attempt was interrupted before the scenario by Unity's package sign-in error; its summary is included. The unchanged retry passed.

A read-only quality review found the direct-color conflict before the failing run finished and reviewed the final CanvasGroup correction without further findings. The source is a scratch capture, not runtime integration.

# How is it reproduced?

Start from the same feature/recap working tree described in `../motion-probe/README.md`, including its experimental shader patches. Stage `SectorJumpMotionProbe.cs` in the slot's `scratch/capture/` directory and use the pool runner:

```powershell
./scripts/agent_worktree_pool.sh run-tests agent-8 -Mode PlayMode -TestFilter Tests.PlayMode.CaptureScenarioPlayModeTests -WithGraphics -Windowed -CaptureScenario SectorJumpMotionProbe
python scripts/capture/assemble.py <frame-directory> --fps 50 --crf 16 --keep-frames
```

The GIF uses every second PNG, resized to 480x270 with Lanczos, then `quantize(colors=256, method=Image.Quantize.MAXCOVERAGE)`. Frames last 40 ms. Decode the written GIF to verify total duration and inspect its peak frame.

# What remains open?

This is the visual experiment for issue #1099. Launch/Continue integration, destination-locale crossfade, continuous arrival, random direction, and production shader coverage are still pending. The same-locale return snap remains deliberately visible. The FMOD debug panel remains visible in the recording. Nothing was merged.
