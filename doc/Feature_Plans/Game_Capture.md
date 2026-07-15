# Game Capture — agent-facing game inspection & recording

**Date:** 2026-07-15
**Status:** Built with this PR. Supersedes PR #151 (`task/rl-episode-recorder`, closed
unmerged): v1 proved the render mechanics for RL episodes only; this generalizes it into
a tool for inspecting *any* game state, with RL episode recording rebuilt as its first
client.

> **One-line intent.** A general tool for agents to inspect any game situation, record
> footage with per-investigation diagnostic markup, and communicate with the user
> through that footage — the footage IS the deliverable.

---

## Architecture

Everything lives in `Assets/Scripts/Capture/` under asmdef **`Game.Capture.Editor`**
(mirrors `Game.RLHarness.Editor`: platforms `[Editor, WindowsStandalone64]`,
`defineConstraints [UNITY_INCLUDE_TESTS]`, `autoReferenced false`, references
`["Game.Core"]`). Namespace `Game.Capture`. The core is NUnit-free and Ships-agnostic —
it frames plane-space `Vector2` subjects and knows nothing about what they are. Note
honestly: the WindowsStandalone64 platform envelope is inherited convention from the
RLHarness asmdef, not exercised by `-testPlatform PlayMode` runs.

- **`CaptureRecorder`** (`CaptureConfig` in the same file) — owns an overhead
  orthographic camera rig + directional light, a 960x540 (configurable, even dims —
  yuv420p) RenderTexture, and the PNG writer. `Step(subjects, draw)` is called once per
  fixed step; every `everyFixedSteps` (default 5 → 0.1 s sim per frame → real-time
  playback at 10 fps) it validates subjects (empty/NaN → loud exception, never a silent
  black frame), auto-frames them
  (`halfHeight = max(yExtent + padding, (xExtent + padding) · h/w, minHalfHeight)`),
  runs the draw callback, renders, and writes `f_%05d.png`. Renders via
  `RenderPipeline.SubmitRenderRequest` + `StandardRequest` when supported (URP 17.1's
  supported out-of-loop camera render), falling back to `camera.Render()` (proven by v1
  footage); `RenderTexture.active` is restored in `try/finally` around every readback.
  The constructor validates the whole config and refuses a frame dir that already has
  frames. A `manifest.json` (dims, fixedDeltaTime, everyFixedSteps, suggestedFps) is
  written next to the frames so the assembler never assumes a fixed dt. All capture
  GameObjects carry a `[Capture]` name prefix; `CaptureRecorder.SweepStranded()` lets
  teardown reclaim anything a timeout stranded.

- **`CaptureDraw`** — immediate-mode overlay the agent composes per investigation:
  `Line/Vector/Ring/Trail/Label` in GamePlane plane-space, lifted +3 above the plane,
  labels billboarded to the rig camera. Sealed concrete class (no interface — no second
  implementation exists). Pooled LineRenderers/TextMeshes because **editor
  Gizmos/Handles never render into offscreen camera renders** (hard-won v1 lesson).
  Anything not redrawn in a captured step is invisible. Overlay renderers keep
  `forceRenderingOff = true` except inside the capture submit, so diagnostics never
  appear in the main camera or scene view. The line material is `ZTest Always`, no
  depth writes — diagnostics must not be occluded. Per-step sequence: reset pool
  cursor → callback in try/catch (throw → disable ALL primitives, rethrow) → disable
  primitives past the cursor → render.

- **`CapturePacing.Locked()`** — IDisposable scope: `timeScale = 1` +
  `captureDeltaTime = fixedDeltaTime` (1 fixed step per rendered frame), restored on
  dispose. Deterministic clips, real-time playback regardless of wall-clock speed.

## Lanes

1. **Scratch scenario (ad hoc).** Author `<Name>.cs` (a `CaptureScenario` subclass) in
   repo-root **`scratch/capture/`** — gitignored and *outside Assets*, so Unity never
   compiles it at rest. `unity_test_agent.ps1 -CaptureScenario <Name>` stages the file
   into the gitignored `Tests/PlayMode/Scratch/` dir for the run and removes it in
   `finally`, with a belt-and-braces sweep of `Scratch/*.cs` at the start of every run:
   the pool's slot-prepare uses `git clean -fd`, which **preserves ignored files**, so a
   stranded staged file would survive lease turnover and invisibly break the next
   agent's compile.
2. **Committed scenario (repeatable).** Promote by moving the file to
   `Tests/PlayMode/Scenarios/` and committing it (with its generated `.meta`).
   `TwoShipSkirmishScenario` is the one committed sample — runner/render smoke and the
   living doc for authoring; the sample library deliberately stays at one.
3. **RL episode recording** (first client) — see below.

