# Stage (ii) retrain — shaping, self-play, and the pursuit hole (2026-07-25/26)

> STATUS: live arc — carries the stage (iii) decision brief; PR-2 (eval gate) and both training launches outstanding

Findings from the stage (ii) training arc. Three defects were root-caused and fixed mid-arc; the
headline result is that the **Evader pursuit hole is solved** and that **self-play erodes it again**.

## Outcome

**Best checkpoint: `results/rl-training/ship_combat_stage2b/ShipCombat/ShipCombat-3199951`**
— deterministic eval, fixed density 2.0, seeds 2001–2005 × 3 episodes:

| | Aggressor | Orbiter | Kiter | Evader | Dummy | non-Evader | TOTAL |
|---|---|---|---|---|---|---|---|
| prior baseline | 15/15 | 13/15 | 14/15 | **0/15** | 15/15 | 57/60 | 57/75 |
| **3199951** | 15/15 | 15/15 | 15/15 | **14/15** | 15/15 | 60/60 | **74/75** |

Evader went from **0 wins across an entire prior run** (150 consecutive draws) to 12–14/15 at every
checkpoint scanned. Previous best-on-record was 58/60 non-Evader with Evader 0/15.

## Defect 1 — an empty BufferSensor is a policy-killer

Run 1 trained 600k steps at density 0, so the stage (ii) obstacle BufferSensor had **zero
observables in every observation**. At the first density step-up the policy collapsed across all
archetypes: damage vs the **stationary** Dummy fell 85.3 → 6.1 with 0% deaths and 0.7 damage taken
(so not obstacle-avoidance struggle), Orbiter deaths hit 96.6%, wins 639 → 1.

ONNX probe on the pre-collapse checkpoint, same combat state, varying only the buffer:

| perturbation | mean \|Δaction\| |
|---|---|
| enemy closes 40u → 15u | 0.024 |
| own health 100% → 25% | 0.020 |
| target lost entirely | 0.108 |
| **buffer: 0 → 8 rocks at the arena edge** | **0.207** |
| buffer: 1 → 32 rocks | 0.029 |

Eight tactically irrelevant rocks perturbed the policy **twice as hard as losing the target**, while
buffer *content* was invisible. The attention layer had learned a single "buffer is non-empty" bias
instead of how to read tokens.

**Fix:** density never reaches 0 (`density_low_teacher` 0.1–0.3). Confirmed by counterfactual — the
same density step-up that destroyed run 1 left run 2 *stronger* (deaths fell ~10 points across every
aggressive archetype).

**Corollary:** a long empty-arena curriculum phase is unsafe with entity attention, which is the
opposite of the intuition that motivated it.

## Defect 2 — `min_lesson_length` is global, not per-lane

`get_minimum_reward_buffer_size()` takes the **max across every lane's lessons** and makes that the
reward deque's `maxlen`; `need_increment` then averages the whole deque. A `min_lesson_length: 2400`
on the lethality lane silently widened the averaging window for **every** gate.

Symptom: the dummy lane sat at the ignition lesson while recent episodes averaged +0.73, because the
2400-episode window still held the run's early −3.0 episodes (806-ep mean −0.449 vs last-150 +0.748).

**Fix:** keep the max near the longest hold actually wanted (1200). Note the buffer clears on *any*
lane's advance (`trainer_controller.py:220`), so `min_lesson_length` counts episodes since the last
lesson change anywhere.

## Defect 3 — discounted potential shaping pays the agent to disengage

`PotentialShaping.Step` used Ng's `F = γΦ′ − Φ`. With γ=0.99 and Φ > 0 that leaks `(1−γ)·Φ` **every
decision** — a cost proportional to how *close* the agent is. Verified against recorded
`midPhiEnvelopeSum`: **80%** of the Evader shaping penalty was this drain (−1.570 of −1.958), versus
−0.557 for 23-second Orbiter/Kiter kills.

| behavior over a 600-decision draw | shaping total |
|---|---|
| hold at 60u | −1.80 |
| hold at 40u | −2.40 |
| hold at 110u | −0.30 |

Against a target it could not finish quickly, **disengaging was worth ~+2.0**. Only the Evader
suffered, because every other archetype closes the distance itself and those episodes end fast.

Stage (ii) had raised `envelopeK1` 0.1 → 0.5 specifically to strengthen pursuit; since the drain
scales with k₁ and accumulates over 600 decisions while the closing gain is one-time, **that change
made pursuit worse, not better**.

**The pre-registered "draw cost" lever would not have worked** — it shifts hovering and chasing by
the same constant, leaving the preference for staying far intact.

**Fix:** shaping telescopes **undiscounted** (`Φ′ − Φ`); holding costs exactly zero, closing pays
exactly ΔΦ, sum reduces to `−Φ₀ + Φ_end`. Trades strict Ng policy-invariance for a pursuit gradient
that points at the target. Verified live in-run: 8/8 episodes matched the new formula.

## Self-play erodes pursuit (mirror league, corrected diagnosis)

Self-play from the 74/75 seed, evaluated against the scripted roster at fixed density:

