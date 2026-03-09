---
name: unity-perf-probe-loop
description: Run repeatable Unity performance probes across commits or worktrees in this repo. Use when comparing performance between revisions, validating MPC perf changes, or measuring before/after results with `unity_test_agent.ps1` and a dedicated perf PlayMode scope such as `navigation_perf`.
---

# Unity Perf Probe Loop

Use this skill when the goal is to compare Unity runtime cost across revisions, not just to run normal correctness tests.

## Workflow

1. Keep the main task worktree clean.
   Use temporary detached worktrees for historical comparisons so the active slot branch stays unchanged.

2. Put the same probe on every revision being measured.
   If the perf test only exists on newer commits, cherry-pick the perf-probe commit onto each temporary worktree before measuring.

3. Run measurements sequentially, never in parallel.
   Do not run two `unity_test_agent.ps1` PlayMode probes against different worktrees at the same time.
   Unity launches are slow enough to look hung, and parallel runs can collide on project/package manager state and leave empty or partial logs.

4. Use the same command, scope, and machine conditions for each run.
   Preferred example:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\unity_test_agent.ps1 -OutDir results/unity-tests-agent -Mode PlayMode -ScopeType Feature -ScopeName navigation_perf
```

5. Read the metric line from the produced XML or log, not from memory.
   For the MPC probe, extract the `MPC perf probe | ...` line from `results/unity-tests-agent/*-PlayMode.xml` or `*-PlayMode.log`.

6. Restore state after ad hoc measurements.
   Remove temporary worktrees when done.
   If a temporary local edit was used to simulate a revert or disable a feature, restore the file to `HEAD` immediately after the run and verify `git status`.

## Guardrails

- Prefer sequential runs in this order: baseline, changed revision, optional confirmation rerun.
- If a run is interrupted, check for lingering `Unity.exe` or `UnityPackageManager` processes before retrying.
- If a literal `git revert` of an older commit conflicts on current `HEAD`, measure the equivalent behavior in a temporary worktree at the historical commit instead of forcing conflict resolution into the active branch.
- Keep perf probes diagnostic unless there is already a stable threshold for the machine. Log measured values; do not invent hard pass/fail limits.

## MPC Pattern

For MPC work in this repo:

1. Add or reuse a dedicated PlayMode perf probe such as `MpcPerformancePlayModeTests`.
2. Expose it through `scripts/unity_test_scopes.json` with a dedicated scope such as `navigation_perf`.
3. Record before/after values for:
   `avgShipSolveMs`, `avgFrameSolveMs`, `maxShipSolveMs`, `maxFrameSolveMs`, and `movedShips`.
4. Use those numbers to decide whether a runtime optimization commit is worth keeping.

## Historical Comparison Example

```powershell
git worktree add D:\amind\git\tmp-mpc-perf-a <commit-a>
git worktree add D:\amind\git\tmp-mpc-perf-b <commit-b>

git -C D:\amind\git\tmp-mpc-perf-a cherry-pick <perf-probe-commit>
git -C D:\amind\git\tmp-mpc-perf-b cherry-pick <perf-probe-commit>

# Run one at a time, not in parallel.
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\unity_test_agent.ps1 -OutDir results/unity-tests-agent -Mode PlayMode -ScopeType Feature -ScopeName navigation_perf
```

After both runs, compare the emitted probe lines and then remove the temporary worktrees.