The generic runner `CaptureScenarioPlayModeTests` (`[Camera] + [RequiresGraphics]`,
extends `PlayModeWorldFixture`) resolves the scenario type named by the
`-captureScenario` command-line arg (read via `Environment.GetCommandLineArgs()` — an
explicit per-run arg, not an ambient env var): full-name match first, then unique short
name; zero or ambiguous matches fail listing candidates; a public parameterless ctor is
required. It runs under `CapturePacing.Locked()`, asserts `FrameCount > 0`, and prints
the absolute frame dir. Without the arg it `Assert.Ignore`s, so the default suite stays
green.

### Fail-closed `-WithGraphics`

Graphics runs drop `-nographics` (rendering needs a device) and are deliberately
narrow: `-WithGraphics` requires `-Mode PlayMode` **and** an explicit `-TestFilter`,
clears the default `RequiresGraphics` exclusion only when the caller didn't set one,
and **fails when zero tests executed** (the normal aggregation treats a 0-run as
green — a filter typo would otherwise pass silently having rendered nothing). Never
run the merge-gate suite with graphics.

### One-command pipeline

```powershell
./scripts/agent_worktree_pool.sh run-tests agent-N -Mode PlayMode -TestFilter Tests.PlayMode.CaptureScenarioPlayModeTests -WithGraphics -CaptureScenario MyProbe
python scripts/capture/assemble.py <slot>/results/capture/frames/<stamp>-MyProbe
```

`assemble.py` reads the manifest for fps/dims; `--format mp4` (default, imageio-ffmpeg
wheel bundles ffmpeg: `libx264 -pix_fmt yuv420p -movflags +faststart`) or `gif`
(Pillow); `--fps --scale --step --colors --crf`. Output layout:
`<outputRoot>/frames/<runStamp>-<clipName>/{manifest.json, f_00000.png, …}` with the
assembled clip written beside the frame dir.

## RL episode recording (first client)

`RLEpisodePlayModeTests.Characterization_WritesJsonl` accepts `record.flag` beside
`watch.flag` in `results/rl-episodes/`:

```json
{ "runSeed": 7, "episodes": [0, 2], "captureEveryFixedSteps": 5, "width": 960, "height": 540 }
```

- **Empty file = v1 defaults** (bare `touch record.flag` still works: 3 episodes, all
  recorded, 960x540 @ every 5 steps).
- `JsonUtility.FromJsonOverwrite` ignores unknown keys — exactly the stale-config bug
  class — so top-level keys are whitelisted before parsing and any unknown key fails
  loudly, as do malformed JSON and duplicate/negative episode indices. An episode index
  beyond the run count extends the count so requested footage is never silently missing.
- `runSeed` is applied to the spec **before** `SpawnPair` (decision seeds derive from
  it). `watch.flag` wins over record: watch keeps real-time pacing, record locks pacing.
- Output is v1-identical: `results/rl-episodes/frames/<stamp>-epNN/`.
- Recorded runs scale the wall-clock deadline (synchronous render/readback/PNG eats the
  `Time.realtimeSinceStartup` budget; sim-step termination still bounds the episode).
- The `-nographics` stats lane is untouched: the fixture carries no `RequiresGraphics`
  category and `RL_EPISODES`, `RL_EPISODE_COUNT`, `RL_WATCH`, `RL_EPISODE_TRACE`
  survive.

**Determinism caveat:** a recorded run is deterministic *within itself* (locked pacing +
seeded spec). Do **not** claim trajectory-identity with the uncaptured stats lane —
`captureDeltaTime` changes frame scheduling around the same fixed steps.

## FireRange seam

The only `Game.Core` change, and inert: `WeaponComponent.FireRange` (`virtual`, 0)
overridden by the four distance-gated weapons (`Lasers`, `ChargeLasers`, `Railguns`,
`Rippers`) to expose their serialized `fireDistance`; `Missiles`/`Grenades` inherit 0 →
no ring, correct. Read via `ship.Weapons.Primary`. This kills v1's `SerializedObject`
read of a private field and its unguarded `using UnityEditor;` (a latent
WindowsStandalone64 compile break). `IWeaponContext` was deliberately NOT extended — it
ripples into test fakes. Pinned by `WeaponFireRangeEditModeTests`.

## Overlay rules carried from v1 (hard-won)

- Aim line = `Gunner.AimPoint(in Kinematics, targetPos, targetVel, projectileSpeed)` —
  the exact lead the AI uses — colored by `Gunsight.InEnvelope()`. **Never
  `Gunsight.Evaluate()`**: it mutates the firing path's LOS cache (observer effect on
  the sim). LOS line via `TargetingMath.IsLineClear`.
- `ShipDiagnosticsOverlay.Draw(ctx, a, b)` is the reusable two-ship diagnostic set:
  velocity vectors, speed labels, aim/envelope lines, FireRange rings, LOS, projectile
  trails.
