---
description: #SOLID Principles & Unity Best Practices: When to apply: Use this rule for all Unity C# code creation, refactoring, and architecture discussions. Relevant for: designing new classes/systems, reviewing code structure, debugging architecture issues, optimizing performance, or discussing Unity-specific implementations.

alwaysApply: false
---
# SOLID Principles & Unity Best Practices

Apply SOLID principles and Unity-specific best practices to all code in this project.

## SOLID Principles

### Single Responsibility Principle (SRP)
- Each class should have one clearly defined responsibility
- MonoBehaviours should focus on Unity lifecycle management; delegate business logic to separate classes
- Separate data, logic, and presentation concerns

### Open/Closed Principle (OCP)
- Design classes to be extensible without modification
- Use inheritance, interfaces, and composition over hardcoding behavior
- Prefer ScriptableObjects for data-driven design and configuration

### Liskov Substitution Principle (LSP)
- Derived classes must be substitutable for their base classes
- Ensure interface implementations are fully compatible
- Avoid breaking base class contracts in derived classes

### Interface Segregation Principle (ISP)
- Create focused, specific interfaces rather than monolithic ones
- Don't force classes to implement methods they don't need
- Use multiple small interfaces over one large interface

### Dependency Inversion Principle (DIP)
- Depend on abstractions (interfaces/abstract classes), not concrete implementations
- Use dependency injection where appropriate
- Decouple systems through events, delegates, or UnityEvents

## Unity-Specific Best Practices

### Component Design
- Keep MonoBehaviours lightweight; use them as orchestrators
- Avoid large Update() methods; split into focused update loops or event-driven logic
- Use GetComponent sparingly; cache references in Awake() or Start()
- Prefer composition over deep inheritance hierarchies

### Performance
- Use object pooling for frequently instantiated/destroyed objects
- Minimize allocations in Update/FixedUpdate (avoid new, LINQ in hot paths)
- Use structs for small data types to reduce heap allocations
- Cache expensive operations (GetComponent, Find, Transform access)

### Architecture Patterns
- Use ScriptableObjects for shared data and configuration
- Implement events/observer pattern for decoupled communication
- Consider Service Locator or Dependency Injection for cross-cutting concerns
- Separate game logic from Unity-specific code where possible

### Code Organization
- Use namespaces to organize code by feature/system
- Follow consistent naming conventions (PascalCase for public, camelCase for private)
- Place serialized fields at the top of MonoBehaviour classes
- Use [SerializeField] for private fields that need inspector exposure

### Best Practices
- Avoid singleton pattern unless absolutely necessary; prefer dependency injection
- Use coroutines for time-based behavior, async for I/O operations
- Implement proper cleanup in OnDestroy/OnDisable
- Use Unity's null-coalescing operator carefully (Unity's fake null)
- Prefer unidirectional data flow to avoid circular dependencies