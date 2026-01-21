---
name: unity-testing
description: How to run Unity tests from command line and test architecture overview
alwaysApply: false
---

# Running Unity Tests

## Command Line Test Execution

Unity tests can be run from PowerShell when the Unity Editor is **not** open:

```powershell
$unityPath = "D:\Programs\Unity\Editor\6000.1.8f1\Editor\Unity.exe"
$projectPath = "D:\amind\git\astronomical-home\src\Asteroids3D"
$resultsFile = "$env:TEMP\test_results.xml"
$logFile = "$env:TEMP\unity_test_log.txt"

# Run PlayMode tests
& $unityPath -batchmode -projectPath $projectPath -runTests -testPlatform PlayMode -testResults $resultsFile -logFile $logFile

# Run EditMode tests
& $unityPath -batchmode -projectPath $projectPath -runTests -testPlatform EditMode -testResults $resultsFile -logFile $logFile
```

## Parsing Test Results (Token-Efficient)

```powershell
# Concise summary - truncate failure messages to reduce token usage
[xml]$r = Get-Content $resultsFile
Write-Host "Pass:$($r.'test-run'.passed) Fail:$($r.'test-run'.failed)"
$r.SelectNodes("//test-case[@result='Failed']") | % {
    $msg = $_.failure.message.InnerText -replace '\s+', ' '
    if ($msg.Length -gt 120) { $msg = $msg.Substring(0,120) + "..." }
    Write-Host "FAIL $($_.name): $msg"
}
```

**Best practices for token efficiency:**
- Use `-Tail 30` when reading log files (not full content)
- Truncate failure messages to ~120 chars
- Use short test names in output (`.name` not `.fullname`)
- Avoid printing stack traces unless debugging specific failures

## Test Architecture

### Test Locations
- **PlayMode tests:** `Assets/Tests/PlayMode/` - Tests that require Unity runtime
- **EditMode tests:** `Assets/Tests/EditMode/` - Pure logic tests without runtime

### Key Test Infrastructure

- **`TestServices`** - Bootstrapper for ship-based tests using `Factory.CreateShip()`
- **`TestConfig`** - ScriptableObject at `Assets/Tests/PlayMode/TestConfig.asset` with prefab references
- **`TestSceneBuilder`** - Utilities for arena creation and positioning

### Creating Test Services

```csharp
[SetUp]
public void SetUp()
{
    var config = TestConfig.Load();
    services = config.CreateServices(separation: 20f);
}

[TearDown]
public void TearDown()
{
    services?.Dispose();
}
```

### Test Config Setup

The `TestConfig.asset` must reference:
- Player/Enemy ship prefabs (`Assets/Prefabs/Ships/Ship_*.prefab`)
- Commander prefabs (`Assets/Prefabs/Ships/Pilots/*.prefab`)
- Ship settings (`Assets/Prefabs/Ships/DefaultSettings.asset`)

## Checking Logs (If No Results)

```powershell
# Only read last 30 lines to check for errors - avoid full log
Get-Content $logFile -Tail 30
```

## Important Notes

1. **Unity must be closed** to run tests from command line (projects lock)
2. **Tests take 60-120 seconds** - wait with `Start-Sleep -Seconds 90` before parsing
3. If results file missing, check log tail for compilation errors
4. PlayMode tests require the test scene to be buildable
