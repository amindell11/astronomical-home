# Chase Navigation Trade Study

**Date:** 2026-07-05
**Status:** Research complete — awaiting scoping decision
**Goal:** High-speed, maneuver-heavy AI chases through dense procedural asteroid fields
(`BigFieldSettings`) — the bot should cut corners through gaps like a skilled human,
including banking (strafe-tilt) to knife through tight gaps, instead of bumping into
rocks and losing the player.

---

## 1. Problem statement

The MPC navigator (CEM-style sampling, 1.7 s horizon, Burst) is **topology-blind**: its
reactive obstacle cost can't see that a 4-second detour around a cluster pays off, so it
gets trapped bumping against rocks. A previous fix — the `AsteroidNavField` Dijkstra
flow-field (removed in #43, last tree at `5f5a4530~1`) — solved topology but felt
grafted on: it substituted the MPC's goal with a routed waypoint, so the global layer
and the tactical costs (LOS/exposure/range-band, still aimed at the true enemy) fought
each other.

Separately, the desired "bank hard to slip through a gap" behavior never emerges, even
though the model already contains a bank-profile term.

## 2. Ground truth (current main)

### Physics
- Bank is real physics: strafe commands a target bank (`Forces.cs:37-40`), applied as a
  damped spring torque on the Rigidbody about the nose axis
  (`MovementController.cs:97-101`). The ship's only collider (convex MeshCollider on the
  `Mesh` child of the Rigidbody root) **rolls with the bank** — the in-plane
  cross-section genuinely narrows.
- Live `maxBankAngle` = **35°** → max narrowing ≈ `1 − cos35°` ≈ **18%** (45° → 29%,
  60° → 50%). Modest; a game-feel lever if we want dramatic knifing.

### What the solver sees
- Ship footprint is a **fixed isotropic disc**: `shipRadius = 1.4` baked into every
  obstacle radius at conversion (`BurstSolver.cs:358`, `obs.radius + shipRadius`).
- The bank term `profileScale = cos(|strafe|·maxBank)` (`Cost.cs:122-145`) only shrinks
  the **padding** (`obstacleThreshold + speed·obstacleSpeedMargin`), never the 1.4 hull
  radius. The modeled reward for banking is structurally understated vs. the real
  geometric benefit. Model-consistency bug, effectively.
- Obstacle scan: speed-adaptive `OverlapSphere` (lookahead 2 s), merged with a 30 u ship
  scan, ≤128 obstacles; solver ranks top-8 by cost (`Scout.cs`, `ObstacleScanner.cs`).

### Live tuning (MpcSettings.asset — actual values, not code defaults)
- `horizonSeconds 1.7`, `rolloutDt 0.1` (17 steps), `samples 512`, `noiseStd 0.75`,
  `eliteFraction 0.113`.
- `wSmoothnessStrafe = 0`, `controlSmoothing = 0` — **nothing damps strafe** except
  `wEffort = 0.1`. (Kills the "smoothing suppresses strafe" hypothesis.)
- `wObstacle = 1` vs `wPos = 50`, with `obstacleFalloffCurve = 13.05` and
  `obstacleThreshold = 0.1`: obstacle avoidance is **deliberately neutered**. This was
  an intentional retreat, not drift: with any meaningful threshold the ship *thrashed*
  at the cost boundary regardless of falloff curve, worst with the speed-based
  expansion/contraction.

### Why the threshold cost thrashes (diagnosis)
The observed thrashing is structural, not a tuning problem:
1. **Speed-margin feedback loop.** `effectiveThreshold = threshold + speed·margin`
   lets a rollout cut obstacle cost *without moving* — just trim thrust. Decelerate →
   threshold contracts → cost vanishes → accelerate → threshold expands → cost spikes.
   The optimizer rides this limit cycle. (`obstacleClosingScale` injects velocity into
   the cost a second time, compounding it.)
2. **Set-membership discontinuity at the boundary.** Outside `radius + threshold` an
   obstacle contributes exactly 0; grazing trajectories flip it in/out of the cost (and
   in/out of the top-8 ranked buffer) between ticks, so elites — and controls — chatter.
   No falloff curve can smooth a discontinuity in *which obstacles exist*.
3. **Scan-radius churn.** The `OverlapSphere` scan radius is itself speed-dependent
   (`|vel|·2s + ½·a·t²`), so the obstacle *set* breathes with speed upstream of the
   solver too.

Conclusion: the inflated-threshold potential cost is the wrong primitive for
close-and-tight flying, and the sensing pipeline should be rethought with it (§3.5).

### Geometry
- `FieldSettings` (sparse): gaps ≫ ship; banking never geometrically necessary.
- `BigFieldSettings` (the fun field): `packingMargin 0`, `minSpacing 0` — gaps can
  approach the ~2.8 u ship diameter. **This is the regime the whole study targets**, and
  where an 18–29% profile reduction actually opens otherwise-impassable gaps.

