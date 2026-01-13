---
name: Utility Builder Refactor
overview: Introduce a declarative UtilityBuilder class to replace imperative utility calculations, making score composition transparent, uniform across all states, and debuggable.
todos:
  - id: create-builder
    content: Create UtilityBuilder class with fluent API and optional breakdown tracking
    status: completed
  - id: add-tuning-params
    content: Add missing base score parameters to UtilityTuning (extract hardcoded values from Utility.cs)
    status: completed
  - id: add-builder-presets
    content: Add static preset methods to UtilityBuilder for attack/evade base configurations
    status: completed
  - id: refactor-attack
    content: Refactor Attack.ComputeUtility to use UtilityBuilder
    status: completed
  - id: refactor-evade
    content: Refactor Evade.ComputeUtility to use UtilityBuilder
    status: completed
  - id: refactor-jink
    content: Refactor JinkEvade.ComputeUtility to use UtilityBuilder
    status: completed
  - id: refactor-orbit
    content: Refactor Orbit.ComputeUtility to use UtilityBuilder
    status: completed
  - id: refactor-kite
    content: Refactor Kite.ComputeUtility to use UtilityBuilder
    status: completed
  - id: refactor-idle-patrol
    content: Refactor Idle and Patrol to use UtilityBuilder (trivial cases)
    status: completed
  - id: cleanup
    content: Remove deprecated ComputeAttackUtility/ComputeEvadeUtility from Utility.cs
    status: completed
---

# Utility Builder Implementation Plan

## Overview

Replace the current imperative utility calculations with a fluent `UtilityBuilder` pattern. This will make each state's scoring logic self-documenting and provide optional breakdown tracking for debugging.

## Architecture

```mermaid
flowchart TD
subgraph states [State ComputeUtility Methods]
Attack
Evade
JinkEvade
Orbit
Kite
Idle
Patrol
end

subgraph builder [UtilityBuilder]
WithBase["WithBase()"]
AddDesire["AddDesire()"]
AddFear["AddFear()"]
AddIf["AddIf()"]
Build["Build() → Clamp01"]
end

subgraph tuning [UtilityTuning ScriptableObject]
BaseScores["Base Scores"]
Modifiers["Modifier Values"]
end

states --> builder
builder --> tuning