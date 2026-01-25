# Project Rules

## Code Conciseness & Clarity

### Core Principles

#### Conciseness
- Write the minimum code necessary to achieve the objective
- Eliminate redundancy, boilerplate, and unnecessary abstractions
- If it can be done in fewer lines without sacrificing clarity, do it
- Don't abstract prematurely; wait until patterns emerge

#### Clarity
- Code should be self-documenting through descriptive naming
- Variable and method names should make the code's purpose obvious
- Prefer explicit over clever; readability over brevity
- Structure code so intent is immediately clear

#### Efficiency
- Minimize computational overhead and memory allocations
- Choose appropriate data structures and algorithms
- Avoid premature optimization, but don't write obviously inefficient code
- Remove dead code, unused variables, and unnecessary operations

### Comments: A Last Resort

**Comments represent a failure of the code to be self-explanatory.**

#### When comments ARE acceptable:
- Explaining non-obvious algorithmic complexity or mathematical concepts
- Documenting WHY a counterintuitive approach was chosen (not WHAT it does)
- Clarifying external constraints, API quirks, or workarounds for bugs
- Public API documentation (method/class headers for external consumers)

#### When comments are NOT acceptable:
- Describing what code does (the code should show this)
- Explaining poorly named variables or functions (rename them instead)
- Compensating for unclear logic (refactor the logic instead)
- Redundant explanations of obvious operations

#### Fix the code, not with comments:
```csharp
// BAD: Using comment to explain unclear code
// Get the player's current health percentage
float h = p.ch / p.mh * 100;

// GOOD: Self-documenting code
float healthPercentage = player.currentHealth / player.maxHealth * 100;
```

### Implementation Guidelines

- Refactor complex logic into well-named helper methods
- Use language features to reduce boilerplate (LINQ, expression-bodied members, pattern matching)
- Delete code aggressively; less code = fewer bugs
- If you need a comment to explain code, first try to make the code clearer

---

## SOLID Principles & Unity Best Practices

Apply SOLID principles and Unity-specific best practices to all code in this project.

### SOLID Principles

#### Single Responsibility Principle (SRP)
- Each class should have one clearly defined responsibility
- MonoBehaviours should focus on Unity lifecycle management; delegate business logic to separate classes
- Separate data, logic, and presentation concerns

#### Open/Closed Principle (OCP)
- Design classes to be extensible without modification
- Use inheritance, interfaces, and composition over hardcoding behavior
- Prefer ScriptableObjects for data-driven design and configuration

#### Liskov Substitution Principle (LSP)
- Derived classes must be substitutable for their base classes
- Ensure interface implementations are fully compatible
- Avoid breaking base class contracts in derived classes

#### Interface Segregation Principle (ISP)
- Create focused, specific interfaces rather than monolithic ones
- Don't force classes to implement methods they don't need
- Use multiple small interfaces over one large interface

#### Dependency Inversion Principle (DIP)
- Depend on abstractions (interfaces/abstract classes), not concrete implementations
- Use dependency injection where appropriate
- Decouple systems through events, delegates, or UnityEvents

### Unity-Specific Best Practices

#### Component Design
- Keep MonoBehaviours lightweight; use them as orchestrators
- Avoid large Update() methods; split into focused update loops or event-driven logic
- Use GetComponent sparingly; cache references in Awake() or Start()
- Prefer composition over deep inheritance hierarchies

#### Performance
- Use object pooling for frequently instantiated/destroyed objects
- Minimize allocations in Update/FixedUpdate (avoid new, LINQ in hot paths)
- Use structs for small data types to reduce heap allocations
- Cache expensive operations (GetComponent, Find, Transform access)

#### Architecture Patterns
- Use ScriptableObjects for shared data and configuration
- Implement events/observer pattern for decoupled communication
- Consider Service Locator or Dependency Injection for cross-cutting concerns
- Separate game logic from Unity-specific code where possible

#### Code Organization
- Use namespaces to organize code by feature/system
- Follow consistent naming conventions (PascalCase for public, camelCase for private)
- Place serialized fields at the top of MonoBehaviour classes
- Use [SerializeField] for private fields that need inspector exposure