### Dead/mistrusted features audit (keep / fix / delete candidates)
| Feature | Location | Verdict candidate |
|---|---|---|
| `adaptiveDtScale` dynamic horizon | `Mpc.RefreshConfig` (`Mpc.cs:212-237`) | **Delete.** Stretching dt coarsens the near field exactly when fast/far (tunneling past small rocks: step length `speed·dt` exceeds small-asteroid diameter), and every 0.25-bin flip triggers warm-start resampling. Terminal-field guidance (Option C) supersedes its purpose. |
| `relaxMin/relaxMax` urgency scaling | `Mpc.Plan` (`Mpc.cs:116-124`) | **Review.** Post-hoc attenuation of the optimizer's plan; can neuter a deliberate max-strafe gap approach. At minimum exempt seeded-primitive solutions. |
| `controlSmoothing` lerp | `Mpc.Plan` | Already 0 in the asset — **delete the code path** or keep dormant. |
| `profileScale` on padding only | `Cost.cs:141-145` | **Fix** (see Option D) — apply bank narrowing to the hull radius, not just padding. |
| `obstacleFalloffCurve 13.05` + `wObstacle 1` | asset | **Retune** — restore a usable gradient (see Phase 0). |

## 3. Research findings (three parallel surveys, full reports in scratchpad)

### 3.1 Global guidance for short-horizon sampling MPC
- Two proven patterns: **(A)** global path tracked via stage costs (Nav2 MPPI critics,
  F1TENTH — the deployed standard) and **(B)** **cost-to-go as terminal cost** on each
  rollout (POLO ICLR'19, TD-MPC, PAC-NMPC — the principled fix for finite-horizon
  myopia). B fits us best: no goal substitution, no second controller — rollout cost
  `= Σ stage + w_T · costToGo(x_terminal)`; the MPC keeps full authority over dynamics,
  combat costs, dodging.
- **Units matter:** store the field as *time-to-go* (distance ÷ nominal chase speed, or
  Eikonal), so `w_T ≈ 1` against time-denominated stage costs. Evidence says err
  *high* on `w_T` — under-weighting re-creates myopia.
- **Coarse grids are fine:** POLO's core result — H-step lookahead absorbs value error.
  Cells ≈ ship-length to half-gap. Bilinear interpolation kills grid-dither.
- **Cheap:** one backward Dijkstra/FMM sweep per *target* (not per ship), re-solved when
  the target moves > ~1 cell; static obstacle stamp built once (deterministic field!).
  Terminal lookup = one fetch per rollout.
- **Grid Dijkstra beats visibility graph / GVD / PRM** *as a field source*; the old
  NavField's grid was right — the coupling (waypoint substitution) was wrong. Tangent
  visibility graph over inflated discs is the exact/corner-cutting upgrade if we ever
  want explicit routes.
- **MPPI would not help**: it mode-averages (can blend left/right routes into the rock);
  CEM mode-seeks — better for gap commitment. Neither escapes topology traps unaided
  (vanilla MPPI ~34% trap-escape in ablations). Keep CEM.
- Racing AIs (GT Sophy, F1TENTH, Linesight): corner-cutting always comes from a global
  line or learned value — never short-horizon reactive planning alone.

### 3.2 Reactive / gap layer
- **VO/ORCA: rejected** (multi-agent crowd tool; non-convex + over-conservative for
  static rocks with momentum). **DWA: rejected** (literally a 1-step constant-control
  subset of our MPC). Steal only the "can I still brake" admissibility framing.
- **Gap-based methods are the fit**: F1TENTH's disparity extender (inflate obstacle
  edges by half-width, drive at farthest safe ray, speed ∝ clearance) produces exactly
  the corner-shaving behavior we want, and with *analytic circles* the whole egocircle +
  disparity pass is closed-form and nearly free. Mujahed's *admissible gap* adds the
  feasibility filter (reachable given velocity/turn capability).
- **Coupling: proposals into the sampler, not a competing controller.**
  - Cheapest increment: GP-MPPI-style **subgoal replacement** (aim goal cost at the
    chosen gap point) — eliminated local-minima episodes (0 vs 6) in cluttered tests.
  - The literature's real answer: **Biased-MPPI** (RA-L 2024) — inject whole candidate
    control sequences from ancillary controllers/primitives as extra samples. CEM
    version needs no importance correction: just seed them into the sample set.
  - AERO-MPPI runs one sampler per gap anchor (homotopy coverage) — 2–3 anchors is our
    affordable version if single-sampler commitment still shows up.
