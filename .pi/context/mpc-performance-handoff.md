# MPC Performance Handoff

Date: 2026-03-06

## Slot / PR

- Slot: `agent-1`
- Local branch: `agent-1`
- Remote task branch: `task/mpc-performance`
- PR: `https://github.com/amindell11/astronomical-home/pull/25`

## Goal

Continue the MPC performance plan up to and through the Burst/Jobs phase.

This branch already completed the low-risk CPU cleanup pass:
- profiling hooks moved to editor-only code
- repeatable MPC perf probe added
- several small hot-loop math and copy-path optimizations landed

Stop treating this as a math-tweaking phase. The next meaningful work is structural: make the rollout path Burst/Jobs-friendly.

## Current Green Gates

Run both of these after every meaningful change:

1. Behavior gate
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\unity_test_agent.ps1 -OutDir results/unity-tests-agent -Mode PlayMode -ScopeType Feature -ScopeName navigation
```

Expected current status:
- passed
- total `7`, passed `5`, failed `0`, skipped `2`

Latest passing summary:
- `D:\amind\git\agent-1\results\unity-tests-agent\20260306-064050-summary.json`

2. Perf probe
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\unity_test_agent.ps1 -OutDir results/unity-tests-agent -Mode PlayMode -ScopeType Feature -ScopeName navigation_perf
```

Expected current status:
- passed
- total `1`, passed `1`, failed `0`

Latest passing summary:
- `D:\amind\git\agent-1\results\unity-tests-agent\20260306-064132-summary.json`

Latest probe line:
- `avgShipSolveMs=2.205`
- `avgFrameSolveMs=17.638`
- `maxShipSolveMs=3.985`
- `maxFrameSolveMs=21.359`
- `movedShips=8/8`

Source artifact:
- `D:\amind\git\agent-1\results\unity-tests-agent\20260306-064132-PlayMode.xml`

## Important Test Detail

The MPC PlayMode suites were stabilized by disabling `UtilitySelector` inside the test fixtures. That change is already committed on this branch:
- commit `0f755381` `test(mpc): isolate waypoint-driven playmode fixtures`

Files:
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/MpcNavigatorPlayModeTests.cs`
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/MpcPerformancePlayModeTests.cs`

Do not remove that without replacing it with a better isolation strategy, or the MPC behavior tests will become noisy again.

## Landed Commit Stack

From oldest to newest on this topic:

- `f5d64cef` `MPC: add profiling markers for solver hot path`
- `aed5fa74` `MPC: reuse candidate buffers and drop runtime profiling`
- `13166d6f` `MPC: move profiling hooks to editor-only files`
- `6cb09178` `MPC: simplify editor profiling scopes`
- `2ff4d7bb` `MPC: add playmode perf probe for 8-ship solves`
- `8d9870c4` `MPC: cache hot-loop scalars and skip empty obstacle scans`
- `0fe0ada3` `MPC: simplify heading cost math in hot loop`
- `0f755381` `test(mpc): isolate waypoint-driven playmode fixtures`
- `c40f397c` `MPC: avoid redundant warm-start copy in sampler`
- `d47ce2a8` `MPC: batch remaining pre-Burst math cleanup`

## What Was Intentionally Not Done

- No solve-cadence reduction yet
- No algorithmic retuning
- No GPU path
- No Burst/Jobs rewrite yet

Several experimental local changes were tested and discarded if they did not improve the perf probe or if they risked changing behavior. The current branch tip is already cleaned back to the accepted state.

## Current Code Reality

Hot path still looks like:
- `MpcNavigator.GenerateNavCommands`
- `Sampler.Solve`
- `Sampler.EvaluateTrajectory`
- `Cost.Evaluate`
- `Model.Step`

Relevant files:
- `src/Asteroids3D/Assets/Scripts/AI/Steering/MPC/MpcNavigator.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/MPC/Sampler.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/MPC/Cost.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/MPC/Model.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/MPC/Types.cs`

Editor-only profiling hooks live behind:
- `src/Asteroids3D/Assets/Scripts/AI/Steering/MPC/Editor/Types.Editor.cs`

## Recommended Next Step

Start the Burst/Jobs phase.

Suggested sequence:

1. Extract a pure rollout-evaluation core that works on blittable structs only.
2. Define explicit data layout for:
   - state sequence / candidate sequence
   - obstacle scan buffer
   - immutable config/dynamics inputs
3. Keep the current managed path as the reference implementation.
4. Add a jobified evaluator that parallelizes candidates, then compare against the same `navigation` and `navigation_perf` gates.
5. Keep result selection deterministic.

## Constraints / Operational Notes

- The repo’s shared worktree metadata sometimes leaves a stale `index.lock` during commits. Retrying the commit has been sufficient.
- Unity test runs sometimes collide if started back-to-back. If one PlayMode or EditMode run just finished, wait for it to exit fully before launching the next one.
- There are recurring untracked Unity-generated `.meta` files in the `agent-1` worktree:
  - `src/Asteroids3D/Assets/Scripts/AI/Debug.meta`
  - `src/Asteroids3D/Assets/Scripts/AI/Debug/ArenaDebugOverlay.cs.meta`
  - `src/Asteroids3D/Assets/Scripts/Cameras/ArenaSpectatorInput.cs.meta`
  These were intentionally not added to the branch.

## Done Boundary

This branch is at the "ready for Burst compilation / Jobs extraction" boundary.

If a new agent picks this up, it should not spend another turn doing tiny scalar cleanups unless a probe proves a very specific win. The highest-leverage remaining work is structural.
