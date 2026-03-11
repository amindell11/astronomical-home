# MPC Backlog Assessment

## 1. Scale MPC for multi-ship real-time performance

**Scope:** Optimize Sampler.Solve() to handle N ships simultaneously without frame drops. Currently 128 samples × 15 steps per ship.

**Complexity:** Medium-High. Involves profiling, potential Burst/Jobs parallelization, or reducing sample counts adaptively.

**Impact:** High — prerequisite for any real combat scenario with multiple AI ships.

**Design Decisions:** Budget per ship (ms), whether to use Unity Jobs/Burst, whether to stagger solves across frames (ship A solves frame 1, ship B frame 2), or reduce horizon/samples for distant ships.

**Approaches:**
- Burst-compile Model.Step and Cost functions
- Adaptive sample count based on LOD/priority
- Temporal staggering — solve every Nth frame per ship
- SIMD-friendly batch evaluation of all samples

---

## 2. Maneuvers/juking constraints

**Scope:** Let MPC directly produce evasive patterns (jinking, dodging) rather than relying on JinkEvade state setting random waypoints.

**Complexity:** Medium. The MPC already controls strafe; the challenge is encoding "unpredictable lateral oscillation" as a cost or constraint.

**Impact:** High — current jinking is waypoint-based and sluggish. MPC-native juking would be more fluid and reactive.

**Design Decisions:** Should juking be a periodic cost term (reward lateral velocity changes), a constraint (minimum lateral displacement over time), or injected via biased noise in the sampler? How to balance predictability-avoidance vs. goal-seeking?

**Approaches:**
- Time-varying cost that rewards strafe sign changes
- "Anti-prediction" cost penalizing straight-line trajectories when under fire
- Structured noise injection in sampler that favors oscillating strafe patterns
- Constraint that enforces minimum lateral acceleration variance

---

## 3. Modify waypoint logic to make MPC more flexible

**Scope:** Extend the Waypoint struct and MPC cost to support velocity goals, corridor constraints, or multiple sequential waypoints.

**Complexity:** Low-Medium. The waypoint velocity field already exists but is ignored by MPC's cost function.

**Impact:** Medium-High — unlocks richer behaviors (fly-through waypoints, approach vectors, patrol paths).

**Design Decisions:** Should MPC track a single waypoint with velocity, or a short queue? Should waypoints have tolerance radii? Should "fly-through" vs "stop-at" be a waypoint property?

**Approaches:**
- Add `wVel * ||v - goalVel||²` term to Cost.cs (minimal change)
- Waypoint queue with automatic advancement
- Parametric path following (Frenet frame cost)
- Waypoint with approach direction constraint

---

## 4. Make MPC consider fast-moving obstacles in state rollouts

**Scope:** Extend Model.Step or Cost to predict obstacle positions forward in time during rollout, not just use their current position.

**Complexity:** Medium. Requires propagating obstacle velocity through the 15-step horizon. Currently obstacles are treated as static in cost evaluation.

**Impact:** High — critical for dodging missiles, debris, and other ships in motion.

**Design Decisions:** Linear extrapolation vs. more complex prediction? How to handle obstacle velocity data (DynamicObstacleScanner already has access to rigidbodies)? Performance cost of N_obstacles × N_steps predictions?

**Approaches:**
- Linear extrapolation: `obs_pos_t = obs_pos + obs_vel * t` per rollout step
- Only predict for obstacles above a velocity threshold
- Inflate obstacle radius proportional to relative closing speed as a cheaper approximation

---

## 5. Make MPC recognize strafe-roll as a strategy

**Scope:** Allow MPC to discover or prefer trajectories that combine strafing with rotation to present a harder target or maintain gun alignment while moving laterally.

**Complexity:** Medium-High. This is emergent behavior — MPC needs the right cost structure to "want" it.

**Impact:** Medium — makes AI movement look more skilled and harder to hit, but is a refinement over basics.

**Design Decisions:** What exactly is the desired strafe-roll behavior? Is it strafing while rotating to keep facing the enemy? Or barrel-roll-like evasion? Should this be a distinct cost term or emerge from existing facing + strafe costs?

