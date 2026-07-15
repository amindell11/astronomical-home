---
name: game-capture
description: Record footage of any game situation with per-investigation diagnostic markup (lines, vectors, rings, labels) and hand the user a clip. Use when footage is the deliverable — showing a behavior, visually diagnosing a sim bug, demoing a feature, or recording RL episodes.
metadata:
  project: astronomical-home
  plan-doc: doc/Feature_Plans/Game_Capture.md
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
  RL run into a recording session.

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

## Run + assemble (one command each)

```powershell
./scripts/agent_worktree_pool.sh run-tests agent-N -Mode PlayMode -TestFilter Tests.PlayMode.CaptureScenarioPlayModeTests -WithGraphics -CaptureScenario MyProbe
python scripts/capture/assemble.py <slot-path>/results/capture/frames/<stamp>-MyProbe
```

The runner prints the absolute frame dir. `assemble.py` defaults fps/dims from the
frame dir's `manifest.json`; `--format gif` for chat-friendly clips, `--scale 0.5` to
shrink. mp4 needs `pip install imageio-ffmpeg` once (wheel bundles ffmpeg).

**Report the slot's absolute output path** — `results/` is worktree-local, so
`results/capture/...` in agent-N is NOT the primary tree's `results/`.

## Hard-won constraints (violate = silent garbage)

- **Gizmos/Handles never render into offscreen captures.** Only `CaptureDraw`
  primitives show up. That's why the overlay exists.
- **`-WithGraphics` is for filtered runs only** (it requires PlayMode + an explicit
  `-TestFilter`, and fails on zero tests executed). Never the merge-gate suite.
- **Never call `Gunsight.Evaluate()` from an overlay** — it mutates the firing path's
  LOS cache (observer effect on the sim). `InEnvelope()` only.
- **Eyeball a mid-clip PNG (Read the file) before claiming success** — compile-green
  says nothing about render output; v1's overlay failed only at render time. For label
  checks, confirm the label *changes* across frames.
- Scratch scenarios are staged into `Tests/PlayMode/Scratch/` only for the run and
  auto-removed; if a run died hard, the next run sweeps leftovers. Don't put files
  there yourself.
- Capture asserts loudly on empty/NaN subjects and a reused frame dir — fix the
  scenario, don't catch the exception.