#### Best Practices
- Avoid singleton pattern unless absolutely necessary; prefer dependency injection
- Use coroutines for time-based behavior, async for I/O operations
- Implement proper cleanup in OnDestroy/OnDisable
- Use Unity's null-coalescing operator carefully (Unity's fake null)
- Prefer unidirectional data flow to avoid circular dependencies

---

## Test-Driven Feature Development

When implementing new features, write accompanying tests to ensure correctness and prevent regressions.

### When to Write Tests

Write tests for:
- New gameplay mechanics (damage, movement, weapons, AI behavior)
- Bug fixes (regression tests to prevent reoccurrence)
- Refactoring of existing systems
- Complex calculations or algorithms

Skip tests for:
- Pure UI/visual changes
- Simple configuration changes
- Editor-only tooling

### Test Types by Feature

#### Gameplay Systems
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

#### Pure Logic (No Unity Runtime)
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

### Test Naming Convention

Use format: `{Method/Feature}_{Scenario}_{ExpectedBehavior}`

Examples:
- `MissileLaunch_FromStationaryShip_MissileMovesForward`
- `ShieldDamage_ExceedsShield_OnlyShieldTakesDamage`
- `Fragmentation_DestroyingAsteroid_CreatesFragments`

### Test Location

- PlayMode: `Assets/Tests/PlayMode/{FeatureName}PlayMode.cs`
- EditMode: `Assets/Tests/EditMode/{FeatureName}EditModeTests.cs`

---

## Unity Null Check Guidelines

Unity's `UnityEngine.Object` provides an implicit bool conversion that correctly handles destroyed objects. Use this idiomatic Unity style wherever possible.

### Prefer Implicit Bool Conversion

For any type deriving from `UnityEngine.Object` (MonoBehaviour, Component, GameObject, ScriptableObject, etc.):

```csharp
// PREFERRED - Unity's implicit bool conversion
if (myComponent) { }
if (!myComponent) { }

// ACCEPTABLE - Unity's overloaded operators
if (myComponent != null) { }
if (myComponent == null) { }
myComponent?.DoSomething();
var result = myComponent ?? fallback;

// AVOID - Bypasses Unity's lifetime check, won't detect destroyed objects
if (myComponent is null) { }
if (myComponent is not null) { }
```

### When Standard C# Null Checks Are Fine

Use `is null` freely for:
- Pure C# types (not inheriting from UnityEngine.Object)
- Interfaces (unless you know the concrete type is a Unity object)
- Value types, strings, collections, etc.

### Quick Reference

| Type | Use `if (obj)` / `?.` / `??` | Use `is null` |
|------|------------------------------|---------------|
| MonoBehaviour | Yes | No |
| Component | Yes | No |
| GameObject | Yes | No |
| ScriptableObject | Yes | No |
| Transform | Yes | No |
| Pure C# classes | N/A | Yes |

---

## Running Unity Tests

### Command Line Test Execution

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

### Parsing Test Results (Token-Efficient)

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

### Test Architecture

#### Test Locations
- **PlayMode tests:** `Assets/Tests/PlayMode/` - Tests that require Unity runtime
- **EditMode tests:** `Assets/Tests/EditMode/` - Pure logic tests without runtime

#### Key Test Infrastructure

- **`TestServices`** - Bootstrapper for ship-based tests using `Factory.CreateShip()`
- **`TestConfig`** - ScriptableObject at `Assets/Tests/PlayMode/TestConfig.asset` with prefab references
- **`TestSceneBuilder`** - Utilities for arena creation and positioning

#### Creating Test Services

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

#### Test Config Setup

The `TestConfig.asset` must reference:
- Player/Enemy ship prefabs (`Assets/Prefabs/Ships/Ship_*.prefab`)
- Commander prefabs (`Assets/Prefabs/Ships/Pilots/*.prefab`)
- Ship settings (`Assets/Prefabs/Ships/DefaultSettings.asset`)

### Checking Logs (If No Results)

```powershell
# Only read last 30 lines to check for errors - avoid full log
Get-Content $logFile -Tail 30
```

### Important Notes

1. **Unity must be closed** to run tests from command line (projects lock)
2. **Tests take 60-120 seconds** - wait with `Start-Sleep -Seconds 90` before parsing
3. If results file missing, check log tail for compilation errors
4. PlayMode tests require the test scene to be buildable
