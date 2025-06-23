# **Implementation Sequencing – 22 → 25 June 2025**

All tasks below distill and re-prioritise the individual Feature Plans so that the whole stack (core optimisations → multi-arena RL pipeline → baseline AI upgrades → test coverage) lands by **EOD 25 Jun**.  Items are grouped by **target day** but may overlap—treat them as *latest-by* deadlines.

---

## **Sun 22 Jun – Foundations & Quick Wins** ✅ **COMPLETE**
• ✅ Apply *General Optimizations* § 1-4 to remove compile errors, debug spam, and allocation spikes.  (Ref: [General_Optimizations](Feature_Plans/General_Optimizations.md))
• ✅ Land *AI Performance* § 3 "Remove per-frame managed allocations" (string tags → layer masks, pooled arrays).  (Ref: [AI_Performance_Optimization](Feature_Plans/AI_Performance_Optimization.md))
• ✅ Begin *Asteroid Environment* anchor decoupling (**C-1, C-2**): add `spawnAnchor`, editor-gate gizmos.  (Ref: [Asteroid_Environment_Update](Feature_Plans/Asteroid_Environment_Update.md))
• ✅ Import ML-Agents 3.x & create stub `RLArbiter.cs` (**RL Plan U-1/U-2**).
• ✅ Start *Behavior Upgrades* **M1**: migrate current Attack/Evade/Patrol sub-trees under temporary `FallbackSelector`.  (Ref: [Behavior_Upgrades](Feature_Plans/Behavior_Upgrades.md))
• ✅ Kick-off *Testing Plan* § 3.1: scaffold EditMode test assembly & add first utility test (`CollisionDamageUtilityTests`).

## **Mon 23 Jun – Performance & Arena Infrastructure**
• Implement *AI Performance* § 1 "Physics & Visibility Queries": prototype batched `RaycastCommand` for ships. 
• Jobify `PathPlanner.Compute` into Burst `IJobParallelFor` (AI Perf § 2A-B). 
• Finish *Asteroid Environment* **C-3→C-5**: build `RLTrainingArena.prefab`, write `ArenaReset.cs` & `ArenaSpawner.cs`; confirm 4-arena head-less run ≥200 FPS.
• Wrap `CameraFollow` for batch-mode (**C-6**). 
• Extend test suite with `PathPlannerTests` + static code-coverage job in CI.
• Add `ShipEvents` & `ShipObservation` helpers (**RL Plan C-1/C-2**) and write `BTStateBridge.cs` (**C-3**).

## **Tue 24 Jun – RL Boot-strapping & Behaviour States**
• Record ≥100 k BC frames (`--recording`) with current Dummy AI. 
• Run BC-initialised PPO Tier-0 training for ~3 h; archive checkpoints. 
• Implement *Behavior Upgrades* **M2 & M3**: `UtilitySelector`, `ScanPatrol`, `Investigate`, `PredictivePursue`, `StrafeRun`, `KiteRun` + hysteresis timer.
• Integrate `AsteroidDensity` Env-Param hook into `AsteroidFieldManager` (**RL Plan C-4**). 
• Finalise batched physics across ships; enable spatial grid query (AI Perf § 1B).
• Add performance benchmark "Asteroid Field Stress" to UTF (Testing Plan § 3.3).

## **Wed 25 Jun – Polish, Evaluation & Docs**
• PPO Tier-1 training: scripted enemy, 100 % density; evaluate 500 episodes → target ≥0 win-rate delta vs Dummy. 
• Profiling pass: ensure 300 AI @ 60 FPS reachable (AI Perf quick-start checklist).
• Play-test session (#P2) with 5 players vs Dummy & RL bots; collect survey data.
• Refactor/debug any critical findings; lock feature branches. 
• Ship documentation: update README, create `reports/p2_balance.pdf` & `reports/p3_believability.pdf`.
• Tag repository `v0.9-feature-freeze` and prepare final PR for instructor review.

---

**Stretch (post-deadline)** – DOTS Entities port, full Behaviour-Tree state rollout (M4-M6), CI training workflow, GPU avoidance system.

---

_Last updated: 2025-06-22_