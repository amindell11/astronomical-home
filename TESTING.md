# Testing Guide — Asteroids3D

Agent-friendly reference for running, interpreting, and extending the Unity test suite.

---

## Quick Start (Agent / CI)

Unity execution is serialized across all worktrees. The test runner joins the
FIFO automatically, prefers a short-lived batch process, and releases the lane
after each Unity invocation. Check the lane before a run:

```powershell
.\scripts\unity_access.ps1 -Action Status
```

Agents can wait on the queue without manual polling. `Wait` renews the ticket
while it polls and acquires automatically when earlier owners or unmanaged
Unity processes exit:

```powershell
.\scripts\unity_access.ps1 -Action Wait -Lease my-task-tests -Slot agent-1 -Mode batch -WaitSeconds 60
```

If an untracked main-worktree editor is reported, it is user-owned. Ask the
user to close it and rerun; never terminate it. Requests remain queued for the
next attempt.

```powershell
# From repo root — runs both EditMode and PlayMode, writes JSON summary.
.\scripts\unity_test_agent.ps1

# Exit codes
# 0  — all tests passed
# 1  — one or more tests failed
# 2  — infrastructure error (Unity crashed, XML not produced)
```

The script writes two files to `results/unity-tests-agent/`:
- `<timestamp>-summary.json` — timestamped full report
- `latest-summary.json`      — always the most-recent run (overwritten each time)

> Standardization note: canonical test artifact path is `results/unity-tests-agent`. When using `unity_test_run` directly, always pass `outDir: "results/unity-tests-agent"`. If omitted, some runners default to `TestResults/`, which fragments artifacts.

### Warm Worktree Pool (agent-1 / agent-2 / agent-3)

To avoid repeated Unity re-import/build cost in fresh worktrees, use the persistent pool script:

```bash
# View pool state
./scripts/agent_worktree_pool.sh status

# Acquire a free slot (creates a lock)
./scripts/agent_worktree_pool.sh acquire my-task-id

# Reset that slot to origin/main without deleting ignored cache dirs (Library/)
./scripts/agent_worktree_pool.sh prepare agent-1 origin/main

# Run tests in that slot (always writes to results/unity-tests-agent)
./scripts/agent_worktree_pool.sh run-tests agent-1 -Mode Both -ScopeType Workspace

# Create a PR for a slot branch (requires gh auth)
./scripts/agent_worktree_pool.sh create-pr agent-1

# Create PRs for all slot branches that are ahead of main
./scripts/agent_worktree_pool.sh create-pool-prs

# One-shot flow: prepare + run tests + create PR + release lock
./scripts/agent_worktree_pool.sh finalize agent-1 origin/main -- -Mode Both -ScopeType Workspace

# During PR review loop: inspect unresolved feedback, then revise branch
./scripts/agent_worktree_pool.sh review-comments agent-1
./scripts/agent_worktree_pool.sh revise agent-1 -- -Mode Smoke -ScopeType Feature -ScopeName camera

# Release lock when done
./scripts/agent_worktree_pool.sh release agent-1
```

`prepare` uses `git clean -fd` (not `-fdx`) so ignored Unity cache directories remain warm.

### Interactive Editor / MCP Checks

Use an interactive editor only when headless batch tests cannot cover the
behavior. The coordinator starts or reuses the durable MCP server, records the
owning slot and editor PID, and blocks behind earlier requests:

```powershell
.\scripts\unity_access.ps1 -Action StartEditor -Lease my-task-editor -Slot agent-1 -Mode editor -WaitSeconds 60
.\scripts\unity_access.ps1 -Action Status
.\scripts\unity_access.ps1 -Action Release -Lease my-task-editor -CloseEditor
```

Do not use the MCP window's **Stop Server** action. The server on port 8081 is
shared; release only the editor session and lane.

### Parameters