- **Anti-fighting rules:** MPC stays sole control owner; reactive layer emits few
  discrete gap proposals (never blended commands); hysteresis lives at gap-selection
  (track gaps frame-to-frame, switch only on margin).

### 3.3 Shape-aware planning (the strafe-tilt question)
- Our `cos(strafe·maxBank)` footprint-from-control model is **literature-standard**
  (Liu et al. SE(3), RA-L 2018 — attitude from control via flatness, ellipsoid
  collision; produces emergent 40° roll through sub-diameter gaps). **The representation
  is fine; the sampler is the problem.**
- **Why cost-only fails** (all hypotheses confirmed): gap-knifing is a *narrow passage
  in control-sequence space* — a coordinated align→max-strafe→hold→unwind sequence has
  near-zero probability under i.i.d. Gaussian CEM noise; no elite ever threads the gap,
  so the optimizer never learns it's cheap; CEM covariance collapse locks onto the
  go-around homotopy class; effort costs charge a guaranteed price for a conditional
  reward.
- **Standard remedy: put the maneuver in the sampler** — inject scripted
  "bank-through-gap" primitive rollouts (Biased-MPPI pattern; solves with ~200 samples
  in the reference work). Plus CEM hygiene: strafe-channel variance floor,
  time-correlated/spline noise (so one draw can express "hold strafe 0.5 s"), keep the
  last successful gap-threading sequence as a warm-start seed.
- **Cheapest gap-attitude formulation** (Falanga ICRA'17): concentrate gap knowledge in
  one attitude-annotated waypoint + a dedicated traverse primitive; don't add bank as a
  planner state. Deterministic field ⇒ gap candidates (asteroid pairs with clearance
  between `shipWidth·cos(maxBank)` and `shipWidth`) can be annotated lazily/offline.
- **Explicitly avoid full 3D MPC** — no successful system in this problem class needs it.

### 3.4 Obstacle cost & sensing redesign (added after user feedback)
The threshold-potential cost was already effectively disabled in the live asset because
of boundary thrashing (§2). The research supports replacing, not re-tuning, it:
- **Racing/agile MPC practice uses (near-)binary collision penalties on rollout
  states**, not graded repulsion fields (4WIDS: binary collision cost; F1TENTH MPPI:
  track-bounds violation penalty; AutoRally likewise). Rollouts that hit are rejected;
  rollouts that miss are *not* punished for proximity. Corner-shaving then emerges from
  the goal/speed costs instead of being fought by a repulsion gradient — which is
  exactly the "fly close and tight" behavior we want.
- The "keep some clearance at speed" job moves to **speed shaping, not position
  shaping**: disparity-extender rule (speed target ∝ clearance along chosen heading) or
  DWA's admissibility framing (penalize states from which braking/turning away is no
  longer possible — a time-to-collision/stopping-distance test), both continuous in
  state with no magic boundary.
- **Sensing: query the deterministic field directly instead of physics overlap scans.**
  Asteroid placement is deterministic and queryable per chunk; a Burst job can gather
  every asteroid within a fixed AABB around the rollout tube (no speed-coupled radius,
  no collider round-trip, no set churn), plus dynamic ships from the registry. The
  top-8 ranked buffer can then become a per-rollout spatial cull rather than a global
  pre-rank, removing the other discontinuity.

### 3.5 RL feasibility (short)
- Full ML-Agents PPO: feasible (~10–30M steps, hours-to-a-day with parallel headless
  envs) but iteration-heavy on reward shaping, and retrains on any dynamics change.
