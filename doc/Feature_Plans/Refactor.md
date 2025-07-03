# Refactor Roadmap

> 30 000-foot view of redundant patterns and quick simplifications identified across the codebase.  Nothing here changes gameplay yet—it is a shopping list of "easy wins" for a future cleanup sprint.

---

1. **Behaviour-Tree boilerplate**  
   Every action/condition repeats identical _GetComponent / null-check / gizmo_ scaffolding.  Introduce small abstract bases (e.g. `ShipBTAction`, `ShipBTCondition`) that cache common references and unify gizmo drawing.

2. **Dual sync *vs.* coroutine fragment code**  
   `AsteroidFragnetics` maintains two near-identical physics pipelines plus placeholder-fragment logic.  Parameterise a single routine by `yieldFrequency`; callers decide whether to `StartCoroutine` or run inline. ✅ COMPLETE

3. **Boundary logic duplication**  
   `ArenaInstance.CheckAgentBoundaries()` and `OnTriggerExit()` both end episodes & assign rewards.  Consolidate into one `BoundaryService` to avoid double penalties and scattered rules.

4. **Multiple implementations of `IGameContext`**  
   `SimpleGameContext`, `ArenaInstance`, and `GameManager` each roll their own "active ship list & area size" code.  A shared base (e.g. `BaseGameContext`) would remove triplicate logic. ✅ COMPLETE

5. **Steering maths cloned in three places**  
   `PathPlanner`, `AICommander's avoidance code, and `VelocityPilot` duplicate constants (`ForwardAcceleration`, `VelocityDeadZone`, ...) and segment-avoid algorithms.  Pull into one steering module.

6. **Scattered physics buffers**  
   Dozens of scripts declare their own static `Collider[] hitBuffer = ...`.  Provide a tiny pooled helper (`PhysicsBuffers`) so buffer size & allocation policy live in one spot.

7. **Gizmo / debug UI sprawl**  
   Repetitive `OnDrawGizmos*` blocks across AI nodes, camera, arena, missiles, etc.  Centralise with `DebugGizmos.DrawSphereLabel()` & consistent colour palette.

8. **Inconsistent logging usage**  
   `RLog` offers category helpers but many files still call `Debug.Log`.  Replace raw calls; consider source-generated extensions to remove boilerplate.

9. **Legacy-vs-new settings paths**  
   `AsteroidSpawner` keeps old inspector arrays alongside `AsteroidSpawnSettings` ScriptableObject.  Choose one (prefer SO) and delete the other.

10. **Dead / deprecated stubs**  
    • `FireWeaponAction` is a no-op—mark `[Obsolete]` or remove once BT assets are migrated.  
    • `RLArbiter` overlaps with `RLCommander`; decide to merge or delete until needed.

11. **Assembly-definition overlap**  
    Both `Game.AI` and `Game.RL` include `Unity.Behavior.*`.  If keeping RL & non-RL AI separate, only one asmdef should reference those libs; the other can depend on the first.

12. **Repeated tag / layer literals**  
    Strings like "Ship", "Projectile", "Asteroid" appear everywhere.  Gather into `LayerIds`/`TagNames` static class.

13. **Observation label drift risk**  
    `RLObserver.ObservationLabels` must stay in sync with `CollectObservations`.  Auto-generate or validate at runtime to prevent silent mismatch.

14. **Mixed scheduling patterns**  
    `InvokeRepeating`, `Update`, and coroutines all schedule similar work.  Standardise per manager to reduce timers & flags.

15. **Duplicate heuristic ↔ player input mapping**  
    `RLCommanderAgent.Heuristic` and `PlayerCommander` both translate keyboard into `ShipCommand`.  Extract common `PlayerInputMapper` so logic lives once.

---

_These items represent the highest ROI for codebase cleanliness and future maintainability.  Address them in small, isolated PRs to keep gameplay behaviour stable while the architecture is simplified._ 