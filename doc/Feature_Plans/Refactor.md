# Refactor Roadmap

> 30 000-foot view of redundant patterns and quick simplifications identified across the codebase.  Nothing here changes gameplay yet—it is a shopping list of "easy wins" for a future cleanup sprint.

---



6. **Scattered physics buffers**  
   Dozens of scripts declare their own static `Collider[] hitBuffer = ...`.  Provide a tiny pooled helper (`PhysicsBuffers`) so buffer size & allocation policy live in one spot.

3. **Boundary logic duplication**  
   `ArenaInstance.CheckAgentBoundaries()` and `OnTriggerExit()` both end episodes & assign rewards.  Consolidate into one `BoundaryService` to avoid double penalties and scattered rules.

14. **Mixed scheduling patterns**  
    `InvokeRepeating`, `Update`, and coroutines all schedule similar work.  Standardise per manager to reduce timers & flags.

11. **Assembly-definition overlap**  
    Both `Game.AI` and `Game.RL` include `Unity.Behavior.*`.  If keeping RL & non-RL AI separate, only one asmdef should reference those libs; the other can depend on the first.

13. **Observation label drift risk**  
    `RLObserver.ObservationLabels` must stay in sync with `CollectObservations`.  Auto-generate or validate at runtime to prevent silent mismatch.




1. **Behaviour-Tree boilerplate**  
   Every action/condition repeats identical _GetComponent / null-check / gizmo_ scaffolding.  Introduce small abstract bases (e.g. `ShipBTAction`, `ShipBTCondition`) that cache common references and unify gizmo drawing.

7. **Gizmo / debug UI sprawl**  
   Repetitive `OnDrawGizmos*` blocks across AI nodes, camera, arena, missiles, etc.  Centralise with `DebugGizmos.DrawSphereLabel()` & consistent colour palette.


15. **Duplicate heuristic ↔ player input mapping**  
    `RLCommanderAgent.Heuristic` and `PlayerCommander` both translate keyboard into `ShipCommand`.  Extract common `PlayerInputMapper` so logic lives once.

---

_These items represent the highest ROI for codebase cleanliness and future maintainability.  Address them in small, isolated PRs to keep gameplay behaviour stable while the architecture is simplified._ 