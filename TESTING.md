# Testing Guide — Asteroids3D

Agent-friendly reference for running, interpreting, and extending the Unity test suite.

---

## Quick Start (Agent / CI)

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

### Parameters

| Parameter          | Default                                    | Description                              |
|--------------------|--------------------------------------------|------------------------------------------|
| `-UnityPath`       | `D:\Programs\Unity\Editor\6000.1.8f1\...` | Path to `Unity.exe`                      |
| `-ProjectPath`     | `src/Asteroids3D`                          | Unity project root                       |
| `-OutDir`          | `results/unity-tests-agent`                | Output directory for XML + JSON          |
| `-Mode`            | `Both`                                     | `Both`, `EditMode`, or `PlayMode`        |
| `-TestFilter`      | *(all)*                                    | NUnit filter string (e.g. `Category=Smoke`) |
| `-MaxFailures`     | `25`                                       | Max failures to include in JSON          |
| `-IncludeStackTrace` | off                                      | Include stack traces in JSON output      |

### Example — run only Smoke tests

```powershell
.\scripts\unity_test_agent.ps1 -Mode EditMode -TestFilter "Category=Smoke"
```

### Example — run both modes, full stack traces on failure

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
├── EditMode/                          # Compiled as Tests.EditMode.asmdef (Editor only)
│   ├── ForcesEditModeTests.cs         # Forces.ComputeOutputs — pure math
│   ├── CameraUtilsEditModeTests.cs    # CameraUtils — pure math
│   ├── CameraUtilsEditMode.cs         # Empty stub — preserved for GUID continuity
│   ├── CollisionDamageUtilityTests.cs # Kinetic energy / damage formulae
│   └── FragneticsCalculatorEditModeTests.cs  # Asteroid fragmentation physics
│
└── PlayMode/                          # Compiled as Tests.PlayMode.asmdef
    ├── TestSceneBuilder.cs            # Scene-building utilities (not a test class)
    ├── CameraFollowPlayModeTests.cs   # ObserverCam follow behaviour
    ├── GamePlanePlayModeTests.cs      # GamePlane coordinate transforms
    ├── NavigatorPlayModeTests.cs      # Ship navigation to waypoints
    ├── MpcNavigatorPlayModeTests.cs   # MPC-based ship navigation
    ├── ScannerPlayModeTests.cs        # AI obstacle scanner
    └── Deprecated/                    # ⛔ NOT compiled (own asmdef gated by
        │                              #    UNITY_INCLUDE_DEPRECATED_TESTS symbol)
        ├── Tests.PlayMode.Deprecated.asmdef
        ├── ActuatorPlayModeTests.cs
        ├── AsteroidSpawningPlayMode.cs
        ├── LaserDamagePlayMode.cs
        ├── MissileHomingPlayMode.cs
        ├── MissileLaunchPlayMode.cs
        └── ShieldRegenerationPlayMode.cs
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

## NUnit Categories

| Category      | Meaning                                              | Where used              |
|---------------|------------------------------------------------------|-------------------------|
| `Smoke`       | Fast sanity check; run on every push                 | EditMode + PlayMode     |
| `Regression`  | Full correctness coverage; run on PRs                | EditMode                |
| `Integration` | Multi-system / scene-based tests                     | PlayMode                |
| `Slow`        | Tests that take > ~1s; skip in tight inner loops     | PlayMode + some EditMode|

Run a specific category:

```powershell
.\scripts\unity_test_agent.ps1 -TestFilter "Category=Smoke"
.\scripts\unity_test_agent.ps1 -TestFilter "Category=Regression"
.\scripts\unity_test_agent.ps1 -Mode PlayMode -TestFilter "Category=Integration"
```

---

## Namespaces

| Assembly              | Namespace        |
|-----------------------|------------------|
| `Tests.EditMode`      | `Tests.EditMode` |
| `Tests.PlayMode`      | `Tests.PlayMode` |
| `Tests.PlayMode.Deprecated` | `Tests.PlayMode.Deprecated` *(not compiled by default)* |

---

## Adding New Tests

### EditMode test checklist
- ✅ **File and class must match exactly** (e.g., `MyFeatureTests.cs` / `public class MyFeatureTests`)
- ✅ **Both must end with `Tests`** (enforced by `scripts/check_test_naming.ps1`)
- Namespace: `Tests.EditMode`
- At least one `[Category("Regression")]` on the class
- Add `[Category("Smoke")]` to the single fastest / most critical test method

### PlayMode test checklist
- ✅ **File and class must match exactly and end with `Tests`**
- Namespace: `Tests.PlayMode`
- Class-level `[Category("Integration")]`; add `[Category("Slow")]` when the test takes > ~1s
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

### Deprecated tests
If a test needs to be retired but kept for reference, move it to `PlayMode/Deprecated/`.  
The `Tests.PlayMode.Deprecated.asmdef` gate (`defineConstraints: ["UNITY_INCLUDE_DEPRECATED_TESTS"]`)  
ensures they are **never compiled** unless the symbol is explicitly defined.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `infra_error` in JSON, no XML produced | Unity crashed during import or compilation | Check `*.log` in the output dir for compile errors |
| PlayMode tests ignored at runtime | `AssetDatabase` unavailable outside editor | Wrap in `#if UNITY_EDITOR` and add `Assert.Ignore(...)` fallback |
| Test flaky / timing-dependent | `WaitForSeconds` without condition check | Convert to polling loop with timeout (see pattern above) |
| Deprecated tests compiling unexpectedly | File placed in wrong folder | Move to `PlayMode/Deprecated/`; ensure `Tests.PlayMode.Deprecated.asmdef` is present |
