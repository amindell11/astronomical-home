# Behavior Tree Upgrades & RL-Ready AI Action Palette

**Author:** AI assistant GPT-4o • **Date:** {{DATE}}

## 1. Objective
Provide a concrete, incremental roadmap for augmenting the current `Unity.Behavior`-based AI with a richer set of tactical states. The goal is to  ❖ keep deterministic, designer-authored behaviour for cinematic polish, **and** ❖ expose clear knobs that a future RL layer (e.g., ML-Agents) can learn to manipulate.

## 2. Target Architecture
1. **Utility Selector Wrapper** – Wrap existing BT root in a *utility selector* that selects the highest-scoring state node every tick.
2. **Context Struct** – Compute a lightweight `AIContext` struct once per frame and cache it on the blackboard:  
   ```csharp
   struct AIContext {
       public float shieldPct;
       public float relDistance;
       public float relSpeed;
       public bool  lineOfSight;
       public bool  incomingMissile;
       public int   nearbyFriendCount;
       // … add as required
   }
   ```
3. **State Nodes** – Each new state lives in its own subtree (one `Action` + support `Condition`s) with:
   * `float Score(AIContext ctx)` method (interface default)
   * Tunable parameters kept in a serialisable `StateParams` asset
4. **RL Bridge** – Expose **(a)** a discrete *state bias* tensor (size = N states) and **(b)** optional continuous addends for each tunable in `StateParams`.

![High-level BT Diagram](./bt_diagram_placeholder.png)

## 3. State-by-State Implementation Checklist
| ID | Node(s) to add | Key Logic | Blackboard Writes | Tunables |
|---|---|---|---|---|
| ScanPatrol | `ScanPatrolAction` | Follow spline, periodic radar sweep | `Waypoint` | spacing, sweepSpeed |
| Investigate | `InvestigateAction` | Fly to last-seen pos, pivot search | `TargetPoint` | interceptSpeed, searchTime |
| PredictivePursue | `PredictivePursueAction` | Lead shot, choose strafe vs tail-chase | `FirePermission` | leadMult, strafeRadius |
| StrafeRun | `OrbitAction` | Constant-radius circle, salvo fire | — | orbitR, orbitDir, salvoLen |
| KiteRun | `KiteAction` | Accelerate away, backfire | — | kiteDist, reengageDist |
| LowShieldEvade | `JinkEvadeAction` + `BoostAction` | Zig-zag, boost, asteroid bias | — | jinkAmp, obstacleWeight |
| HideRepair | `HideAction` | Stop near asteroid shadow, regen | — | hideDist, exitShieldPct |
| Reposition | `RepositionAction` | Sample low-density waypoint | `Waypoint` | sampleCount, repositionDist |
| Finisher | `FinisherAction` | Aggressive charge, loosen avoidance | `AvoidanceScale` | aggressionFlag |
| GroupRegroup | `RegroupAction` | Spiral toward centroid | `FormationPoint` | regroupRadius |

## 4. Integration Steps
1. **Baseline Refactor (1 day)**  
   • Move current `Attack`/`Evade`/`Patrol` sub-trees under a temporary *FallbackSelector*.  
   • Add empty placeholders for new states so RL tensors have fixed size from day 1.
2. **Context Provider (0.5 day)**  
   • Implement `AIContextProvider : MonoBehaviour` that fills blackboard fields.
3. **Utility Selector (0.5 day)**  
   • Implement `UtilitySelectorNode` inheriting `Composite`.  
   • Evaluation order: compute `Score`, pick max, tick selected child.
4. **State Node Rollout (4 days)**  
   _Repeated per state:_ skeleton → params asset → editor gizmos → unit test.
5. **Debug Overlay (0.5 day)**  
   • Draw current state & major param values via `OnGUI`.
6. **Tune & QA (2 days)**  
   • Manual play-test each state; adjust arrival radii, etc.

## 5. Data Contract for RL
```text
Observation Size = 32 (existing) + |AIContext|
Action Discrete  = N_STATES (select bias)
Action Continuous= Σ tunables selected (optional)
Reward Shaping   = +0.01 * (desired micro-objective) per state
```

## 6. Milestones
1. **M1 – New BT skeleton & selector ✅ COMPLETE (Jun 22)**
2. **M2 – Patrol & Investigate functional (Day 4)**
3. **M3 – Pursue, Strafe, Kite (Day 6)**
4. **M4 – Defensive suite (Evade, Hide, Reposition) (Day 8)**
5. **M5 – Group & Finisher, polish (Day 10)**
6. **M6 – ML-Agents glue code stub (Day 11)**

## 7. Risks & Mitigations
* **BT bloat** – Use param assets to avoid inspector clutter.  
* **Selector thrashing** – Add hysteresis (`minTimeInState`) in `UtilitySelector`.  
* **Perf** – Cache expensive raycasts; step `GroupRegroup` only every N frames.

---
© 2025 LMU CMSI-5998 • Feel free to modify or extend. 