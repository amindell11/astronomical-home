# Profiling Work Summary

## Goal

Implement the plan in `.pi/plans/terminal-latency-profiling.md` in `agent-2`, with a terminal-first profiling workflow that:

- launches a standalone player
- runs a deterministic `combat_baseline` scenario
- records frame latency and profiler-derived timing
- emits machine-readable artifacts
- exits automatically when complete

## Work Completed

### Agent / worktree

- Acquired and prepared `agent-2` with lease `terminal-latency-profiling`
- Worked in `D:/amind/git/agent-2`

### Runtime profiling code added

Added new profiling code under:

- `src/Asteroids3D/Assets/Scripts/Diagnostics/Performance/LatencyProfilingScenario.cs`
- `src/Asteroids3D/Assets/Scripts/Diagnostics/Performance/LatencyProfilingRunner.cs`
- `src/Asteroids3D/Assets/Scripts/Diagnostics/Performance/LatencyMetricsCollector.cs`
- `src/Asteroids3D/Assets/Scripts/Diagnostics/Performance/LatencyProfilingMarkers.cs`

Intent of these files:

- parse `-latency*` command-line args
- define the `combat_baseline` scenario settings
- wait for gameplay startup
- warm up, sample, and write:
  - `summary.json`
  - `frame-times.csv`
- auto-quit when finished
- emit failure diagnostics with timeout if startup never reaches the expected state

### Scenario / gameplay wiring added

Hooked profiling overrides into runtime bootstrap:

- `src/Asteroids3D/Assets/Scripts/Game/Bootstrap/MainGameManager.cs`
- `src/Asteroids3D/Assets/Scripts/Game/Sectors/ArenaSectorManager.cs`
- `src/Asteroids3D/Assets/Scripts/Asteroids/Fields/UpdatingAsteroidField.Profiling.cs`

Intent:

- detect latency profiling mode before sector setup
- force deterministic arena settings for `combat_baseline`
- override arena counts / spawn radius / asteroid population

### Marker instrumentation added

Added initial profiler markers in:

- `src/Asteroids3D/Assets/Scripts/AI/AICommander.cs`
- `src/Asteroids3D/Assets/Scripts/Combat/Weapons/WeaponBase.cs`
- `src/Asteroids3D/Assets/Scripts/Combat/Projectile/ProjectileBase.cs`
- `src/Asteroids3D/Assets/Scripts/Objectives/ObjectiveTracker.cs`
- `src/Asteroids3D/Assets/Scripts/Asteroids/Spawning/AsteroidSpawner.cs`

Marker intent:

- AI update
- projectile fire
- projectile update
- objective tracker tick
- asteroid spawn / fragment spawn

### Launcher scripts added

Added:

- `scripts/run_latency_profile.ps1`
- `scripts/run_latency_profile.cmd`

Intent:

- launch the standalone player with explicit profiling args
- create a timestamped output directory
- optionally capture raw Unity profiler output

### Build profile fix

Fixed scene order in:

- `src/Asteroids3D/Assets/Settings/Rendering/Build Profiles/Main.asset`

Changed order from:

1. `Assets/Scenes/BasicWorld.unity`
2. `Assets/Scenes/InitScene.unity`

to:

1. `Assets/Scenes/InitScene.unity`
2. `Assets/Scenes/BasicWorld.unity`

This was necessary because the standalone player was launching `BasicWorld` first when it should start in `InitScene`.

### Startup/teardown bug fix

Patched null-unsafe observer teardown in:

- `src/Asteroids3D/Assets/Scripts/Game/Sectors/Utils/SectorUtils.cs`

Reason:

- player logs showed a `NullReferenceException` during observer/registry cleanup
- this was causing standalone runs to die during startup/teardown

### Tests added

Added:

- `src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/LatencyProfilingEditModeTests.cs`

Covered:

- command-line parsing
- required scenario detection
- output directory resolution

## Verified Successfully

### Unity EditMode tests

