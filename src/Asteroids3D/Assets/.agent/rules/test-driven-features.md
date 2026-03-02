---
name: test-driven-features
description: Guidelines for writing and running Unity tests alongside new features
alwaysApply: false
---

# Test-Driven Feature Development

Write tests for new gameplay logic and bug fixes so regressions are caught quickly.

## When to write tests

Write tests for:
- gameplay behavior (combat, navigation, scanning, utility logic)
- bug fixes (add a regression test first)
- refactors that change logic flow
- deterministic math/utility code

Skip tests for:
- purely visual/UI polish
- inspector-only tweaks with no logic impact

## Test types

### EditMode (fast, logic-first)
Use for pure logic and deterministic helpers.

Location:
- `Assets/Scripts/Editor/Tests/EditMode/`

Example shape:
```csharp
[Test]
public void Feature_Scenario_ExpectedResult()
{
    // Arrange
    // Act
    // Assert
}
```

### PlayMode (runtime integration)
Use for frame/physics/runtime behavior.

Location:
- `Assets/Scripts/Editor/Tests/PlayMode/`

Example shape:
```csharp
[UnityTest]
public IEnumerator Feature_Scenario_ExpectedRuntimeBehavior()
{
    // Arrange
    // Act
    yield return null;
    // Assert
}
```

## Naming convention

`{FeatureOrMethod}_{Scenario}_{ExpectedBehavior}`

Examples:
- `CameraFollow_TargetMoves_CameraTracksTarget`
- `Navigator_ObstacleDetected_PathAdjusts`
- `CollisionDamageUtility_ImpulseHigh_DamageClamped`

## Running tests (agent-friendly)

Use the project runner:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\unity_test_agent.ps1 -Mode Both
```

For faster iteration:
- run `-Mode EditMode` or `-Mode PlayMode`
- use scope/filter arguments via extension tooling
- rerun failed tests only after a fix
