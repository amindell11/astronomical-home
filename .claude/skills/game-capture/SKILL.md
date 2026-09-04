---
name: game-capture
description: Record footage of any game situation, with native gizmo diagnostics drawn over it, and hand the user a clip. Use when footage is the deliverable — showing a behavior, visually diagnosing a sim bug, demoing a feature, or recording RL episodes.
metadata:
  project: astronomical-home
---

# Game Capture

Record PNG frame dumps of a game situation through the Editor's Game View, with
native gizmos drawn over it, then assemble them into mp4/gif. **The footage is the
deliverable** — always end by reading a mid-clip PNG yourself and handing the user
the clip path.

Capture needs a rendering Game View — a **windowed Editor** booted by the test runner
(`-WithGraphics -Windowed`), or a **resident GUI editor** via the warm lane. This is
structural, not a preference: Unity never resumes Recorder's `WaitForEndOfFrame` under
`-batchmode`, so there is no headless capture lane.

## Pick a lane

- **Repeat clips against a running Editor** → the **warm lane**: attach once, dispatch
  scenarios without rebooting (~1 s play-enter instead of a boot per clip):
  §"Warm lane (attach to a resident editor)".
- **Live-editor still** → verify drawers/visuals in a running coordinated Editor over
  the `unity` CLI — stills, not clips: §"Live-editor stills (CLI lane)".
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
  checkpoint (default: the smoke fixture); `RL_HARNESS_GIZMOS` picks one gizmo capture
  profile (`steering`, `combat`, `everything`). A profile films with presentation off —
  collider silhouettes plus gizmo geometry; **unset films plain gameplay with
  presentation on**. Full `RL_HARNESS_*` grammar in `training/rl/README.md`. The session
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
using Tests.PlayMode.Common;
using UnityEngine;

public sealed class MyProbe : CaptureScenario
{
    // Which native gizmos the footage carries; None films the game alone.
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.Combat;

