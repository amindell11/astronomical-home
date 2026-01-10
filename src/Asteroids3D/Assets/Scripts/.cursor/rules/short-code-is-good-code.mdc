---
name: short-code-is-good-code
description: When to apply: All code writing and refactoring tasks. Relevant for: implementing features, optimizing performance, code reviews, or simplifying existing code.
---

# Code Conciseness & Clarity



---

## Core Principles

### Conciseness
- Write the minimum code necessary to achieve the objective
- Eliminate redundancy, boilerplate, and unnecessary abstractions
- If it can be done in fewer lines without sacrificing clarity, do it
- Don't abstract prematurely; wait until patterns emerge

### Clarity
- Code should be self-documenting through descriptive naming
- Variable and method names should make the code's purpose obvious
- Prefer explicit over clever; readability over brevity
- Structure code so intent is immediately clear

### Efficiency
- Minimize computational overhead and memory allocations
- Choose appropriate data structures and algorithms
- Avoid premature optimization, but don't write obviously inefficient code
- Remove dead code, unused variables, and unnecessary operations

## Comments: A Last Resort

**Comments represent a failure of the code to be self-explanatory.**

### When comments ARE acceptable:
- Explaining non-obvious algorithmic complexity or mathematical concepts
- Documenting WHY a counterintuitive approach was chosen (not WHAT it does)
- Clarifying external constraints, API quirks, or workarounds for bugs
- Public API documentation (method/class headers for external consumers)

### When comments are NOT acceptable:
- Describing what code does (the code should show this)
- Explaining poorly named variables or functions (rename them instead)
- Compensating for unclear logic (refactor the logic instead)
- Redundant explanations of obvious operations

### Fix the code, not with comments:
```csharp
// BAD: Using comment to explain unclear code
// Get the player's current health percentage
float h = p.ch / p.mh * 100;

// GOOD: Self-documenting code
float healthPercentage = player.currentHealth / player.maxHealth * 100;
```

## Implementation Guidelines

- Refactor complex logic into well-named helper methods
- Use language features to reduce boilerplate (LINQ, expression-bodied members, pattern matching)
- Delete code aggressively; less code = fewer bugs
- If you need a comment to explain code, first try to make the code clearer
