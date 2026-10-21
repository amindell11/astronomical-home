# Sector jump motion experiment

This is evidence for issue #1099, captured on 2026-10-21. It tests the owner's proposal to move the real ship and camera together, letting the existing locale and motion blur produce travel. It is not the final sector-transition implementation.

## Result

The ship turns, then travels 20,000 units right and 20,000 units down over 75 recorded frames, with the camera held at the same offset. Gameplay time is paused. The existing sky produces the diagonal streaks without sky-shader changes.

The first visual comparison blurred the ship away. DrawnSurface and DrawnCanopy had no MotionVectors pass. Adding the standard URP object-motion pass to both makes the ship remain visible and substantially sharp at the same peak-speed frame. No second camera, ship copy or custom blur was added.

The probe asserts the ship's screen center within 0.001 viewport units on every travel frame, preserved hull, and the final heading. The graphics test passed, 1/1. Shader sharpness was checked from the recorded frames, not inferred from those assertions.

A center-region red-paint count corroborates the visual comparison. Without object vectors it goes from 449 before travel to 2 at peak and 426 after stopping. With object vectors it is 443, 351 and 424. These counts detect the missing ship; they are not a general sharpness metric.

## Files

- [Clip](ship-and-camera.mp4) and [GIF](ship-and-camera.gif).
- [Peak frame with the object-motion pass](after-peak.png).
- [Peak frame without the pass](before-peak.png) and [baseline clip](missing-motion-vectors.mp4).
- `SectorJumpMotionProbe.cs` contains the complete scratch scenario.
- `sector-jump-motion-vectors.patch` contains the two experimental shader passes.
- `probe-overlay-accessor.patch` supplies the small HUD visibility accessor used by the scenario.
- `capture-summary.json` records the passing run.

## Reproduce

Use a pooled worktree with the current InitScene, player rig and locale content. Apply the two patches, place the scenario under `scratch/capture/`, and run the shared coordinator through the pool script:

```sh
bash scripts/agent_worktree_pool.sh run-tests agent-8 -Mode PlayMode -TestFilter Tests.PlayMode.CaptureScenarioPlayModeTests -WithGraphics -Windowed -CaptureScenario SectorJumpMotionProbe
```

The tested worktree held local commit b3fa2ecb0 and the working files of dependency PR #1065, head e6364485d8b164b2c8babd3e9af68b58528b4f22. GameHost used the recap version. The experiment starts after the first sector loads and controls the shot itself.

## Limits and follow-up

- The arrival is a deliberate raw snap back inside the same locale. The sky visibly changes position. This does not prove a destination crossfade or continuous loading.
- The two experimental motion-vector passes do not respect the existing debris-visibility clipping. A production fix must match that coverage before it ships.
- The black contour around the ship is a separate pass. The intact-ship capture establishes visibility, not pixel-perfect preservation of every outline or transparent effect.
- The normal overlay is hidden, but the ship's world-space health bars and FMOD debug panel remain in this diagnostic capture.
- The camera zoom and turn direction are fixed for this repeatable experiment. Random direction, hangar/Continue integration, locale crossfade and loading remain on #1099.
- Read-only quality review found no blocker for this intact-ship experiment. It identified the debris clipping and same-locale arrival limits above. Scratch setup lookups must not be copied into runtime code.

The initial probe also caught deferred Rigidbody-to-transform synchronization while physics was paused. Its final Place operation writes the visible transform and Rigidbody together. It never uses RespawnShip for animation.