    public override IEnumerator Run()
    {
        var (a, _) = SpawnCombatShip(new Vector2(-12f, 0f), rotDeg: -90f, team: 0);
        var (b, _) = SpawnCombatShip(new Vector2(12f, 0f), rotDeg: 90f, team: 1);
        Film(a, b);                                       // framed + gizmo-selected subjects

        for (var i = 0; i < 400 && a && b; i++)
        {
            yield return new WaitForFixedUpdate();
            FilmStep();
        }
    }
}
#endif
```

`Film(...)` starts the episode and names the ships to frame and select; `FilmStep()`
advances one captured step. The runner ends the episode when `Run` returns or throws.
Override `Config` for clip name/size/cadence, `Profile` for the gizmo set, and
`Config.gizmoScope` (`All` / `Selected` / `Team` + `gizmoScopeTeam`) for whose gizmos draw.

**Define the gizmo set and scope from what the clip must show.** Pick the narrowest
`Profile` and `gizmoScope` that reveal the target behaviour: a `Combat` clip of one ship's
firing solution scopes to that ship; a two-team engagement scopes by `Team`. `Everything` at
scope `All` stacks every drawer's ink over every object and buries the subject — reach for it
only when the whole board is the point.

**There is no per-scenario drawing API.** A diagnostic you want on film is a native
`[DrawGizmo]` drawer on the Unity component whose state it explains, added to the
relevant profile in `GizmoCaptureProfiles` — never a capture-only overlay. That is the
whole point of the painter removal (#376): one place to author a diagnostic, seen
identically in the live Editor and on film.

## Film a trained checkpoint (policy vs archetype)

The harness capture lane (above) covers the standard case: `RL_HARNESS_LANE=capture`
with `RL_HARNESS_ONNX=<ckpt>.onnx` and `RL_HARNESS_OPPONENT=<archetype|mirror|ckpt>`,
`RL_HARNESS_GIZMOS=combat` (unset films plain gameplay). An
absolute checkpoint path is imported into the fixture slot automatically. For
compositions the lane can't express (bespoke overlays, non-archetype opponents),
author a scratch scenario mirroring
`CaptureClient`'s composition: `host.NewComposition` (or `EpisodePair.SpawnWithAgentChooser`
→ `ShipAgentFactory.ComposeInferenceOnly` → `EpisodeLoopDriver`), pumping the episode
enumerator and calling `FilmStep()` per fixed step.

## Warm lane (attach to a resident editor)

Film repeat clips through an editor your work stream already holds (unity-access
rung 3) instead of booting one per clip: `capture_lane_attach` switches the editor
to no-reload play (Enter Play Mode Options), so play-enter drops from ~7–8 s to
~1 s and a clip costs its real-time runtime. The user's own interactive editor is
the default target — **ask first**: capture steals the Game View for the clip and
flips EPO/presentation state (all restored on release; a hard-killed lane restores
itself on the editor's next load via the lane journal). A leased slot editor
(unity-access rung 4) serves AFK work.

```powershell
unity command capture_lane_attach --project-path <proj>   # once per session
unity command capture_request_scenario --scenario TwoShipSkirmishScenario --project-path <proj>
./scripts/unity_test_agent.ps1 -Routed -Mode PlayMode -TestFilter Tests.PlayMode.CaptureScenarioPlayModeTests -ExcludeCategory '' -ProjectPath <proj>
unity command capture_lane_release --project-path <proj>  # restores EPO
```

- `-ExcludeCategory ''` is required: the capture fixture is `RequiresGraphics`,
  and `-Routed` makes running excluded categories in a resident editor a
  deliberate act. The editor must not already be in Play Mode.
- The request is one-shot — cleared when the runner reads it, dead with the
  editor — so a stale scenario can never refilm. The run prints the frame dir;
  assemble as below.
- Scenario types must already be compiled in the resident editor: promoted
  scenarios just work. A scratch scenario must be copied under `Assets/` (e.g.
  `.../Editor/Tests/PlayMode/Scenarios/`) first — wait out the recompile, re-arm
  `set_autotick --enable true`, delete the file (and `.meta`) after. The cold
  runner's automatic scratch staging never runs here.

## Live-editor stills (CLI lane)

Verify drawers/visuals in a live coordinated Editor over the `unity` CLI (route into a
held editor per the unity-access skill; CLI contract: `doc/agents/unity-cli.md`).
Proven end-to-end by the 2026-08-26 gizmo-eyeball pass (arc #357). Ready-made eval
snippets live in this skill's `cli-eval/` — run them with `eval_file`.

- **Gizmos composite only in the play-mode Game view.** Reflect the internal
  `GameView.drawGizmos = true` (`cli-eval/gameview_gizmos_on.cs`; the same contract
  `UnityGameViewAdapter` pins), then `unity command capture_game_view --source screen`.
  `capture_scene_view` and `screenshot --view scene` re-render the camera and never
  composite gizmos. Consequence: **edit-mode gizmo claims need human eyes** — the CLI
  cannot verify them. (`screenshot --view game` with drawGizmos forced on was probed
  2026-08-27 and REFUTED — it re-renders the camera, no gizmos; `capture_game_view
  --source screen` is the only composited still. Delete its `save_path` output from
  `Assets/` after.)
- **Check per-type gizmo checkboxes before calling a drawer broken** — AnnotationManager
  state is per-Library and silently hides drawers (`cli-eval/read_annotations.cs`,
  `enable_gizmo_annotations.cs`; the #401 flake family).
- **Select via eval** (`cli-eval/select_ships.cs`) and bracket each capture with a
  state-read eval so you know what was actually on screen when the frame was taken.
- **Live-fire scene without playing the game:** boot InitScene, then
  `cli-eval/launch_no_presentation.cs`, `spawn_enemy.cs` (`UnitService.SpawnShip` with a
  Ship prefab + AgentPilot Commander), `teleport_close.cs` for tight ObserverCam framing.
  `launch_no_presentation.cs` flips `GameSessionHost.sessionProfile.presentation = false`
  **before** clicking hangar launch, so the pre-spawn compose suppresses the asteroid
  field's renderers too — poking only the `GameSettings` static after compose leaves the
  field lit (the "magenta asteroid" leak). With presentation off, the environment
  silhouette comes from **collider gizmos** (the Gizmo View Colliders toggle / the capture
  transaction's `CollidersOn`), not unlit meshes.
- **Sub-second subjects are out of reach**: a select→capture round-trip is ~0.5–1 s, so
  laser bolts and projectiles-in-flight cannot be stilled from outside — that needs an
  editor-side atomic `[CliCommand]`, `capture.gizmo_still` (carded #446).
  Meanwhile: pause with the subject in flight and select it manually.

## Run + assemble (one command each)

```powershell
./scripts/agent_worktree_pool.sh run-tests agent-N -Mode PlayMode -TestFilter Tests.PlayMode.CaptureScenarioPlayModeTests -WithGraphics -Windowed -CaptureScenario MyProbe
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

- **Capture needs a windowed Editor** (`-WithGraphics -Windowed`). Recorder starves
  without a rendering Game View under `-batchmode`, and gizmos never render into a
  manual offscreen `camera.Render()`. There is no headless capture lane.
- **Batch runs are `-nographics` by default; `-WithGraphics` is for filtered runs
  only** (it requires PlayMode + an explicit `-TestFilter`, and fails on zero tests
  executed). Never the merge-gate suite — never record merge-gate proof from a
  graphics run.
- **Frame pixels are sRGB; gizmo colors are linear.** The project renders linear
  (`m_ActiveColorSpace: 1`), so a gizmo `Color(1, 0.55, 0.15)` lands in the PNG as
  `(255, 196, 108)`, not `(255, 140, 38)`. Scanning a frame for the gamma-space bytes
  finds zero hits and reads exactly like "the drawer never ran" — convert before you
  assert, and instrument the drawer before believing a colour scan that says nothing
  drew.
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