| checkpoint | non-Evader | TOTAL | Evader |
|---|---|---|---|
| SEED 3199951 | 60/60 | 74/75 | 14-1-0 |
| selfplay3 @ 200k | 56/60 | 69/75 | 13-2-0 |
| selfplay3 @ 400k | 57/60 | 65/75 | 8-6-1 |
| selfplay3 @ 600k | 59/60 | 65/75 | 6-9-0 |

Monotone erosion of the exact capability the arc was spent buying, while non-Evader stayed flat.
**ELO rose (1200 → 1244) throughout** — it cannot see an absolute capability being lost because the
reference set loses it too.

An earlier hypothesis — that the previous arc's "league of mirror-brawlers has no pursuit gradient"
diagnosis was really mismeasuring the shaping drain — is **wrong**. With shaping demonstrably fixed,
the league still erodes pursuit. Mechanism is visible in the mirror footage: two aggressive brawlers
close immediately, episodes resolve in ~11–13s with **zero draws in every density band**, so nothing
in the league ever runs and pursuit decays from disuse.

**Implication for any future self-play:** the league needs opponents that flee. A hybrid composition
(mirror matches plus a fraction of scripted-roster episodes weighted toward Evader) is the shape to
scope; widening `window`/`save_steps` does not help, because no snapshot runs away.

### League mechanics worth knowing

There is no mirror→league transition: `_swap_snapshots()` flips a coin every swap —
`play_against_latest_model_ratio` 0.5 → current policy, else a random past snapshot. Snapshots save
every `save_steps` into a **cyclic** `window`-deep pool, so diversity horizon = `window × save_steps`
(here 10 × 20k = a 200k trailing window; the seed is never retained). Snapshots inherit
`current_elo` at save time, so ELO measures improvement *relative to recent past selves* — a
treadmill, where flat ELO and a genuine stall are indistinguishable.

## Yaw thrash — `wSmoothnessYaw` ruled out

The policy's facing command is **already** a goal passed to MPC, not a hard override: it replaces the
auto-computed `InterceptYaw` as the *source* of `Config.facingTarget`, consumed as a cost weighted by
`wFacing`/`facingWidth`. The name `SetFacingOverride` describes replacing the source, not bypassing
the controller.

Direct thrash A/B (mirror probe counting yaw-rate sign flips):

| wSmoothnessYaw | reversals/s | mean \|yawRate\| |
|---|---|---|
| 0 | 2.99 | 101°/s |
| 1 | 3.14 | 99°/s |
| 3 | 3.21 | 98°/s |

**Damping does not reduce thrash** — flat-to-slightly-up across a 3× weight increase. The churn is in
the commanded target, not the controller's tracking. (An outcome-only eval sweep separately showed
damping costs no performance: 70/71/73 of 75, all within noise.)

**Fork RESOLVED 2026-07-26 — the churn is in the command (hypothesis 1).** Two hypotheses made
opposite predictions:
1. The nose faithfully tracks a churning command → fix is an **override weight**. The action space
   already carries one: `fx/fy` magnitude was discarded by `ToFacingRad`, so the policy
   could not express "I don't care", and near-zero vectors have wildly unstable angles. Scaling
   `wFacing` by `|fx,fy|` adds no action dimensions. Needs a retrain (semantics change).
2. The facing command is being *ignored* — `wFacing: 1` vs `wVelTrack: 50` is 1/50th authority, so
   the nose may be swinging to serve velocity. Then the fix is raising `wFacing` first.

The facing probe measured both: cmdDelta mean 48°/decision ≈ tracking error 50.9° (n=2080) — the
commanded target moves per decision about as much as the nose lags it. A `wFacing` ×1/×5/×15 sweep
ruled out authority (hypothesis 2): error fell only 50.6° → 45.5° while reversals/s **rose**
2.70 → 3.69. Fix = the stage (iii) package's F1 below: `|fx,fy|` becomes the MPC facing authority,
so the policy can express "don't care" instead of emitting churning near-zero angles.

## Methodology notes

- **Eval noise floor is ~±4/75.** An identical re-run of the same checkpoint/seeds/weights scored
  70/75 where an earlier run scored 74/75, despite `DeterministicInference` and pinned seeds.
  Checkpoint rankings within a few wins are not meaningful; the Evader signal (0 → 14) is far
  outside it.
- **Never read the blended mean reward.** All three defects were invisible in it and visible
  immediately in per-archetype episode records. The Dummy column is the cleanest diagnostic — a
  stationary target that the agent stops hitting cannot be explained by opponent behaviour.
- Self-play mean reward sits near 0 by symmetry; it is not a progress signal.

## Dashboard

`dev/rl-status/server.py --run-id <run> --port 8765` — read-only status page regenerated per request
(steps/ETA/throughput, curriculum lessons, per-archetype table, failure signatures). Self-play mode
swaps in an ELO card and a density-band table. Episode groups are bound to the run's active window,
since JSONL groups are named by wall-clock and a live run otherwise leaks into a finished run's view.

## Stage (iii) retrain package — decision brief (2026-07-26)

