# MPC Velocity-Reference Mode — PR-0 + PR-1 Implementation Plan

**Date:** 2026-07-11 (scoped via grill)
**Parent:** `Tactical_AI_Audit_And_Roadmap.md` §3′/§4′ — this is the detailed breakdown of
that roadmap's **PR-1** (the keystone velocity-reference interface), plus a **PR-0** refactor
that must land first.
**Status:** Scoped, not started. PR-0 is the mergeable-on-its-own precursor; PR-1 builds on it.

> **One-line intent.** Reshape the MPC into a *feasibility tracker* that follows a commanded
> planar velocity (the seam a learned goal-policy will later drive), without contorting the
> existing position-goal + tactical controller. The clean coexistence comes from **PR-0**,
> which reorganizes the cost so the tracker is a *composition* (`Feasibility + Aim + velocity
> Objective`) rather than the scripted controller with its tactical terms zeroed out.

---

## Why PR-0 first (the root-cause framing)

The naive way to add a velocity mode — branch on `goalMode` throughout `Cost.Evaluate` and
zero ~8 weights — is the contrived tangle it feels like. The root cause is that
`Cost.Evaluate` conflates three separable responsibilities in one flat body:

- **Feasibility / regularization** (always on, both controllers): collision, obstacle
  turn-away, effort, boost-effort, smoothness, yaw-rate, momentum.
- **Objective** (the "what to achieve", exactly one active): position-goal / range-band /
  flee — and, new, **velocity-track**.
- **Tactical shaping** (authored combat tactics): exposure, tangential, miss-distance, LOS,
  plus the pursuit terminal cost-to-go field.

Post-RL, the tactical block is **dead in the learned path** — the reward teaches those
behaviors and the policy expresses them through the velocity reference. Those terms don't
survive as "knobs set to 0"; they survive as **a different controller entirely**: the frozen
*scripted baseline opponent*. So the real end-state is **two cost identities sharing one
solver**:

| Cost group | Scripted baseline (frozen opponent) | RL / velocity tracker |
|---|---|---|
| Feasibility | ✓ | ✓ |
| Aim (intercept-facing) | ✓ | ✓ |
| Objective | position-goal / range-band / flee | **velocity reference** |
| Tactical (exposure, tangential, miss-dist, LOS, pursuit-field) | ✓ | **absent** |

Encoding the tracker as "scripted-minus-eight-weights" models it as a crippled copy of the
scripted AI. It is not — it is `Feasibility + Aim + velocityObjective`, full stop. PR-0 makes
that structure explicit so the two identities coexist without either contorting the other.

**What PR-0 is *not*.** It does **not** restructure `Config` into sub-structs (needless Burst
struct-layout churn across every `ToConfig`/override call site) and does **not** delete the
tactical terms (they are the baseline opponent). It reorganizes the *summation* and adds one
toggle. A **toggle, not a delete** — because "kept but inactive here" is the truth.

---

## PR-0 — Cost regroup + objective-aware idle gate

Behavior-preserving for the scripted controller (the tuned `.asset` weights and baked test
tolerances are the safety net), **plus one targeted idle-gate correctness fix** (below). Not a
pure no-op — that is called out honestly in the PR.

### A. `Cost.Evaluate` regroup (`AI/Navigation/MPC/Cost.cs`)

Extract four static helpers over the *existing* term functions — no math changes, **no
`Config` data-layout change**:

- `Feasibility(s, u, prevU, cfg)` → collision, obstacle turn-away, effort, boost-effort,
  smoothness, yaw-rate, momentum.
- `Aim(s, ctx, cfg)` → `FacingCost` (intercept-facing). **Pulled out of the tactical block** —
  this resolves the roadmap's own contradiction (it listed `FacingCost` as "tactical, gated
  off" *and* "intercept-facing, kept"). Aim is aiming geometry, kept in both identities; only
  the authored *tactics* toggle off.