- The attractive hybrid: **small learned terminal value in the same slot as the
  geometric field** (TD-MPC / Bhardwaj ICLR'21). But a terminal value doesn't fix
  sampling — if no rollout threads the gap, the value behind it is never queried.
  **RL comes after, not instead of, the sampling fix — and the terminal-cost seam is
  forward-compatible with it.**

## 4. Options compared

| | Option | Fixes | Cost | Risk | Verdict |
|---|---|---|---|---|---|
| A | Retune current MPC (weights, noise, sampler hygiene) | Some strafe suppression; eval baseline | Hours | Low | **Do first — Phase 0** |
| A′ | Obstacle cost redesign: collision-check + admissibility/speed shaping, deterministic-field sensing (replaces threshold potential) | Boundary thrashing, speed-margin feedback, scan churn; enables close/tight flying | Days | Medium | **Core — Phase 1** |
| B | Gap layer → seeded primitive rollouts (+ subgoal as fallback) | Gap-knifing, homotopy exploration, corner-shaving | Days | Medium (new gap detector, but analytic) | **Core — Phase 1** |
| C | Flow-field **terminal cost** (Dijkstra/FMM time-to-go per target, Burst job) | Topology blindness / trapping / long detours | Days (NavField core is resurrectable from `5f5a4530~1`) | Low-medium (well-precedented; units + staleness are the known traps) | **Core — Phase 2** |
| D | Bank-aware footprint fix (profile applies to hull radius, not padding) | Understated bank reward; model-physics consistency | Hours (solver-side) | Low | **Fold into Phase 1** |
| E | MPPI switch | — | Days | — | **Rejected** (mode-averaging worse for gaps) |
| F | VO/ORCA/DWA layer | — | — | — | **Rejected** (dominated/mismatched) |
| G | Full RL policy | Everything, eventually | Weeks+ | High | **Deferred** |
| H | Learned terminal value (RL-lite) | Residual myopia after C | ~Week | Medium | **Deferred; slot reserved by C** |

**Key architectural insight:** B and C attack *different, complementary* failures.
C tells rollouts what the world looks like past 1.7 s (routing); B makes the sampler
capable of drawing the thin-tube maneuvers that the cost already knows are cheap
(gap-knifing). Neither substitutes the goal; the MPC remains the only controller —
which is exactly what the old NavField got wrong.

## 5. Recommended phased plan

**Phase 0 — Sampler hygiene + cleanup (no new systems).**
Strafe variance floor + time-correlated noise in the sampler, delete `adaptiveDtScale`
(dead feature), review `relax*`. Do **not** re-tune the threshold obstacle cost — it's
being replaced in Phase 1 (§3.4). Establish the eval scenario first (see §6) so every
later phase is measured.

**Phase 1 — Obstacle model redesign + analytic gap layer + seeded rollouts.**
Replace the inflated-threshold potential with: (a) hard collision check per rollout
state (disc overlap vs `shipRadius·profileScale` — the honest bank model, applied to
the hull, with `shipRadius` no longer baked into obstacle radii), large penalty on hit;
(b) an admissibility/speed-shaping term (time-to-collision / stopping-distance along
current velocity, continuous — no boundary, no speed-inflated threshold). Sense
obstacles by querying the deterministic field per chunk within a fixed AABB around the
rollout tube (Burst job) instead of speed-coupled `OverlapSphere`. On top: closed-form
egocircle/disparity gap pass → top-k admissible gaps with hysteresis → inject per-gap
primitive rollouts (align → max strafe → hold → unwind) into the CEM sample set, exempt
from `relax*` attenuation. Optional feel lever: raise `maxBankAngle`.

**Phase 2 — Terminal cost-to-go field.**
Resurrect `NavField`'s Dijkstra core from `5f5a4530~1` (or FMM), Burst-jobbed, one field
per chase target shared by all pursuers, stored as time-to-go, re-solved on ~1-cell
target motion, bilinear lookup as terminal cost `w_T ≈ 1` (also consider a small
stage-progress term if pure terminal is weak at 17 steps). Delete nothing from Phase 1 —
the layers compose.

**Phase 3 (optional, later) — learned terminal value** replacing the geometric field in
the same slot, per the project's RL ambitions.

### Execution split
The plan runs as **two parallel PR tracks** (sequential within each; interface
contracts between them):
- **Track A (solver line):** `Chase_Nav_Track_A_Solver.md` — sampler hygiene/cleanup
  → obstacle cost redesign → gap layer + seeded primitives.
- **Track B (independent line):** `Chase_Nav_Track_B_Field_And_Eval.md` — eval harness
  (gates all merges) → deterministic-field sensing → terminal cost-to-go field.

## 6. Evaluation scenario (prerequisite)

Scripted chase benchmark in `BigFieldSettings`: scripted/recorded player path weaving
through the dense field; measure per-run (a) time-to-intercept / mean distance-behind,
(b) collision count & impact energy, (c) mean speed vs player's, (d) gaps threaded vs
detoured. Deterministic field + fixed seed ⇒ reproducible A/B between phases.

## 7. Open questions

1. `maxBankAngle` 35° → higher? Game-feel call; changes how often bank-gaps exist at all.
2. Should ships *destroy* small asteroids in the way (weapons as navigation) instead of
   always dodging? Changes the cost of "blocked" topology.
3. Multi-ship chases: one shared field per target is cheap, but do we want pursuers to
   take *different* gaps (homotopy diversity per ship) for flanking flavor?
4. Does the evade/flee mode need the ascending-gradient equivalent (old `RoutingMode.Evade`)?

## 8. Sources

Full research reports (with ~40 citations: POLO, TD-MPC, Biased-MPPI, AERO-MPPI,
GP-MPPI, Falanga ICRA'17, Liu RA-L'18, Fray context steering, F1TENTH disparity
extender, Nav2 MPPI, GT Sophy, …) were produced in-session:
- `research_mpc_global_guidance.md`
- `research_reactive_steering.md`
- `research_shape_aware_and_rl.md`
(scratchpad copies; promote alongside this doc if wanted.)