| Parameter          | Default                                    | Description                              |
|--------------------|--------------------------------------------|------------------------------------------|
| `-UnityPath`       | `D:\Programs\Unity\Editor\6000.1.8f1\...` | Path to `Unity.exe`                      |
| `-ProjectPath`     | `src/Asteroids3D`                          | Unity project root                       |
| `-OutDir`          | `results/unity-tests-agent`                | Output directory for XML + JSON          |
| `-Mode`            | `Both`                                     | `Both`, `EditMode`, or `PlayMode`        |
| `-ScopeType`       | `Workspace`                                | `Workspace`, `Feature`, `Module`, `Smoke`, or `Auto` |
| `-ScopeName`       | *(empty)*                                  | Name of feature/module (required for Feature/Module) |
| `-TestFilter`      | *(resolved from scope)*                    | NUnit filter string (overrides scope resolution) |
| `-TestCategory`    | *(none)*                                   | NUnit category filter                    |
| `-ValidateScope`   | off                                        | Validate scope filter matches at least one test |
| `-ScopeMapPath`    | `scripts/unity_test_scopes.json`           | Path to scope definition file            |
| `-UnityTimeoutSec` | `1800`                                     | Kill batch Unity run after timeout (prevents indefinite hangs) |
| `-MaxFailures`     | `25`                                       | Max failures to include in JSON          |
| `-IncludeStackTrace` | off                                      | Include stack traces in JSON output      |

---

## Scope-Based Execution

The test agent supports **scope-based test selection** through `scripts/unity_test_scopes.json`.  
Instead of manually specifying test filters, you can run predefined test scopes:

### Scope Types

| Scope Type  | Description | Requires `-ScopeName`? | Example |
|-------------|-------------|------------------------|---------|
| `Workspace` | All tests (empty filter) | No | `.\scripts\unity_test_agent.ps1` |
| `Smoke`     | Fast sanity checks | No | `.\scripts\unity_test_agent.ps1 -ScopeType Smoke` |
| `Feature`   | Feature-specific tests (curated name-regex) | **Yes** | `.\scripts\unity_test_agent.ps1 -ScopeType Feature -ScopeName camera` |
| `Module`    | A module's domain-category slice, derived from the fixtures its `paths` cover | **Yes** | `.\scripts\unity_test_agent.ps1 -ScopeType Module -ScopeName ai` |
| `Auto`      | Diff-driven: changed files → modules → their fixtures' domain categories; any unmapped file falls back to full Workspace | No | `.\scripts\unity_test_agent.ps1 -ScopeType Auto -DiffBase origin/main` |

### Available Scopes

**Smoke** — Fast sanity checks across all critical systems:
```powershell
.\scripts\unity_test_agent.ps1 -ScopeType Smoke
```

**Features** — Focused on specific features (see `scripts/unity_test_scopes.json`):
`camera`, `navigation` (MPC solver + navigator), `navigation_perf`, `scanning`,
`ai`, `physics`, `weapons`, `targeting`, `objectives`, `sectors`, `damage`,
`ui`, `ships`, `services`, `bootstrap`.

```powershell
.\scripts\unity_test_agent.ps1 -ScopeType Feature -ScopeName navigation
```

**Modules** — Broader system-level groupings, defined by directory `paths`:
`ai`, `mpc`, `combat`, `sectors`, `objectives`, `asteroids`, `ui`, `utils`,
`workspace`. A module run selects tests by the **domain `[Category]` tags on the
fixtures its `paths` cover** — not a hand-maintained name list — so a new fixture
in an already-mapped domain is picked up automatically.

```powershell
.\scripts\unity_test_agent.ps1 -ScopeType Module -ScopeName ai
```

**Auto** — Resolves the same way against the working-tree diff (see the `Auto`
row above); this is the recommended scope for iteration and submit runs.

