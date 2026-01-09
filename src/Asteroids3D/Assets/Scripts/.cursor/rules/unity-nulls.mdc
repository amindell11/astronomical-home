---
description: Use Unity-style null checks for UnityEngine.Object types
alwaysApply: false
---
# Unity Null Check Guidelines

Unity's `UnityEngine.Object` provides an implicit bool conversion that correctly handles destroyed objects. Use this idiomatic Unity style wherever possible.

## Prefer Implicit Bool Conversion

For any type deriving from `UnityEngine.Object` (MonoBehaviour, Component, GameObject, ScriptableObject, etc.):

```csharp
// ✅ PREFERRED - Unity's implicit bool conversion
if (myComponent) { }
if (!myComponent) { }

// ✅ ACCEPTABLE - Unity's overloaded operators
if (myComponent != null) { }
if (myComponent == null) { }
myComponent?.DoSomething();
var result = myComponent ?? fallback;

// ❌ AVOID - Bypasses Unity's lifetime check, won't detect destroyed objects
if (myComponent is null) { }
if (myComponent is not null) { }
```

## When Standard C# Null Checks Are Fine

Use `is null` freely for:
- Pure C# types (not inheriting from UnityEngine.Object)
- Interfaces (unless you know the concrete type is a Unity object)
- Value types, strings, collections, etc.

## Quick Reference

| Type | Use `if (obj)` / `?.` / `??` | Use `is null` |
|------|------------------------------|---------------|
| MonoBehaviour | ✅ | ❌ |
| Component | ✅ | ❌ |
| GameObject | ✅ | ❌ |
| ScriptableObject | ✅ | ❌ |
| Transform | ✅ | ❌ |
| Pure C# classes | N/A | ✅ |
