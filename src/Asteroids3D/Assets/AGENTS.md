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
!Warning! This does not include SerializeField Tooltips! Leave these alone!
### Implementation Guidelines

- Refactor complex logic into well-named helper methods
- Use language features to reduce boilerplate (LINQ, expression-bodied members, pattern matching)
- Delete code aggressively; less code = fewer bugs
- If you need a comment to explain code, first try to make the code clearer

---

## SOLID Principles & Unity Best Practices

Apply SOLID principles and Unity-specific best practices to all code in this project.

### Unity-Specific Best Practices

#### Component Design
- Keep MonoBehaviours lightweight; use them as orchestrators
- Avoid large Update() methods; split into focused update loops or event-driven logic
- Prefer composition over deep inheritance hierarchies

#### Expensive Unity Operations (STRICT)

**All expensive Unity operations must be called in `Awake()` only.** This includes:
- `GetComponent<T>()`, `GetComponentInChildren<T>()`, `GetComponentInParent<T>()`
- `GameObject.Find()`, `FindObjectOfType<T>()`, `FindGameObjectWithTag()`
- `Camera.main` (uses FindGameObjectWithTag internally)

These calls are **forbidden** in:
- `Start()`, `OnEnable()`, `Initialize()` methods
- `Update()`, `FixedUpdate()`, `LateUpdate()`
- Any method called at runtime

**Correct pattern:**
```csharp
private Rigidbody rb;
private AudioSource audioSource;

void Awake()
{
    rb = GetComponent<Rigidbody>();
    audioSource = GetComponentInChildren<AudioSource>();
}

public void Initialize(SomeDependency dep)
{
    // Only assign injected references, no GetComponent calls
    this.dependency = dep;
}
```

**For cross-object dependencies:** Use dependency injection from the composition root (GameInitiator), not runtime lookups.

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
###
Inverted if:
use inverted if statements where appropriate, minimizing nested code:
```csharp
//this:

if (!missileAmmoUI || !missileLauncher) return;
//do thing
return;

//not this
if(missileAmmoUI && missileLauncher) {
    //do thing
}
return;
```