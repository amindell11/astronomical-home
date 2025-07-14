# Behavior-Tree Action & Condition Decomposition

**Purpose** – break each AI state into *lego-sized* Behavior-Tree building blocks, highlighting reuse opportunities. Small, single-responsibility nodes make graphs easier to maintain, tune, and share across states.

> Legend  
> • **Action** = executes a motor skill (returns *Running* until finished)  
> • **Condition** = evaluates instantly (Success/Failure)  
> • **Reuse** = ⭐ already exists in codebase & can be shared across states

---

## 0. Shared Low-Level Actions (library)
| ID | Type | Purpose |
|----|------|---------|
| **MaintainHeading** | Action | Keep ship’s nose aligned with current velocity (used by Idle, Patrol)
| **ZeroThrust** | Action | Cut engines & drift (Idle, HideRepair)
| **PickRandomWaypoint** | Action | Choose point inside radius (⭐ `PatrolRandomAction`)
| **NavigateToPoint** | Action | Feed AIControl with waypoint & avoidance flag (⭐ part of `SeekTargetAction` – extract)
| **AcquireEnemy** | Condition/Action | Finds nearest enemy & writes to `BlackboardVariable<Ship>` (⭐ `AquireEnemiesAction`)
| **HasLineOfSight** | Condition | True if gun LOS clear (⭐ `HasLosCondition`)
| **ShieldBelowPct** | Condition | Low-shield check (⭐ `ShieldStateCondition`)
| **IsAlive** | Condition | Ship health > 0 (⭐ `IsAliveCondition`)
| **ComputeFleeVector** | Action | Pick opposite direction + distance (⭐ logic inside `EvadeAction` – extract)
| **FirePrimary** | Action | Toggle laser (simple wrapper)
| **FireSecondary** | Action | Toggle missile (simple wrapper)
| **WaitForSeconds** | Decorator | Hysteresis/minTimeInState helper

*(New actions may live in `Assets/Scripts/AI/BT/LowLevel/`)*

---

## 1. Current States

### Idle
```
Selector
 ├─ Sequence
 │   ├─ Condition  AcquireEnemy (fails if none)
 │   ├─ Condition  HasLineOfSight
 │   └─ Action     FirePrimary (brief pot-shot)
 └─ Action MaintainHeading + ZeroThrust
```
*Reuse*: `AcquireEnemy`, `HasLineOfSight`, `MaintainHeading`, `ZeroThrust`, `FirePrimary`.

### Patrol
```
Sequence
 ├─ Action PickRandomWaypoint      (⭐)
 ├─ Action NavigateToPoint         (reused by many)
 └─ Decorator WaitForSeconds(1-3)  (gives time on node)
```
*Reuse*: `PickRandomWaypoint`, `NavigateToPoint`, `WaitForSeconds`.

### Evade
```
Sequence
 ├─ Action ComputeFleeVector       (⭐ extract)
 ├─ Action NavigateToPoint (avoidance ON, maxSpeed)
 └─ Condition ShieldBelowPct? (decide when to leave state)
```
*Reuse*: `ComputeFleeVector`, `NavigateToPoint`, `ShieldBelowPct`.

### Attack
```
Selector (Utility ordered)
 ├─ Sequence  (Laser)
 │   ├─ Condition HasLineOfSight
 │   ├─ Action    NavigateToPoint (closing distance)
 │   └─ Action    FirePrimary
 ├─ Sequence  (Missile)
 │   ├─ Condition EnemyDistance < MissileRange
 │   └─ Action    FireSecondary
 └─ Action NavigateToPoint (pursue / strafe)
```
*Reuse*: `HasLineOfSight`, `NavigateToPoint`, `FirePrimary`, `FireSecondary`.

---

## 2. Planned States (Behavior_Upgrades.md)

| Planned State | Suggested BT Sub-Tree | Reused Low-Level Nodes |
|---------------|-----------------------|------------------------|
| **ScanPatrol** | Sequence → PickRandomWaypoint → NavigateToPoint → Action **RadarSweep** (new) | PickRandomWaypoint, NavigateToPoint |
| **Investigate** | Sequence → Action **MoveToLastSeen** → Condition HasLineOfSight | AcquireEnemy, NavigateToPoint, HasLineOfSight |
| **PredictivePursue** | Sequence → Action **LeadInterceptCourse** (new) → FirePrimary | NavigateToPoint, FirePrimary |
| **StrafeRun** | Sequence → Action **SetOrbitPoint** (new) → NavigateToPoint (orbit) → FirePrimary | NavigateToPoint, FirePrimary |
| **KiteRun** | Sequence → ComputeFleeVector (but maintain LOS) → FirePrimary (rear) | ComputeFleeVector, FirePrimary |
| **LowShieldEvade** | (same as Evade) but exit condition ShieldAbovePct | ComputeFleeVector, NavigateToPoint, ShieldBelowPct |
| **HideRepair** | Sequence → Action **FindAsteroidCover** (new) → NavigateToPoint → ZeroThrust → Condition ShieldAbovePct | NavigateToPoint, ZeroThrust, ShieldBelowPct |
| **Reposition** | Sequence → Action **SampleLowDensityWaypoint** (new) → NavigateToPoint | NavigateToPoint |
| **Finisher** | Selector → Condition EnemyHealth < X → Action **ChargeTarget** (new) → FirePrimary/Secondary | HasLineOfSight, FirePrimary, FireSecondary, NavigateToPoint |
| **GroupRegroup** | Sequence → Action **ComputeFriendCentroid** (new) → NavigateToPoint | NavigateToPoint |

### Reuse Hot-spots
1. **NavigateToPoint** – core of almost every state. Parametrise: `avoidance`, `speedFactor`, `orbitR`…
2. **AcquireEnemy** – used by Attack, Finisher, Investigate, PredictivePursue.
3. **ComputeFleeVector** – Evade, KiteRun, LowShieldEvade.
4. **HasLineOfSight** condition shared by Attack, Finisher, Investigate, PredictivePursue.
5. **FirePrimary/Secondary** – trivial wrapper reusable everywhere.

---

## 3. Implementation Roadmap
1. Extract reusable bits from existing monolithic Actions into the low-level library (especially navigation & flee logic).  
2. Replace inner code of `EvadeAction`, `PatrolRandomAction`, etc., with compositions of new nodes.  
3. Create parameterized `NavigateToPoint` Action that accepts a Blackboard waypoint (set by preceding node).  
4. Build planned state sub-trees incrementally, plugging into current UtilitySelector.

This decomposition keeps Behavior Trees, maximizes node reuse, and sets the stage for RL biases to tweak individual tunables rather than whole states. 