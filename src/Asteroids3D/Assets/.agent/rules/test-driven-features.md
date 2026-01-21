---
name: test-driven-features
description: Guidelines for writing tests alongside new features
alwaysApply: false
---

# Test-Driven Feature Development

When implementing new features, write accompanying tests to ensure correctness and prevent regressions.

## When to Write Tests

Write tests for:
- New gameplay mechanics (damage, movement, weapons, AI behavior)
- Bug fixes (regression tests to prevent reoccurrence)
- Refactoring of existing systems
- Complex calculations or algorithms

Skip tests for:
- Pure UI/visual changes
- Simple configuration changes
- Editor-only tooling

## Test Types by Feature

### Gameplay Systems
Use **PlayMode tests** with `TestServices`:

```csharp
[UnityTest]
public IEnumerator NewFeature_Behavior_ExpectedOutcome()
{
    var config = TestConfig.Load();
    var services = config.CreateServices();
    
    // Arrange - set up test conditions
    // Act - trigger the behavior
    yield return null; // Wait for physics/events
    
    // Assert - verify expected outcome
    Assert.AreEqual(expected, actual);
    
    services.Dispose();
}
```

### Pure Logic (No Unity Runtime)
Use **EditMode tests**:

```csharp
[Test]
public void Calculator_Method_ReturnsExpectedValue()
{
    var calc = new Calculator(settings);
    var result = calc.Compute(input);
    Assert.AreEqual(expected, result);
}
```

## Test Naming Convention

Use format: `{Method/Feature}_{Scenario}_{ExpectedBehavior}`

Examples:
- `MissileLaunch_FromStationaryShip_MissileMovesForward`
- `ShieldDamage_ExceedsShield_OnlyShieldTakesDamage`
- `Fragmentation_DestroyingAsteroid_CreatesFragments`

## Test Location

- PlayMode: `Assets/Tests/PlayMode/{FeatureName}PlayMode.cs`
- EditMode: `Assets/Tests/EditMode/{FeatureName}EditModeTests.cs`

## Running Tests

After implementing tests, run them via:
1. **Unity Editor:** Window → General → Test Runner
2. **Command line:** See `unity-testing.mdc` rule for batch execution
