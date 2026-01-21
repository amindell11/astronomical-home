---
name: Refactor MPC for Modularity
overview: Refactor the MPC (Model Predictive Control) steering system to use a modular, interface-based architecture. This will decouple the optimization logic from the physics model and cost functions, making it easier to experiment with different flight models and navigation constraints while following project best practices.
todos:
  - id: create-interfaces
    content: Create MpcInterfaces.cs with IMpcModel and IMpcCostFunction
    status: pending
  - id: create-settings
    content: Create MpcSettings.cs ScriptableObject for configuration and weights
    status: pending
  - id: implement-modules
    content: Implement StandardMpcModel.cs and StandardMpcCostFunction.cs
    status: pending
  - id: refactor-solver
    content: Refactor MpcController.cs to use the new interfaces
    status: pending
  - id: refactor-navigator
    content: Refactor MpcNavigator.cs and its Editor partial class to use the new modular system
    status: pending
---

# Refactor MPC for Modularity and Transparency

This plan refactors the MPC system in `Scripts/AI/Steering/MPC/` to follow SOLID principles and project conventions.

## 1. Abstractions and Settings

- Create [`Scripts/AI/Steering/MPC/MpcInterfaces.cs`](Scripts/AI/Steering/MPC/MpcInterfaces.cs) defining:
    - `IMpcModel`: Dynamics integration (`Step` function).
    - `IMpcCostFunction`: Trajectory evaluation logic.
- Create [`Scripts/AI/Steering/MPC/MpcSettings.cs`](Scripts/AI/Steering/MPC/MpcSettings.cs) as a `ScriptableObject` to hold weights, horizon, and sampling parameters.

## 2. Implementation Modules

- Implement [`Scripts/AI/Steering/MPC/StandardMpcModel.cs`](Scripts/AI/Steering/MPC/StandardMpcModel.cs):
    - Encapsulates ship physics (thrust, torque, damping, integration).
- Implement [`Scripts/AI/Steering/MPC/StandardMpcCostFunction.cs`](Scripts/AI/Steering/MPC/StandardMpcCostFunction.cs):
    - Calculates weighted costs for position, velocity, heading, effort, and obstacles.
    - Centralizes obstacle cost calculation for both solver and debug visualization.

## 3. Solver and Navigator Refactoring

- Refactor [`Scripts/AI/Steering/MPC/MpcController.cs`](Scripts/AI/Steering/MPC/MpcController.cs):
    - Remove hardcoded physics and costs.
    - Accept `IMpcModel` and `IMpcCostFunction` as parameters in the `Solve` method.
- Refactor [`Scripts/AI/Steering/MPC/MpcNavigator.cs`](Scripts/AI/Steering/MPC/MpcNavigator.cs):
    - Orchestrate the new modules.
    - Use `var`, fix Unity null checks, and follow concise coding standards.
- Harmonize [`Scripts/AI/Steering/MPC/Editor/MpcNavigator.Editor.cs`](Scripts/AI/Steering/MPC/Editor/MpcNavigator.Editor.cs):
    - Use the central `IMpcCostFunction` instance for gizmo rendering to ensure visualization matches AI intent.
```mermaid
graph TD
    Navigator[MpcNavigator] --> Settings[MpcSettings]
    Navigator --> Solver[MpcSolver/Controller]
    Navigator --> Model[IMpcModel: StandardMpcModel]
    Navigator --> Cost[IMpcCostFunction: StandardMpcCostFunction]
    Solver --> Model
    Solver --> Cost
    Editor[MpcNavigator.Editor] --> Cost
```