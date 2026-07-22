---
name: game-capture
description: Record footage of any game situation with per-investigation diagnostic markup (lines, vectors, rings, labels) and hand the user a clip. Use when footage is the deliverable — showing a behavior, visually diagnosing a sim bug, demoing a feature, or recording RL episodes.
metadata:
  project: astronomical-home
---

# Game Capture

Record PNG frame dumps of a game situation with an immediate-mode diagnostic overlay,
then assemble them into mp4/gif. **The footage is the deliverable** — always end by
reading a mid-clip PNG yourself and handing the user the clip path.

## Pick a lane

- **Ad-hoc probe** → author a scratch scenario in repo-root `scratch/capture/` (create
  the dir if needed; it's gitignored and outside Assets, so Unity never compiles it at
  rest). Delete the file when the investigation is done.
- **Repeatable scenario** → promote: move the file to
  `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/Scenarios/`, commit with its
  generated `.meta`. Sample/living doc: `TwoShipSkirmishScenario.cs`.
- **RL episodes** → drop `record.flag` in `<slot>/results/rl-episodes/` (empty = 3
  episodes, all recorded; or JSON
  `{ "runSeed": 7, "episodes": [0], "captureEveryFixedSteps": 5, "width": 960, "height": 540 }` —
  unknown keys fail loudly). Run the `RLEpisodePlayModeTests` filter with
  `-WithGraphics`. **Delete the flag after use** — a forgotten flag turns every later
  RL run into a recording session. This lane films the built-in characterization
  opponent only — for a trained checkpoint, see "Film a trained checkpoint" below.

## Author a scenario

`scratch/capture/MyProbe.cs` — file name = class name, public parameterless ctor:

```csharp
#if UNITY_EDITOR
using System.Collections;
using Game.Capture;
using Tests.PlayMode.Common;
using UnityEngine;

public sealed class MyProbe : CaptureScenario
{
    public override IEnumerator Run(CaptureRecorder recorder)
    {
        var (a, _) = SpawnUtilityShip(new Vector2(-12f, 0f), rotDeg: -90f, team: 0);
        var (b, _) = SpawnUtilityShip(new Vector2(12f, 0f), rotDeg: 90f, team: 1);

        var subjects = new Vector2[2];                    // reuse — never allocate per step
        for (var i = 0; i < 400 && a && b; i++)
        {
            yield return new WaitForFixedUpdate();
            subjects[0] = a.Kinematics.pos;
            subjects[1] = b.Kinematics.pos;
            recorder.Step(subjects, ctx =>
            {
                ShipDiagnosticsOverlay.Draw(ctx, a, b);   // standard two-ship markup
                ctx.Label(subjects[0], $"step {i}", Color.white);  // plus whatever this investigation needs
            });
        }
    }
}
#endif
```

`CaptureDraw` gives you `Line / Vector / Ring / Trail / Label` in plane-space plus a
`LineWidth` knob. Anything not redrawn in a captured step disappears — compose the
overlay fresh per frame. Override `Config` to change clip name/size/cadence.

## Film a trained checkpoint (policy vs archetype)

`record.flag` can't select a policy or opponent, so author a scratch scenario
mirroring `CheckpointEvaluator.Run`'s composition: copy the `.onnx` to
`Assets/Tests/Fixtures/EvalCandidate.onnx` (models load via AssetDatabase — an
absolute file path won't), then `EpisodePair.SpawnWithAgentChooser` →
`OpponentRoster` (pinned `Install` per archetype) →
`ShipAgentFactory.ComposeInferenceOnly` → `EpisodeLoopDriver`, pumping the episode
enumerator and calling `recorder.Step` per fixed step. Gotcha: `Session.Services`
exposes interfaces but the spawn seams take concretes — cast
`(UnitService)Session.Services.UnitService`, or it's a boot-cycle-wasting CS1503.
Delete the staged `.onnx` + `.meta` with the scratch file — a leftover under
`Assets/` re-imports on every editor boot.

## Run + assemble (one command each)

```powershell
./scripts/agent_worktree_pool.sh run-tests agent-N -Mode PlayMode -TestFilter Tests.PlayMode.CaptureScenarioPlayModeTests -WithGraphics -CaptureScenario MyProbe
python scripts/capture/assemble.py <slot-path>/results/capture/frames/<stamp>-MyProbe
```

The runner prints the absolute frame dir. `assemble.py` defaults fps/dims from the
frame dir's `manifest.json`; `--step N` drops to every Nth frame. `suggestedFps`
replays real time — pass `--fps` at 3–4× for a watchable multi-episode clip. mp4
needs imageio-ffmpeg, and the venvs here are uv-managed with no pip module:
`uv pip install --python <venv-python> imageio-ffmpeg` (once per venv/worktree).

## Deliver

- **Report the slot's absolute output path** — `results/` is worktree-local, so
  `results/capture/...` in agent-N is NOT the primary tree's `results/`.
- For a remote/chat user a path is not a deliverable, and mp4 has failed to render
  there both as a file attachment and as an artifact data-URI `<video>`. Proven:
  `--web` mp4 (≤5 MB) via SendUserFile, or `--format gif --scale 0.4 --step 2`
  embedded as an `<img>` data URI in an artifact.
- Note the delivered clip's absolute path in the ledger row / topic file — the next
  session otherwise greps every worktree hunting for it.

## Hard-won constraints (violate = silent garbage)

- **Gizmos/Handles never render into offscreen captures.** Only `CaptureDraw`
  primitives show up — they're real renderers (LineRenderers); URP
  `RenderPipelineManager` GL hooks are unreliable for manual `camera.Render()`.
- **Batch runs are `-nographics` by default; `-WithGraphics` is for filtered runs
  only** (it requires PlayMode + an explicit `-TestFilter`, and fails on zero tests
  executed). Never the merge-gate suite — never record merge-gate proof from a
  graphics run.
- **Never call `Gunsight.Evaluate()` from an overlay** — it mutates the firing path's
  LOS cache (observer effect on the sim). `InEnvelope()` only.
- **Recorded runs lock frame pacing** (`Time.timeScale=1` +
  `Time.captureDeltaTime=Time.fixedDeltaTime`) so a recorded seed replays
  identically; `watch.flag` wins if both flags exist.
- **Aim visuals use the public `Gunner.AimPoint(...)` static** — the same lead the
  AI uses. RLHarness has no internals access to GameCore; `AssemblyInfo.cs` is the
  unlock if ever needed.
- **Eyeball a mid-clip PNG (Read the file) before claiming success** — compile-green
  says nothing about render output; v1's overlay failed only at render time. For label
  checks, confirm the label *changes* across frames.
- Scratch scenarios are staged into `Tests/PlayMode/Scratch/` only for the run and
  auto-removed; if a run died hard, the next run sweeps leftovers. Don't put files
  there yourself.
- Capture asserts loudly on empty/NaN subjects and a reused frame dir — fix the
  scenario, don't catch the exception.
- **Never call `recorder.Step` on the spawn frame** — capture only after the first
  `WaitForFixedUpdate` yield, or URP dies in `ForwardLights` ("Render Graph
  Execution error").
- **Never run a capture/eval play session while an ML-Agents trainer is attached on
  this machine** — every Academy play mode touches the trainer's port-5004 handshake
  and can sever the live training connection (killed the 2026-07-20 run mid-flight
  at 534k). Record from checkpoints after the run ends. If it happens anyway: the
  trainer survives — kill the orphaned editor and relaunch with `--resume`; zero
  steps lost.
