# Second slipstream checkpoint, 2026-11-17

The owner authorized the three-step plan and requested the next game-size still and clip, followed by a stop for review.

## Diagnosis

The unchanged KeystoneTrailScenario reproduced the repeated tooth profile on the first ribbon prototype. Replacing the atlas with a solid white texture on the same mesh produced smooth silhouettes at the same flight frame. The diagnostic files are review2-before.png and review2-solid-probe.png.

The mesh tapered its support width while the atlas also supplied an interior tapered contour. That combination warped the texture across each triangle pair. The rung 1 fix removes the support-mesh taper, leaving the atlas to define the contour. This reduces the large repeated teeth without changing the recorded flight path.

Bulwark's right bow sheet also used cell 17, a tiny left-facing shape. The right-facing counterpart to left cell 16 is cell 21. The authored correction uses that pair and increases the front-sheet widths.

## Visual result and remaining uncertainty

The sheets have more visible fill, and Bulwark now has front flow on both sides. The four body sheets remain connected to fixed hull points and follow the travelled path. The material gradient is weaker, so it no longer suppresses the fill as strongly alongside the existing age fade.

This pass still falls short of the rich overlapping sheets in the selected C frames. Vanguard retains fine edge rippling. The still does not establish whether that remainder comes from curved mesh interpolation or the shader's screen-space hatch. The smallest later diagnostic would compare the same paused frame with hatch disabled. No further tuning was performed after this capture.

This is a visual checkpoint awaiting the owner's judgment. It is not acceptance of the look or full combat validation.

## Evidence identity

- Source: 846a7f1807fe1784f35cfba231e1fdfa0c77e1da on task/keystone-trails-dash.
- Baseline and solid diagnostic: bb7f319a0c84bd5d86f5ba74e2fe8b853fd0e9d9, with the diagnostic changing materials only in editor memory and restoring them afterward.
- Capture: KeystoneTrailScenario in TuningSector, native Game View, 1920 by 1080, Performant quality.
- Framing: camera minimum half-height 22, about 70 pixels per Vanguard hull. First activations coincide for the still; second activations retain separate timings through turns.
- review2-game-size.png is the unmodified native frame 75. review2-bulwark.png is unmodified frame 365.
- review2-game-size.mp4 keeps native resolution. The simulation advances 20 milliseconds per recorded frame, so playback is 50 frames per second, excluding capture wall-clock overhead. There are 427 frames, about 8.54 seconds.
- review2-preview.gif is a half-resolution preview at 25 frames per second, with the same simulation speed.
- review2-provenance.json, review2-frames.csv and review2-test-summary.json preserve capture identity, timing and the routed test result.
- review2-setup.cs preserves the editor-authored material and prefab tuning.

## Verification and cleanup

The compile check passed all eight assemblies. Each of the baseline, solid diagnostic and final capture runs passed CaptureScenarioPlayModeTests, one test per run. The encoder validated frame identity and decoded frame count. The GIF encoder checked playback duration and subject colour.

The capture lane was released, editor settings restored, and the coordinated editor closed. Coordinator status has no owners, boot lease, queue entries or blockers. The pre-existing QualitySettings edit remains outside the commit. No PR submission or merge.