Frozen at pr-prep 2026-07-26; the implementing PR builds it, it does not re-decide. Seeded by the
findings above plus the facing probe (cmdDelta 48°/decision ≈ tracking error 50.9°; the wFacing ×15
sweep ruled out authority — summaries in `results/rl-eval/20260726-16*-facing-probe-summary.json`).

### Scope

1. `|fx,fy|` magnitude → MPC facing authority (policy expresses "don't care"; kills command churn).
2. Hybrid self-play league: per-worker scripted/mirror split so pursuit keeps a gradient.
3. Automated absolute eval gate so erosion is visible without ELO.

**Non-goals:** heat curriculum, obstacle token cap, production legacy-shim replacement,
rock-shooting incentives, per-episode ghost mixing, evaluator archetype weighting.

### Locked forks (with why)

- **F1 = 1a, action boundary.** `facingWeight = clamp01(|fx,fy|)` computed at the decision
  boundary (`ShipAgent`/`AgentActions`, where action semantics are single-sourced);
  `AgentChooser.SetAction` gains the param; chooser emits the existing
  `WeightOverride{MpcWeight.Facing, ×w}` on the intent (cache the array; mutate in place).
  MPC/Navigator untouched. Full magnitude = ×1: the settings asset's `wFacing` stays the
  authority ceiling (post-train tunable without retrain). Plain clamp01, NO deadzone
  (a deadzone re-adds a discontinuity; near-zero magnitude already ≈ zero weight).
- **F2 = 2a, fresh full run.** Phase A: fresh 3.5M curriculum (`ppo_ship_combat.yaml`, #218
  fixes) under the new semantics; also first end-to-end validation of the repaired yaml.
  Phase B: hybrid self-play from phase A's final checkpoint (`--initialize-from`).
  Why: clean semantics from ignition; the 74/75 seed's value is partly voided by the
  semantics change; mlagents' squashed actions start near-saturated |v| ⇒ full authority at
  init, no behavior cliff.
- **F3 = 3a, per-worker split.** `TrainingHost` already derives worker index from the port
  offset; new env knob `RL_HYBRID_SCRIPTED_WORKERS=k` ⇒ workers k'<k boot
  `ScriptedRosterComposition`, rest `SelfPlayComposition`. Boot-frozen invariant preserved;
  launcher flag in `run_parallel.py` sets the var. Why: smallest principled wiring; ratio
  needs no runtime adaptivity for a first hybrid run.
- **F4 = 4a, automated sidecar gate.** Script (home: `training/rl/`, beside run_parallel)
  watches checkpoint exports; per ~200k-step checkpoint runs the standard deterministic
  75-ep scripted eval on a pool slot via the coordinator (acquire per eval, release after).
  Alert + stop rule below. Why: selfplay3's erosion was invisible until manual eval; ELO is
  a treadmill.

### Blindsider resolutions

- **B1:** gate signal = standard 75-ep eval + **two-consecutive-checkpoints rule** (≈30
  Evader episodes per decision); evaluator untouched.
- **B2:** **2 of 6 workers scripted** (~33% pursuit-gradient experience).
- **B3:** hybrid phase budget **1.5M steps** with the gate armed.

### Assumptions (confirmed)

1. Action spec stays 6-continuous; observations unchanged (ONNX shape identical).
2. New `ppo_ship_combat_hybrid.yaml`: self_play block + density sampler band (0.5–2.5, min>0)
   + lethality 1.0 + `opponent_weight_*` present, Evader-dominant ≈ {Evader 0.5, Aggressor
   0.2, Orbiter 0.15, Kiter 0.15, Dummy 0.0–0.05}; config-family EditMode tests extended
   (the selfplay yaml's roster-free assertion stays selfplay-specific by filename).
3. Stop rule numbers: alert at Evader ≤10/15; stop on two consecutive checkpoints Evader
   ≤10/15 OR total <55/75. Baseline: seed-class policies score Evader 12–14/15, total ~70–74.
4. Fleet mechanics per the run-mechanics memory runbook: parallel players `--num-envs 6`,
   base-port 5006 single-occupancy, rebuild exe after the code merge, clear BurstCache.
5. `ToFacingRad` degenerate handling unchanged (weight ~0 makes the angle moot); heuristic's
   unit bearing = full authority — both existing tests stay valid.
6. Acceptance test for F1: re-apply `training/archive/patches/facing-probe-scratch.patch`
   (extend to log emitted |fx,fy| via the chooser's stored weight) — expect low-|v| commands
   while maneuvering, high while aiming, reversals/s well under 2.7, and kill-time no worse.
7. **Smoke-verify before any spend:** `run_parallel.py --smoke` in hybrid mode must prove the
   ghost trainer tolerates scripted workers emitting team-0-only trajectories. If it chokes,
   F3 is void — stop and redesign, do not work around silently.
8. Production shim untouched; shipping to gameplay is a separate arc.

### Sequencing

PR-1 (code): F1 seam + F3 TrainingHost/launcher + hybrid yaml + tests + this brief into the
findings doc. PR-2 (tooling, may fold into PR-1): F4 gate script. Then: rebuild exe → hybrid
smoke → Phase A launch (user approval = spend) → Phase B launch (user approval = spend).
