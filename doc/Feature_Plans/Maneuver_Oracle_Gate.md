# Maneuver-Oracle Gate — PR-2a Implementation Plan

**Date:** 2026-07-12 (scoped via grill)
**Parent:** `Tactical_AI_Audit_And_Roadmap.md` §4′ PR-2, split per grill into **PR-2a**
(this doc — the go/no-go gate) and **PR-2b** (reward + episode/reset, scoped *after* the
gate result). Builds directly on the PR-1 velocity interface (`MPC_Velocity_Reference_Mode.md`,
merged #122).
**Status:** Scoped, not started.

> **One-line intent.** Before any reward/ML spend, prove that closed-loop *velocity commanding*
> — the PR-1 seam a learned policy will drive — can produce **held** orbit / break / range
> maneuvers. A cheap, editor-only, fail-fast gate. If it fails at every sane tracker tuning,
> we stop and rethink the interface before PR-2b/PR-3.

---

## Why this is a gate, not a feature

The roadmap's PR-2 bundles the maneuver oracle (a go/no-go *before* reward/ML) with the reward
+ episode/reset layer. That's contradictory: if the interface can't hold a maneuver, the
reward machinery built alongside it is wasted. So the oracle is split out as **PR-2a** and lands
first. PR-2b (reward/reset) is gated on PR-2a passing and is scoped afterward, informed by what
the oracle finds.

The gate answers one question: **can a 2D velocity command span orbit / break / range at a sane
tracker tuning?** PR-1 already found tracking is *soft* at `wVelTrack=5` (strafe authority
~0.2 m/s for a 12.5 m/s perpendicular command), and orbit-while-aiming is *pure strafe*
(intercept-yaw pins the nose on the target, so tangential velocity ⟂ facing = the weak axis).
So the honest question isn't "does it orbit at `wVelTrack=5`" but "does it orbit at *some* sane
`wVelTrack`" — which makes `wVelTrack` the primary sweep variable, and the failing/passing value
the useful output feeding PR-3's reward tuning.

---

## Design decisions (resolved via grill)

1. **Split.** PR-2a = runner + oracle gate. PR-2b = reward + episode/reset, scoped after the gate.
2. **Hand-authored oracle first; CMA-ES deferred.** Three hand-written velocity-goal controllers.
   CMA-ES only if a hand-authored maneuver is *ambiguous* (can't tell bad tuning from a real
   ceiling). Structure the controllers with exposed params so CMA-ES *could* wrap them later.
3. **Scenario:** one ship-under-test + a single stationary dummy target, **no firing / no HP**.
   **Empty space is the primary gate**; the deterministic asteroid field is a *secondary
   characterization* (config toggle), non-gating — obstacle-avoidance eroding a maneuver is
   informative for PR-2b's training env but doesn't red-light the interface.
4. **Metrics = envelope, not tight bounds** (mirrors PR-1's fidelity philosophy). Pass = metric
   met at *any* swept `wVelTrack ∈ {5,20,50,100}`, reporting which value it needed.
5. **Command path = the real learner path.** A scripted `ManeuverChooser : IIntentChooser`
   emitting `NavigationIntent{goalMode=VelocityReference}`, driven by the ship's own
   `AICommander.FixedUpdate` loop (`Decide → ApplyIntent → ComputeCommand → Pilot`). Proving a
   scripted chooser holds a maneuver through this exact path means PR-3 swaps only the chooser
   *body* (scripted math → ML inference), zero plumbing risk.
6. **Decision cadence fixed at 5 Hz** (recompute `v_ref` every 10 physics steps; MPC tracks
   between). No cadence sweep — orbit vs a stationary dummy has no transient to stress cadence;
   the cadence knee is a PR-3 question against a live opponent.
7. **Dummy aims (bounded turn-rate).** A stationary *non-aiming* dummy makes "break" degenerate
   into the strafe number PR-1 already has. A bounded-rate aimer makes break a real *out-turn*
   test ("generate enough LOS bearing-rate to exceed the dummy's tracking") — the tactical
   primitive break exists to prove. Still pure geometry (no fire/HP).
8. **Home: new `Game.RLHarness.Editor.asmdef`** (editor-only, refs runtime `GameCore` one-way).
   Nothing ships. **Zero runtime/production change** (see wiring below). Existing
   `ChaseBenchmark` migration into this asm is a deferred follow-up, not part of PR-2a.
9. **Gate mechanics:** always-on **smoke** (guards the harness runs) + opt-in **sweep**
   (`ORACLE_SWEEP=1`, writes JSONL). **Go/no-go is a human read** of the JSONL + a findings
   section appended here. No hard CI assertion on the maneuver holding (the gate is a one-time
   directional decision, not a regression invariant).

---

## The zero-runtime-change wiring (verified against current code)

The whole harness is editor-only. It touches **no** file under runtime `GameCore`.

- **Spawn** via `ShipTestFactory.CreateDefaultShipAt` (code, no prefab). `AICommander`
  no-ops its init until `SetRegistry` (registry null after `CreateShip`) — the window to
  install overrides.
- **`wVelTrack` override:** `Mpc.RefreshConfig` re-reads `settings.ToConfig()` every solve but
  captures the `settings` *reference* at construction, so clone before init:
  `nav.mpcSettings = Instantiate(nav.mpcSettings); nav.mpcSettings.wVelTrack = sweepValue;`
  (`Navigator.mpcSettings` is a public field). Assign **before** `SetRegistry`.
- **Chooser install:** `Brain.chooser` is a private `[SerializeReference] IIntentChooser` with a
  getter only. Install the editor-only `ManeuverChooser` via reflection from the harness
  (`typeof(Brain).GetField("chooser", NonPublic|Instance).SetValue(brain, chooser)`) **before**
  `SetRegistry` — so `Brain.Initialize` sees a non-`IStateChooser` and skips state-profile init
  (the "continuous policy" path `Brain.cs:54` already supports). One well-contained reflection
  line in editor-only code keeps `Brain` untouched. (Fallback if brittle: a `ResetForTesting`-style
  internal seam — precedent exists — but reflection is the zero-footprint default.)
- **Aim in velocity mode:** `tacticalEnabled = goalMode != VelocityReference` is goalMode-derived
  (`MpcSettings.ToConfig:188`), independent of the intent's `applyTacticalCosts`. Intercept-yaw
  aim needs `enemyYaw` non-NaN + `projectileSpeed>0`, populated by `Navigator.SetEnemyState`,
  which `ApplyIntent` calls when `applyTacticalCosts && hasTarget`. So the chooser sets
  `applyTacticalCosts=true, hasTarget=true, target=dummy, projectileSpeed>0` **only to feed aim** —
  the tactical block stays off regardless (goalMode-derived). Intent stays idempotent.
- **Order:** `CreateShip` → set `nav.mpcSettings` clone → reflect-install chooser → `SetRegistry`
  → tick `FixedUpdate` under `Time.timeScale`. The ship flies under its own `AICommander` loop.

---

## Components (all in `Game.RLHarness.Editor`, except the test driver)

- **`ManeuverChooser : IIntentChooser`** — the scripted velocity-goal policy. Holds the dummy
  `Transform` + maneuver kind + params (injected by the driver). Recomputes `v_ref` at 5 Hz,
  returns the held intent otherwise. Emits `NavigationIntent{ isValid=true,
  goalMode=VelocityReference, velocityReference=v_ref, hasTarget=true, target=<dummy EnemyTarget>,
  applyTacticalCosts=true, projectileSpeed=<self weapon speed or a fixed value> }`.
  - **Orbit(R, dir):** `v_ref = v_orbit·tangent_hat(dir) + kRadial·(R − r)·inward_hat`, where
    `tangent_hat ⟂ LOS`. `v_orbit` ~ a fraction of maxSpeed; `kRadial` a P-gain.
  - **Break(ω-context):** `v_ref = v_max·perp_hat` where `perp_hat` is the LOS-perpendicular
    direction that most rapidly increases the dummy's required aim angle (drives it out of the arc).
  - **Range(r_d):** `v_ref = kRange·(r − r_d)·(±LOS_hat)` (radial in/out) + light damping.
- **`DummyTarget`** — plain `GameObject` at a fixed pose. Optional bounded-rate aim: each
  `FixedUpdate`, rotate facing toward the ship-under-test capped at `ω_aim` (used by Break).
  Wrapped into an `EnemyTarget{ kinematics:{pos, vel:0, yaw, yawRate:0}, dynamics:<default/self-copy>,
  source:transform }` by the chooser.
- **`OracleRunConfig` / `OracleRunResult`** — mirror `ChaseRunConfig`/`ChaseRunResult`: maneuver
  kind, `wVelTrack`, radius/range/`ω_aim` params, field on/off, duration, tag; result carries the
  per-maneuver metrics + pass flags + `ToJsonLine()`.
- **`OracleMetrics`** — accumulated by the driver each `FixedUpdate` from ship↔dummy geometry:
  radius series (mean/‖err‖ vs R after a settle window), net angular progress (∫dθ), exposure
  angle series (dummy-forward vs LOS), settle time + steady-state error + limit-cycle variance.
- **`ManeuverOraclePlayModeTests`** (in `Tests.PlayMode`, refs the harness asm) — the
  `Time.timeScale`-driven headless driver (mirror `ChaseBenchmarkPlayModeTests`: `SetUp`
  timeScale=20 + maxDelta, spawn, install overrides, tick to duration, snapshot, teardown).

## Metric definitions (envelope thresholds — tune during build)

- **Orbit** — after a settle window (~first 3 s ignored): `mean|r−R|/R < ~0.20` **and** net
  angular progress `≥ 2π` (≥1 revolution). Both required (holding radius while parked ≠ orbit).
- **Break** — exposure angle (between dummy aim-forward and LOS-to-ship) exceeds the arc
  half-width within `T ~ 2 s` and stays out over the final window. Tests strafe authority
  out-turning `ω_aim`.
- **Range** — `|r − r_d| < tol` reached within a settle window and held; final-window variance
  small (no limit cycle).

Thresholds are envelopes, not tight bounds; report the *value* and the min passing `wVelTrack`,
not just pass/fail.

## Gate execution

- **Smoke (always-on):** one short orbit episode; assert plumbing produced sane metrics (radius
  sampled, ship moved, chooser drove it). Guards the harness; a durable invariant worth a test.
- **Sweep (opt-in `ORACLE_SWEEP=1`):** {orbit, break, range} × `wVelTrack ∈ {5,20,50,100}` ×
  {empty, in-field} → one JSONL row each under `results/maneuver-oracle/`.
- **Go/no-go = human** from the JSONL + a **Findings** section appended to this doc. Pass = each
  maneuver metric met at some sane `wVelTrack`; report the needed value. If even `wVelTrack=100`
  can't hold aimed orbit, that's the real red light (escalate to CMA-ES / free-yaw diagnostic /
  reconsider the interface).
- **Aimed-orbit is the gate; free-yaw orbit is a diagnostic** row (localizes a failure to
  strafe-authority vs the interface itself) — only run if aimed orbit fails.

---

## Deferred (recorded so lessons outlive the PR)

- **CMA-ES** over the maneuver-chooser params — only if a hand-authored maneuver is ambiguous.
- **Cadence sweep** → PR-3 (needs a live/shooting opponent; a stationary dummy can't stress it).
- **`ChaseBenchmark` migration** into `Game.RLHarness.Editor` — board card, after PR-2a.
- **Pinned maneuver-regression assertion** at the passing `wVelTrack` — only once the sweep
  reveals that value; not before.
- **PR-2b (reward + episode + atomic reset)** — scoped after the gate result.

## Appendix — files (all new, editor-only)

- `Game.RLHarness.Editor.asmdef` (+ meta) — new editor assembly, refs `GameCore`.
- `ManeuverChooser.cs`, `DummyTarget.cs`, `OracleTypes.cs` (config/result/metrics) — harness asm.
- `ManeuverOraclePlayModeTests.cs` — `Tests.PlayMode` (add ref to the harness asm).
- No runtime `GameCore` file is modified.