Confirmed passing:

- `./scripts/agent_worktree_pool.sh run-tests agent-2 -Mode EditMode -TestFilter Tests.EditMode.LatencyProfilingEditModeTests`

Observed result:

- `passed total=3 passed=3 failed=0 skipped=0`

### Standalone builds

Successfully produced standalone Windows players multiple times, including:

- `build/StandaloneWindows64-Fixed/Asteroids3D.exe`
- `build/StandaloneWindows64-Fixed2/Asteroids3D.exe`
- `build/StandaloneWindows64-Fixed3/Asteroids3D.exe`
- `build/StandaloneWindows64-Fixed4/Asteroids3D.exe`

Build logs showed:

- `Build Finished, Result: Success`

## Current Blocker

### Main unresolved issue

The profiling runner still does **not** execute in the standalone player.

Evidence:

- profiling run output directories contain only `run.log`
- they do **not** contain:
  - `summary.json`
  - `frame-times.csv`
  - `latency-runner.log`
  - `error.txt`
- player windows stayed open until manually closed in multiple attempts
- even after adding timeout/fail-fast behavior, no runner diagnostics were written

This strongly suggests:

- the separate profiling bootstrap path is still not running in the built player
- or it is not running early enough / in the way expected

### Important conclusion

The current separate `LatencyProfilingRunner` bootstrap approach is not reliable enough yet for this project/player configuration.

## Most Recent Findings

### Player log behavior

Latest standalone player logs show:

- normal engine startup
- gameplay object creation beginning
- `ComponentStateDiagnostics` logs from existing gameplay systems
- no `[LatencyProfiling]` logs
- no runner diagnostic log file written

That means gameplay code is executing, but the profiling runner path is still not observably active.

### Manual timing expectation

For the short test command:

- warmup: `5s`
- sample: `10s`
- startup timeout: `45s`

Expected behavior:

- auto-close after startup + ~15s successful run
- or auto-close with error by ~45s timeout

Actual behavior:

- player remained open until manually forced closed
- standalone process still failed to produce profiling artifacts

## Recommended Next Step

Move profiling command-line handling out of the separate runner bootstrap and into code that is already proven to run in the standalone player.

Best candidate:

- integrate profiling startup directly into `MainGameManager`

Reason:

- `MainGameManager` definitely executes in standalone
- it already controls the sector lifecycle
- it is a reliable place to:
  - detect latency profiling args
  - start warmup/sample timing
  - write artifacts
  - enforce timeout / fail-fast behavior
  - call `Application.Quit()`

## Suggested Follow-up Implementation

1. Create a small profiling session object used by `MainGameManager` directly.
2. Start the session once `CurrentState` transitions to `InSector`.
3. If `InSector` is not reached within timeout, write `error.txt` and quit.
4. Keep `LatencyMetricsCollector` and marker instrumentation; those parts are still useful.
5. Remove or minimize reliance on `RuntimeInitializeOnLoadMethod` for player startup.

## Key Files Changed

- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Diagnostics/Performance/LatencyProfilingScenario.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Diagnostics/Performance/LatencyProfilingRunner.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Diagnostics/Performance/LatencyMetricsCollector.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Diagnostics/Performance/LatencyProfilingMarkers.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Asteroids/Fields/UpdatingAsteroidField.Profiling.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Game/Bootstrap/MainGameManager.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Game/Sectors/ArenaSectorManager.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Game/Sectors/Utils/SectorUtils.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/AI/AICommander.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Combat/Weapons/WeaponBase.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Combat/Projectile/ProjectileBase.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Objectives/ObjectiveTracker.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Asteroids/Spawning/AsteroidSpawner.cs`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Settings/Rendering/Build Profiles/Main.asset`
- `D:/amind/git/agent-2/scripts/run_latency_profile.ps1`
- `D:/amind/git/agent-2/scripts/run_latency_profile.cmd`
- `D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/LatencyProfilingEditModeTests.cs`