**Approaches:**
- Reward trajectories where strafe and yaw are correlated (cost term for `strafe * yawTorque` alignment)
- Facing override toward enemy combined with reduced strafe smoothness penalty during combat
- Explicit "strafe-roll" mode activated by utility states that adjusts weights

---

## 6. Utility states adjust MPC weights

**Scope:** Let AI states (Attack, Evade, Kite, etc.) dynamically modify MPC Config weights when they activate.

**Complexity:** Low. The Config struct already holds all weights; states just need to set them.

**Impact:** High — single biggest lever for making MPC behavior context-appropriate. An evading ship should have high obstacle/smoothness weights; an attacking ship should have high facing weight and low strafe penalty.

**Design Decisions:** Per-state weight profiles (ScriptableObjects?) vs. computed adjustments? How to blend during state transitions? Which weights matter most per state?

**Approaches:**
- Each state holds a `MpcWeightOverride` ScriptableObject applied on enter
- States modify specific weights via multipliers (e.g., `evade: wObstacle *= 3`)
- Lerp weights over transition period to avoid jerky behavior changes
- Start with a simple dictionary of per-state weight presets

---

## 7. MPC obstacle avoidance

**Scope:** Foundational obstacle avoidance already exists (inverse-square cost in Cost.cs). This likely refers to making it work reliably in practice.

**Complexity:** Low-Medium. The math is there; the work is integration, tuning, and edge cases (obstacles behind ship, narrow gaps, etc.).

**Impact:** High — basic safety requirement for any navigation scenario.

**Design Decisions:** Is the current inverse-square formulation sufficient or does it need a hard constraint (infeasible trajectories rejected outright)? Should avoidance be directional (only avoid obstacles ahead)? What about the interaction between obstacle cost and position cost when the goal is behind an obstacle?

**Approaches:**
- Tune existing `wObstacle` and `obstacleThreshold` values
- Add hard constraint — reject any trajectory that intersects obstacles
- Add velocity-aware lookahead (overlaps with item #4)
- Directional filtering — only penalize obstacles in the forward cone

---

## 8. Aiming constraint

**Scope:** Add an MPC constraint or cost that keeps the ship oriented to maintain weapon firing solutions while navigating.

**Complexity:** Medium. The facing override system exists, but a true "aiming constraint" means the MPC should sacrifice position-optimal paths to keep guns on target.

**Impact:** High — directly affects combat effectiveness. Currently Gunner and Navigator are loosely coupled via facing override.

**Design Decisions:** Hard constraint (never deviate beyond X degrees from target) vs. soft cost (penalty for facing away)? Should this account for weapon convergence angle, turret traverse, or fixed-forward guns only? Should it factor in time-to-fire windows?

**Approaches:**
- Increase `wFacing` dynamically when Gunner has a firing solution
- Add angular-velocity-aware term that keeps yaw rate aligned with target tracking rate
- "Firing cone" constraint — bonus for trajectories where ship faces target for more timesteps
- Integrate Gunner's intercept prediction into MPC cost directly

---

## 9. Debug/tune MPC obstacle avoidance

**Scope:** Use existing Editor gizmos and cost breakdowns to iteratively tune obstacle avoidance behavior.

**Complexity:** Low. The debug tooling (MpcNavigator.Editor.cs) already exists with cost visualization and trajectory rendering.

**Impact:** Medium — depends on how well #7 is implemented first.

**Design Decisions:** What scenarios define "good" avoidance? Need test scenarios (asteroid field, narrow corridor, head-on collision course). Acceptance criteria for avoidance quality.

**Approaches:**
- Build dedicated test scenes with known obstacle layouts
- Log cost breakdowns over time to identify when obstacle cost is overwhelmed by position cost
- Visualize "near miss" events
- A/B compare weight configurations automatically

---

## Recommended Priority Order

1. **Utility states adjust MPC weights** — low complexity, high impact, unlocks value from everything else
2. **MPC obstacle avoidance** + **Debug/tune** — foundational safety, do together
3. **Scale for multi-ship** — required for real gameplay
4. **Fast-moving obstacles in rollouts** — critical for missiles/combat
5. **Waypoint flexibility** — quick win, partially already stubbed out
6. **Aiming constraint** — direct combat effectiveness improvement
7. **Maneuvers/juking** — refinement layer on top of working system
8. **Strafe-roll as strategy** — polish/emergent behavior, do last
