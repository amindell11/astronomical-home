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
- **RL episodes / trained checkpoints** → the **harness capture lane** (env-configured,
  not a test flag). Set `RL_HARNESS_LANE=capture` + `RL_HARNESS_RECORD=all` (or
  comma indices), one seed (`RL_HARNESS_SEEDS`), and one opponent block
  (`RL_HARNESS_OPPONENT=aggressor|mirror|<ckpt>.onnx` — `roster` is refused: five
  archetype films are five sessions). `RL_HARNESS_ONNX` names the candidate
  checkpoint (default: the smoke fixture); `RL_HARNESS_PAINTERS` picks the markup
  (default `ship-diagnostics`; add `policy` for the facing fan). Full `RL_HARNESS_*`
  grammar in `training/rl/README.md`. The session
  needs a graphics device, so it runs **without** `-nographics` — never the merge
  gate. Clips land beside their JSONL under `RL_HARNESS_OUT_DIR` (or
  `results/rl-capture/`). A one-command `eval_lane.py` capture preset is a later
  leaf; for now set the env and run the harness batch through the coordinator.

## Author a scenario

`scratch/capture/MyProbe.cs` — file name = class name, public parameterless ctor:

```csharp
#if UNITY_EDITOR
using System.Collections;
using Game.Capture;
using Game.Diagnostics;
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
                ShipDiagnosticsPainter.Draw(ctx, a, b, Session.Services.Projectiles);  // standard two-ship markup
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

The harness capture lane (above) covers the standard case: `RL_HARNESS_LANE=capture`
with `RL_HARNESS_ONNX=<ckpt>.onnx` and `RL_HARNESS_OPPONENT=<archetype|mirror|ckpt>`,
`RL_HARNESS_PAINTERS=ship-diagnostics,policy`. An absolute checkpoint path is imported
into the fixture slot automatically. For compositions the lane can't express (bespoke
overlays, non-archetype opponents), author a scratch scenario mirroring
`CaptureClient`'s composition: `host.NewComposition` (or `EpisodePair.SpawnWithAgentChooser`
→ `ShipAgentFactory.ComposeInferenceOnly` → `EpisodeLoopDriver`), pumping the episode
enumerator and calling `recorder.Step` per fixed step with the active painters. New
diagnostics are **painters** (`Game.Diagnostics.IDiagnosticPainter`), written once over
the `IDiagnosticCanvas` contract so they render in both clips and live editor gizmos —
never a capture-only overlay.

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
  `Time.captureDeltaTime=Time.fixedDeltaTime`, the harness `PacingContract`) so a
  recorded seed replays identically.
- **A recording harness session holds the machine-wide boot lane ~10× longer**
  than a headless eval — synchronous PNG render/readback runs at wall clock, not
  the sped-up sim. Expect a capture lane to occupy Unity access far longer than
  the same eval; plan the coordinator lease accordingly.
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