- `Objective(s, ctx, cfg)` → today's position bundle (`pos + closing + heading + vel-damping +
  arrival`). **Dispatches on `goalMode`.** PR-0 ships only the existing position-family branch
  (no velocity branch yet → zero dead code).
- `Tactical(s, ctx, cfg)` → `los + exposure + tangential + missDistance`, summed **only when
  `cfg.tacticalEnabled`**.

Add `bool tacticalEnabled` to `Config`, **default `true`**; every existing `ToConfig` path
leaves it `true` → identical behavior.

**Terminal ramp preserved.** Today `total += ramp * (positionalCost + tacticalCost)` and
`tacticalCost` already includes `facingCost`. The regrouped ramp target is
`Objective + Aim + (tacticalEnabled ? Tactical : 0)` — identical for the scripted path because
tactical stays on and aim was already inside the ramped block.

Update `EvaluateBreakdown` (`Navigator.Editor` / `Cost.Editor`) to reuse the same groups so the
cost inspector does not drift from `Evaluate`.

### B. Navigator objective-aware idle gate (`AI/Navigator.cs`)

Today `ComputeCommand` early-returns `default` when `!currentWaypoint.isValid ||
HasArrived(kin)`. This conflates **"do I have a destination"** with **"should I be
controlling the ship"**, hard-wired to a *waypoint* objective. Extract a mode-dispatched
`ShouldIdle(kin)`:

- **Waypoint** (goto / patrol) → `!currentWaypoint.isValid || HasArrived(kin)` — unchanged;
  arrival semantics are correct here.
- **MaintainRange / Flee** → `!currentWaypoint.isValid` **only, no arrival check.** These are
  continuous hold/evade objectives, not destinations; the enemy position *is* the waypoint and
  the MPC should always run while the enemy exists.
- **VelocityReference** (added in PR-1) → `!hasVelocityReference`.

**This is a real, beneficial behavior change** for combat modes, not a no-op. Today
`HasArrived` can fire for MaintainRange/Flee in an edge case (within `arriveRadius` of the
enemy *and* nearly stopped), which the range-band repulsion normally prevents — so the common
case is unchanged, but the edge case currently causes a **spurious freeze**, and the idle
short-circuit also blocked the MPC from making micro-corrections to *hold* the band. Removing
it is strictly better. Guarded by a new test: *combat mode, near-and-stopped, does not idle.*

### PR-0 tests
- Existing MPC/AI suites pass unchanged (they are the pin for the scripted path).
- New: `tacticalEnabled == true` reproduces pre-refactor cost on a fixed state (regroup guard).
- New: MaintainRange near-and-stopped does not idle (idle-gate fix guard).

---

## PR-1 — VelocityReference objective + tracking validation

### A. Enum + plumbing (additive)
- `GoalMode.VelocityReference = 3` (`MPC/Types.cs`). `IsEnemyAnchored()` stays **false** for it.
- `float2 velocityReference` + `bool hasVelocityReference` on `NavigationIntent`, `MpcInputs`,
  `CostInput`.
- `float wVelTrack` base field on `MpcSettings` → `Config`.

### B. The tracker cost (`Cost.Objective` gains a branch)
- `goalMode == VelocityReference` → `VelocityTrackCost(s.vel, input.velocityReference)`,
  **skipping the entire position bundle.** The objective dispatch *is* the gate — legacy modes
  never evaluate this term, so they stay byte-identical. **No weight-zeroing anywhere.**
- Form: `VelocityTrackCost = ‖s.vel − v_ref‖² / maxSpeedSq` (same normalization idiom as
  `VelocityCost`). **Un-ramped, per-step** — uniform tracking across the horizon, not a
  terminal goal. Starting `wVelTrack ≈ 5`, tuned by the fidelity test.

### C. Mode config
- `tacticalEnabled = goalMode != VelocityReference` (set in `ToConfig`/`RefreshConfig`) → the
  single line that drops the tactical block.
- Pursuit terminal cost-to-go field is off because the Navigator does not set
  `terminalFieldTarget` in this mode (it is set only for MaintainRange today).
- **Aim + Feasibility stay on.**

### D. Aim / yaw in velocity mode
- Enemy present → intercept-yaw (`Cost.InterceptYaw`, via `projectileSpeed`) — preserves
  strafe-while-aiming and retreat-while-facing.
- **No enemy → free (regularized) yaw.** Chosen for now for simplicity. See *Deferred* — a
  nose-follows-velocity fallback would improve tracking authority in the no-enemy case (thrust
  is nose-aligned; forward accel ≫ strafe accel), but that case is degenerate in real 1v1 RL
  use, so it is deferred.

### E. Navigator wiring (`AI/Navigator.cs`)
- `SetVelocityReference(float2)` — public low-level seam that `ApplyIntent` composes (mirrors
  `SetNavigationPoint`); the PlayMode smoke and any direct-drive test use it.
- `ApplyIntent`: `VelocityReference` case sets the mode + reference and **does not set a
  waypoint** — this is where PR-0's `ShouldIdle` seam pays off (no bogus waypoint to gate on).
  The reference flows through the single idempotent entry point, preserving "result depends
  only on the intent."
- `ShouldIdle`: VelocityReference → active iff `hasVelocityReference` (never "arrives"; a zero
  reference is a valid "stop" command).

### F. Frame
- The interface is **world-plane** (`float2` in plane coordinates), matching `State.vel`; the
  tracker cost is `‖s.vel − v_ref‖²` with zero conversion in Burst.
- Ego encoding is a **rolling-frame trap** (the ship yaws during the rollout; an ego-encoded
  reference reprojected through each step's changing yaw would curve). The reference is a
  desired *world* motion captured at decision time.
- The *policy's* ego I/O (PR-3) converts ego→world **once at the chooser boundary** using
  `ObservationExtractor`'s `EgoFrame` — mirroring how `SetEnemyState` already puts the
  yaw-convention conversion "at the MPC boundary, not in the strategy layer."

### G. Validation (this PR)
- **EditMode unit:** `VelocityTrackCost` minimized at `v = v_ref`, monotonic in error; a
  regression guard that legacy-mode cost is byte-identical.
- **EditMode tracking-fidelity** (mirror `MpcSolverTests.SolveTerminal`, drive `Mpc.Plan` on
  `Model.Step`, no ship/physics): command **on-axis** `v_ref` → tight convergence; command
  **off-axis** `v_ref` (perpendicular to a pinned facing) → converges within a *characterized*
  strafe-authority envelope. This is the direct de-risk of the one real interface risk —
  moving hard perpendicular to where you are facing is the physically-hardest case, and the
  test asserts the *envelope*, not a pass/fail tight bound.
- **PlayMode smoke** (existing `ShipTestFactory` / `AIIntegrationFixture`): real ship,
  `SetVelocityReference` + mode, tick physics, assert real velocity trends to the command —
  confirms the mode moves a real hull through `Pilot` / `MovementController`.

---

## Implementation notes (as-built, PR-0 + PR-1 stacked on one branch)

Two places where the as-built code deviates from the prose above — both to preserve the
stated intent (behavior-preserving PR-0; un-ramped tracking) against details the plan glossed:

1. **Terminal-ramp membership (PR-0 §A).** The plan's regrouped ramp target — `Objective +
   Aim + (tacticalEnabled ? Tactical : 0)` — is **not** byte-identical to the legacy ramp,
   which multiplied `positionalCost + tacticalCost`, and `positionalCost` *includes* yaw-rate,
   obstacle turn-away, and momentum. Ramping only Objective+Aim+Tactical would silently drop
   those three from the ramp near the horizon end → a real behavior change → broken baked
   tolerances. As-built, `stateCost` (the ramped quantity) is exactly `positionalCost +
   tacticalCost`, i.e. the four groups plus the state-shaping regularizers (obstacle/yaw-rate/
   momentum), so the scripted path is numerically identical (modulo float re-association).
   Feasibility therefore isn't a single ramp-coherent block: its state regularizers ride the
   ramp; control effort and the fixed collision penalty do not. Whether those regularizers
   *should* be un-ramped is a legitimate question — but a deliberate, evidence-backed retune,
   not a "behavior-preserving" refactor. Left as-is; noted under *Deferred*.

2. **Velocity objective lives outside the ramp (PR-1 §B).** The plan says put the velocity
   branch *inside* `Objective`, and also that `VelocityTrackCost` is *un-ramped*. Those
   conflict — `Objective` is part of the ramped `stateCost`. As-built, the velocity term is
   dispatched in `Evaluate` (`goalMode == VelocityReference`) and added to the base total
   **outside** the ramp, so tracking is uniform per-step (correct for a receding-horizon
   tracker). `Objective` stays the position-family bundle. Aim + the regularizers remain
   ramped in velocity mode, matching the scripted path.

Two smaller as-built choices worth recording:

- **No redundant `hasVelocityReference` flags on `MpcInputs`/`CostInput`.** The plan listed
  them there; in practice the cost dispatches purely on `goalMode == VelocityReference`, and
  the solver only ever runs velocity mode while armed (ShouldIdle gates it). The arm flag lives
  only where it's read — on the `Navigator` (for `ShouldIdle`). A zero reference is still a
  valid "stop" (the flag, not the value, gates activity). This follows the dependency
  philosophy: don't thread state that isn't consumed.
- **Fidelity finding — tracking is correct but soft at `wVelTrack = 5`.** The closed-loop
  EditMode test (re-plan → apply first control → `Model.Step`, ~10 s) confirms the ship settles
  its velocity toward the command and that forward authority strictly exceeds strafe authority
  (perpendicular-to-nose is the hardest case). But at the plan's starting `wVelTrack = 5` the
  tracking is *soft*: meaningful on-axis forward velocity with some lateral drift, and low
  strafe authority (~0.2 m/s for a 12.5 m/s perpendicular command, fighting the strafe-smoothness
  weight). So the fidelity test asserts the **envelope** (right direction, forward ≥ strafe,
  brakes to rest on a zero command), not a tight bound — faithful to the plan's "characterize
  the envelope" intent. Left `wVelTrack = 5` as shipped; the reward loop tunes it in PR-3 (per
  the plan's own "≈5 starting, tuned by the fidelity test").
- **Editor inspector in velocity mode is deferred.** `SolverBuffers.BuildCostInput` (used by
  the in-editor cost breakdown, comparison rollouts, and predicted-trajectory gizmo) does not
  yet carry `velocityReference`, so a *live* velocity-mode ship would show its velocity-track
  bar against a zero reference. No live ship enters velocity mode until PR-3 wiring; the
  EditMode fidelity tests drive `Cost.EvaluateBreakdown` directly with a correct `CostInput`,
  so the drift-guard still holds. Wire it when PR-3 puts a real ship in the mode.

---

## Deferred / open (recorded here so the lessons outlive the PR)

- **Closed-loop maneuver oracle + CMA-ES → PR-2.** The *tracking-fidelity* question (does the
  MPC follow `v_ref`?) is a property of the cost/solver and lives in PR-1. The *maneuver
  expressiveness / ceiling* question (does closed-loop velocity commanding produce a **held**
  orbit / effective break / stable range band, measured over an episode?) needs episode-length
  rollouts + maneuver metrics + ideally CMA-ES — which is PR-2's headless runner minus the
  reward. Build it there as the runner's first application and an explicit **go/no-go gate
  before reward/ML** (fail-fast is preserved; it moves from end-of-PR-1 to start-of-PR-2,
  still before the PR-3 ML spend). CMA-ES reuses that loop instead of a throwaway harness.
- **Yaw-when-no-enemy in velocity mode.** Free/regularized for now; revisit a
  nose-follows-velocity fallback (better tracking authority) if the degenerate case ever
  matters.
- **Ego I/O conversion → PR-3** (chooser boundary; `EgoFrame`).
- **`Config` sub-struct restructure — not now.** The summation regroup delivers the
  organizational win; a data-layout split churns Burst for no functional gain until evidence
  demands it.
- **Latent (untouched): none remaining in the idle gate** — PR-0 fixes the combat-arrival
  conflation. If a future mode needs different idle semantics, extend `ShouldIdle`.

---

## Appendix — files touched

- **PR-0:** `AI/Navigation/MPC/Cost.cs` (regroup + `tacticalEnabled`); `MPC/Types.cs` (`Config`
  field); `MPC/MpcSettings.cs` (`ToConfig` sets `tacticalEnabled`);
  `AI/Navigation/MPC/Editor/Cost.Editor.cs` + `MPC/Editor/Navigator.Editor.cs`
  (`EvaluateBreakdown`); `AI/Navigator.cs` (`ShouldIdle`); tests under
  `Editor/Tests/EditMode`.
- **PR-1:** `MPC/Types.cs` (`GoalMode`, `CostInput`, `MpcInputs`); `MpcSettings.cs`
  (`wVelTrack`); `Cost.cs` (`VelocityTrackCost`, `Objective` branch); `Navigator.cs`
  (`SetVelocityReference`, `ApplyIntent` case, `ShouldIdle` branch);
  `AI/Navigation/NavigationIntent.cs` (fields); `BurstSolver.cs` / `Mpc.cs` (thread
  `velocityReference` through `Solve`/`Plan`); tests (EditMode fidelity + PlayMode smoke).