> `-TestCategory <Domain>` is the zero-upkeep primitive underneath all of this:
> every fixture carries a domain category, so category selection never goes stale
> the way name-regex `Feature`/`Smoke` scopes can. See
> [NUnit Categories](#nunit-categories) below.

### Scope Validation

Use `-ValidateScope` to ensure scope definitions are not stale:

```powershell
.\scripts\unity_test_agent.ps1 -ScopeType Feature -ScopeName camera -ValidateScope
```

This runs a dry-run check to verify the scope's test filter matches at least one test.  
If the filter matches **no tests**, the script exits with an error, indicating the scope definition may be outdated.

**When to validate:**
- After renaming/moving test files
- After refactoring test class names
- When adding new scopes to `unity_test_scopes.json`
- In CI pipelines to catch stale scope definitions early

### Scope Map Structure (`scripts/unity_test_scopes.json`)

```json
{
  "smoke": {
    "testFilter": "CameraUtilsEditModeTests|CollisionDamageUtilityTests|..."
  },
  "features": {
    "camera": {
      "testFilter": "CameraUtilsEditModeTests|CameraFollowPlayMode"
    },
    "navigation": {
      "testFilter": "MpcNavigatorPlayMode|MpcSolverTests|MpcBoostEditModeTests|LosCostEditModeTests"
    }
  },
  "modules": {
    "ai": {
      "paths": [
        "src/Asteroids3D/Assets/Scripts/AI/**",
        "src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/ScannerPlayModeTests.cs*"
      ]
    }
  }
}
```

**Features** carry an NUnit name-regex `testFilter` (pipe-separated class-name
patterns) — curated, human-picked, kept in sync by hand on rename. **Modules**
carry only `paths` (repo-relative forward-slash globs): `-ScopeType Auto` maps
changed files to modules and `-ScopeType Module <name>` names one, then both
derive the run from the domain `[Category]` tags on the fixtures those paths
cover, so a new fixture in a mapped domain needs no map edit. Speed/selector
overlays (`Smoke`/`Slow`/`RequiresGraphics`/`ChaseBenchmark`) never seed a scope,
and the `Smoke` category is always added. A changed file matching no module glob,
or a matched module whose paths cover no tagged fixture, falls back to the full
Workspace suite — so leaving ambiguous areas unmapped is safe.

### Examples

**Run smoke tests with validation:**
```powershell
.\scripts\unity_test_agent.ps1 -ScopeType Smoke -ValidateScope
```

**Run feature tests in EditMode only:**
```powershell
.\scripts\unity_test_agent.ps1 -Mode EditMode -ScopeType Feature -ScopeName physics
```

**Override scope with explicit filter:**
```powershell
# Scope is ignored when -TestFilter is provided
.\scripts\unity_test_agent.ps1 -ScopeType Feature -ScopeName camera -TestFilter "CameraUtilsEditModeTests"
```

**Run a single domain across both modes:**
```powershell
# No scope map needed — every fixture carries a domain category
.\scripts\unity_test_agent.ps1 -Mode Both -TestCategory Sectors
```

---

### Legacy Examples (still supported)

**Run only Smoke category tests (old method):**
```powershell
.\scripts\unity_test_agent.ps1 -Mode EditMode -TestFilter "Category=Smoke"
```

**Run both modes, full stack traces on failure:**
```powershell
.\scripts\unity_test_agent.ps1 -IncludeStackTrace
```

---

## JSON Summary Format

```json
{
  "generatedAt": "2026-03-01T15:00:00.000Z",
  "status": "passed|failed|infra_error",
  "totals": {
    "total": 42,
    "passed": 41,
    "failed": 1,
    "skipped": 0,
    "durationSec": 18.4
  },
  "runs": [
    {
      "platform": "EditMode",
      "status": "passed",
      "total": 20,
      "passed": 20,
      "failed": 0,
      "failures": []
    },
    {
      "platform": "PlayMode",
      "status": "failed",
      "total": 22,
      "passed": 21,
      "failed": 1,
      "failures": [
        {
          "name": "MpcYawOnly_ShipRotatesToFacingOverride",
          "fullName": "Tests.PlayMode.MpcNavigatorPlayModeTests.MpcYawOnly_...",
          "durationSec": 8.01,
          "message": "Expected: less than 10 But was: 14.3"
        }
      ]
    }
  ]
}
```

Machine-readable status check:

```powershell
$result = Get-Content results/unity-tests-agent/latest-summary.json | ConvertFrom-Json
if ($result.status -ne "passed") { exit 1 }
```

---

## Test Structure

```
Assets/Scripts/Editor/Tests/
├── TEST_CATEGORIES.md                 # Pointer to the "NUnit Categories" section below
├── EditMode/                          # Compiled as Tests.EditMode.asmdef (Editor only)
│   ├── ForcesEditModeTests.cs         # [Physics] Forces.ComputeOutputs — pure math
│   ├── CollisionDamageUtilityTests.cs # [Damage]  Kinetic energy / damage formulae
│   ├── MpcSolverTests.cs              # [MPC]     Solver decisions (cheap unit coverage)
│   ├── ObjectiveTrackerEditModeTests.cs # [Objectives] Mission state machine
│   └── … one fixture per feature (17 total; grouped by domain category)
│
└── PlayMode/                          # Compiled as Tests.PlayMode.asmdef
    ├── Common/                        # Shared test utilities (reduce duplication)
    │   ├── PlayModeWorldFixture.cs    # Shared setup/teardown + isolation
    │   ├── TestAssets.cs              # Asset loading (AssetDatabase helpers)
    │   ├── ShipTestFactory.cs         # Ship creation with common configs
    │   ├── AsyncAssert.cs             # Async polling assertions with timeout
    │   ├── AIIntegrationFixture.cs    # Multi-ship AI loop base fixture
    │   ├── StubShipRegistry.cs        # Minimal IShipRegistry stub
    │   └── TestUtilities.cs           # General helpers (distance, angle, audio)
    ├── TestSceneBuilder.cs            # Scene-building utilities (not a test class)
    ├── CameraFollowPlayModeTests.cs   # [Camera]   ObserverCam follow behaviour
    ├── GamePlanePlayModeTests.cs      # [Core]     GamePlane coordinate transforms
    ├── MpcNavigatorPlayModeTests.cs   # [MPC]      MPC closed-loop navigation
    ├── SectorCompositionPlayModeTests.cs # [Sectors] Manifest adopt/spawn/teardown
    └── … one fixture per feature (17 total; grouped by domain category)
```

### Naming Conventions

All test files and classes follow a strict naming convention enforced by `scripts/check_test_naming.ps1`:

**Rules:**
1. **Test files** must end with `Tests.cs` (e.g., `MyFeatureTests.cs`)
2. **Test classes** must match their file name exactly (e.g., `public class MyFeatureTests`)
3. **Utility classes** (like `TestSceneBuilder`) are exempt from the `*Tests` requirement

**Validation:**
```powershell
# Check all test files for naming violations
.\scripts\check_test_naming.ps1

# Show suggested fixes for violations
.\scripts\check_test_naming.ps1 -Fix
```

This check is run in CI to prevent naming drift. If you rename a test class, you must also rename the file to match.

---

## Shared PlayMode Test Utilities

The `Tests.PlayMode.Common` namespace provides reusable utilities to reduce duplication across PlayMode tests:

### TestAssets
Centralized asset loading to avoid repeated `AssetDatabase.LoadAssetAtPath` calls:
```csharp
using Tests.PlayMode.Common;

// Load common test assets
var settings = TestAssets.LoadDefaultShipSettings();
var shipPrefab = TestAssets.LoadShip2Prefab();
var mpcPilotPrefab = TestAssets.LoadTestPilotMpc();  // MPC pilot (the only pilot)
```

### ShipTestFactory
Factory methods for creating ships with standard test configurations:
```csharp
using Tests.PlayMode.Common;

// Create ship with default settings at origin
ship = ShipTestFactory.CreateDefaultShip();

// Create at specific position
ship = ShipTestFactory.CreateDefaultShipAt(position, rotation);

// Clean up
ShipTestFactory.DestroyShip(ship);
```

### AsyncAssert
Polling-based assertions with configurable timeouts to avoid flaky tests:
```csharp
using Tests.PlayMode.Common;

// Wait until condition becomes true (with timeout)
yield return AsyncAssert.WaitUntil(
    () => ship.arrived,
    timeoutSec: 5f,
    failureMessage: "Ship did not arrive",
    useFixedUpdate: true);

// Wait until condition, then run custom assertion
yield return AsyncAssert.WaitUntilThen(
    () => distanceToTarget < threshold,
    timeoutSec: 10f,
    () => Assert.That(finalDistance, Is.LessThan(threshold)),
    useFixedUpdate: true);

// Assert condition remains false for duration
yield return AsyncAssert.WaitAndAssertRemainsFalse(
    () => camera.hasMoved,
    waitSec: 3f,
    failureMessage: "Camera moved unexpectedly");

// Specialized helpers for common patterns
yield return AsyncAssert.WaitForVector2NearTarget(
    () => GamePlane.WorldPointToPlane(ship.transform.position),
    targetPos,
    threshold: 0.5f,
    timeoutSec: 20f);
```

### TestUtilities
General-purpose helpers for common test operations:
```csharp
using Tests.PlayMode.Common;

// Distance calculations
float dist = TestUtilities.DistanceToPlaneTarget(ship.transform, targetPos);

// Angle calculations
float facingAngle = TestUtilities.GetPlaneFacingAngle(ship.transform);
float angleDelta = TestUtilities.AngleDeltaToTarget(ship.transform, targetAngle: 90f);

// Audio management (SetUp / TearDown)
TestUtilities.PauseAudio();   // In SetUp
TestUtilities.ResumeAudio();  // In TearDown
```

---

## NUnit Categories

Every fixture is tagged on **two orthogonal axes** so an agent or CI can run a
single feature slice instead of the whole suite.

### Axis 1 — Domain (required, exactly one per fixture)

The feature area under test. Pick the *primary* one; the test name carries the
finer detail.

| Domain       | Covers                                                        |
|--------------|---------------------------------------------------------------|
| `AI`         | Perception + utility/state selection (Scout, UtilityChooser)  |
| `MPC`        | Model-predictive nav: solver, boost, LOS cost, navigator loop |
| `Sectors`    | Sector composition/lifecycle, manifest sync, respawn policy   |
| `Weapons`    | Weapon dispatch, heat, missile guidance                       |
| `Targeting`  | Lock-on sensor/registry wiring                                |
| `Objectives` | Objective tracker/channel, key pickup pipeline                |
| `Camera`     | Camera follow + camera utils                                  |
| `UI`         | HUD/indicator lifecycle, event-driven UI/audio                |
| `Damage`     | DamageController routing, respawn health/shield               |
| `Physics`    | Forces, fragnetics, collision-damage math                     |
| `Movement`   | Ship sim/kinematics contract (sim-invariance characterization) |
| `Core`       | Foundational spatial/context plumbing (GamePlane, decoupling) |
| `Services`   | Game service contracts                                        |
| `Bootstrap`  | Bootstrap/wiring contracts                                    |
| `Ships`      | Ship composition/activation lifecycle                         |

### Axis 2 — Speed (optional overlay)

| Tag     | Meaning                                                             |
|---------|--------------------------------------------------------------------|
| `Smoke` | Curated, fast, representative — the gating subset. Usually method-level. |
| `Slow`  | Multi-second wall-clock. Skip in tight iteration loops.            |

A fixture may carry `Slow` while still exposing one `Smoke` method (the smoke
test is the fast representative of an otherwise-slow suite).

> **Not used:** `Regression` (dropped — every test guards a regression, so it
> carried no selective value) and `Integration` (dropped — it mirrored "is a
> PlayMode test"; select that with `-Mode PlayMode`).

Run a slice — `-TestCategory` forwards straight to Unity's NUnit filter and
needs no scope-map upkeep:

```powershell
.\scripts\unity_test_agent.ps1 -Mode Both -TestCategory Weapons   # one domain
.\scripts\unity_test_agent.ps1 -Mode Both -TestCategory Smoke     # fast gating subset
.\scripts\unity_test_agent.ps1 -Mode PlayMode -TestCategory Sectors
```

---

## Namespaces

| Assembly              | Namespace        |
|-----------------------|------------------|
| `Tests.EditMode`      | `Tests.EditMode` |
| `Tests.PlayMode`      | `Tests.PlayMode` |

---

## Adding New Tests

### EditMode test checklist
- ✅ **File and class must match exactly** (e.g., `MyFeatureTests.cs` / `public class MyFeatureTests`)
- ✅ **Both must end with `Tests`** (enforced by `scripts/check_test_naming.ps1`)
- Namespace: `Tests.EditMode`
- Exactly one **domain** `[Category(...)]` on the class (see the table above)
- Add `[Category("Smoke")]` to the single fastest / most critical test method

### PlayMode test checklist
- ✅ **File and class must match exactly and end with `Tests`**
- Namespace: `Tests.PlayMode`
- Exactly one **domain** `[Category(...)]` on the class; add `[Category("Slow")]` when the suite takes > ~1s of wall-clock
- **No `AssetDatabase` calls outside `#if UNITY_EDITOR`** — wrap them:
  ```csharp
  #if UNITY_EDITOR
      using UnityEditor;
  #endif
  // ...
  [SetUp] public void SetUp() {
  #if UNITY_EDITOR
      var prefab = AssetDatabase.LoadAssetAtPath<MyType>("Assets/...");
  #else
      Assert.Ignore("Requires Unity Editor");
  #endif
  }
  ```
- **Avoid `yield return new WaitForSeconds(N)`** for pass/fail decisions.  
  Use a polling loop with a timeout instead:
  ```csharp
  var deadline = Time.realtimeSinceStartup + timeoutSec;
  while (!conditionMet && Time.realtimeSinceStartup < deadline)
      yield return new WaitForFixedUpdate();
  Assert.That(condition, Is.True, "Condition not met within timeout");
  ```

### Retiring tests
Retire a test by **deleting it** — git history preserves it if you ever need it
back. To *temporarily* quarantine a flaky or known-broken test without deleting
it, mark the method `[Ignore("reason — link/ticket")]` so it shows as skipped
rather than red (see `MissileGuidancePlayModeTests.Target90Degrees_Converges`).

---

## Native Unity profiling

Use the Development Player profiler harness for frame-time investigations:

```powershell
./scripts/unity_profile.ps1 -Label baseline -WarmupFrames 300 -SampleFrames 1200
./scripts/unity_profile.ps1 -Label candidate -WarmupFrames 300 -SampleFrames 1200
```

`-ExtraShips 6` adds six fully wired enemies to the normal player-plus-enemy CombatSector workload. The harness coordinates Unity access, builds a temporary profiling scene without modifying the checked-in scene, records native `.raw` CPU/GPU/Rendering/Memory/Physics/UI data, and emits a JSON distribution and spike-marker summary under `results/profiling/`.

Keep scenario, resolution, quality, warmup, frame count, and extra-ship count identical between baseline and candidate. Use tests as regression coverage, not as a substitute for the native frame capture.

---

## Best practices

Distilled from the test-suite cleanup. Rules of thumb, not laws.

- **Push logic down to EditMode; reserve PlayMode for real integration.** If the
  behaviour is Time-free and doesn't need the game loop, physics, coroutines, or
  a prefab, it's an EditMode unit test. A `MonoBehaviour`'s pure logic is usually
  reachable in EditMode via `new GameObject().AddComponent<T>()` + a production
  init method (see below) — `Awake` doesn't run, so initialise explicitly.
  Reserve PlayMode for what genuinely needs it: physics/`OnTrigger`, activation
  lifecycle across `SetActive`, real-prefab wiring, and ship-identity
  (`ShipId`/`OnDeath`). Example split: `DamageControllerEditModeTests` (routing,
  reset, regen — fast) vs `ShipRespawnDamagePlayModeTests` (ship-id + hull-smoke).
- **Assert an observable effect, not "no exception."** Dispatching an event and
  checking nothing threw proves little. Assert the visible result —
  `CanvasGroup.alpha`, `Image.fillAmount`, a resource value. If the effect isn't
  observable, that's a seam gap in the production type, not a reason to weaken
  the test.
- **Make time deterministic.** Drive time-based logic through an injected
  `Tick(float dt)` against an internal clock rather than reading
  `Time.time`/`Time.deltaTime` directly. Tests then step time at zero wall-clock
  (see `Heat.Tick`, `RegenResource.Update(dt)`). This is both a test practice and
  a seam (below).
- **Perf tests assert a *generous* budget, not a tautology.** `solveMs > 0`
  catches nothing. A loose ceiling (~30× observed headroom) catches catastrophic
  regressions (Burst off, O(n²)) without flaking on slow CI. See
  `MpcPerformancePlayModeTests`.
- **Quarantine flakies, don't tolerate red.** `[Ignore("reason — ticket")]` so a
  known-flaky shows as skipped; fix the determinism separately.
- **Refactor tests verify-before-delete.** When moving/rewriting coverage: (1)
  regression-run the existing tests against the refactored production code to
  prove it's behaviour-neutral, (2) add the new coverage and see it green, (3)
  *then* delete the old. Never delete the guard before the replacement is proven.
- **Share fixtures, don't copy them.** Common helpers live in the `Tests.Common`
  assembly (referenced by both EditMode and PlayMode) or `Tests.PlayMode.Common`.
  One `StubShipRegistry`, one `TestDamage.Kill`, one reflection-free path — a
  third copy of a stub is a smell.

## Designing for testability (seams)

When a test reaches for reflection to set a private field, that's the signal —
**the production type is missing a seam.** Add the seam; don't write a nicer
reflection helper. How to choose the seam:

1. **Reuse an existing production config path first.** Before adding anything,
   check how the value is set in the game. `DamageController` is configured from
   a `ShipSettings` SO via `PopulateSettings(...)` — tests build a `ShipSettings`
   and call that same method. No new API, no reflection.
2. **Real capability → `public Configure(...)`.** If the seam is something
   production would plausibly use (weapon tuning, spawner setup, missile
   variants), make it a public runtime method. Tests just reuse it. See
   `Heat.Configure`, `Missile.Configure`, `RingSpawner.Configure`.
3. **Editor/test-only injection → `internal` + `.Editor.cs` partial.** If it's
   only ever set by the editor bake or a test (not production runtime), keep it
   `internal` and put it in a `#if UNITY_EDITOR` `partial` in an `Editor/`
   subfolder (repo convention). `[assembly: InternalsVisibleTo("Tests.EditMode")]`
   / `"Tests.PlayMode"` already exists in `AssemblyInfo.cs`. See
   `Sector.Editor.cs` (`SetManifest`, `SetLoadScene`).
4. **A seam lives on the class that owns the state.** A `partial` of class A
   cannot touch class B's `private` members, and `InternalsVisibleTo` only
   crosses *assembly* boundaries, not class boundaries. The setter for
   `SectorSettings.loadScene` had to be on `SectorSettings` (or the field moved).
5. **Put config where it varies.** Type-intrinsic config belongs on the
   template/prefab; per-instance/per-run config belongs in an overridable SO.
   Scene identity (`sceneName`/`loadScene`) moved onto the `Sector` template
   (every instance of a sector loads the same scene); `difficultySeed` stays in
   the per-entry `SectorSettings`. Getting this right often *removes* the need
   for a seam entirely.
6. **The `Tick(dt)` + internal-clock pattern** (for #3 above): keep a private
   `clock` advanced by `Tick(float dt)`; have `Update()` call
   `Tick(Time.deltaTime)`; measure delays against `clock`, not `Time.time`.
   Behaviour is identical in play, and fully deterministic under test.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `infra_error` in JSON, no XML produced | Unity crashed during import or compilation | Check `*.log` in the output dir for compile errors |
| PlayMode tests ignored at runtime | `AssetDatabase` unavailable outside editor | Wrap in `#if UNITY_EDITOR` and add `Assert.Ignore(...)` fallback |
| Test flaky / timing-dependent | `WaitForSeconds` without condition check | Convert to polling loop with timeout (see pattern above) |
| `-TestCategory Foo` runs nothing | Domain typo or fixture missing its domain tag | Check the domain table above; every fixture needs exactly one |
