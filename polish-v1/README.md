# What is this comparison for?

The owner asked for creative polish while preserving the previous version as a baseline. The baseline remains at commit `475829a379b342b293bc3d2f83ffa42ec226743e`, in `hud-fade/`. This candidate changes only the scratch visual experiment for issue #1099.

The aim is to give the jump a deliberate launch and a readable finish. HUD fading overlaps the turn, exhaust builds before departure, the camera pulls back during acceleration, and travel eases into a settled arrival. Exposure and vignette change slightly with speed. The existing ship blur remains, with higher sample quality. Recording ends before restoring the diagnostic starting position, so the candidate does not show the baseline's reset snap.

# What did the captures establish?

`capture-summary.json` records the final passing capture, 1/1. Assertions check actual ship status bars under the shared fade, zero HUD opacity throughout travel, centered ship position, unchanged hull, matching heading, the travel endpoint, and live particles from every nozzle throughout cruise. Raw frames were inspected during preparation, cruise, deceleration and settled arrival. The FMOD debug panel is absent in the final capture.

The first pass was test-green but visually incomplete. Shortening the authored particle lifetime without increasing emission left the exhaust almost empty. The final probe adjusts the rate alongside lifetime and asserts sustained particles. `first-pass-cruise.png` and `first-pass-summary.json` preserve that observation. The first lookup also omitted FMOD's hidden debug helper. The final lookup includes hidden objects and asserts that it found the helper before disabling its drawing component. This is an earliest deterministic setup failure, fix-ladder rung 2. It restores the helper afterward without disabling the audio manager.

A read-only quality agent identified the exhaust-rate mismatch, checked the corrected deltas, and found no further material issues. The motion curve integrates continuously to the endpoint. The clip preserves the current locale and existing ship materials.

`polish-v1.mp4` is the full-resolution color reference. The GIF is a reduced preview using the maximum-coverage palette correction from the baseline investigation. Its decoded cruise frame is included so preview colors can be compared with the raw PNG. No sound was added.

# How can it be reproduced?

Use the same feature/recap working tree and experimental shader passes documented in `../motion-probe/README.md`. Copy `SectorJumpPolishProbe.cs` into the slot's `scratch/capture/`, then run:

```powershell
./scripts/agent_worktree_pool.sh run-tests agent-8 -Mode PlayMode -TestFilter Tests.PlayMode.CaptureScenarioPlayModeTests -WithGraphics -Windowed -CaptureScenario SectorJumpPolishProbe
python scripts/capture/assemble.py <frame-directory> --fps 50 --crf 16 --keep-frames
python encode_jump_preview.py <frame-directory>
```

# What remains outside this experiment?

This films one real ship and camera travelling through the current locale. It is not wired into hangar launch or sector Continue, does not load or crossfade a destination locale, and does not resume gameplay at a destination. Production transition integration and shader coverage remain open on #1099. Nothing was merged.
