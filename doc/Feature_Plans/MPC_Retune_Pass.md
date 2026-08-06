# MPC Retune Pass — instrumentation, bench, and contingent controller refactor

> STATUS: live arc — Slices A (#261), B (#260) and C all landed; the bench read is the open step.

Shape and slice briefs frozen 2026-08-05 with the user. Nav-field addback
DESCOPED (see Rulings). Structural controller work is CONTINGENT on the
Slice-B bench read — not yet authorized.

Entry evidence: memory `handoff_2026-08-04_k1_4_eval_mpc_navfield.md` (K1-4
run record, facing-authority + MPC-noise ablations, obstacle-competence gate
spec) and `project_anchored_k1_arc.md` (arc close-out, #250 disposition).
Predecessor plan: `Anchored_Intent_Architecture.md` (K=1 arc — CLOSED).

## Context

The anchored K=1 arc closed 2026-08-04: the anchoring thesis confirmed, K1-4
candidate staged, and the residual nose thrash located in the MPC controller,
not the policy interface. The user playtested the staged candidate 2026-08-05,
hand-tuned `MpcSettings_AgentPilot` (`noiseStd 0.75→0.31`,
`wSmoothnessYaw 0→0.2`, commit `965bc2ed` on the K1-3 branch), and cleared
PR #250 to merge with the tweaks (merge driven outside this pass).

Two facts frame this pass:

1. **The felt thrash improvement was the noise drop, not the smoothness
   weight.** At playtest time `Cost.SmoothnessCost` multiplied by `0.25·dt²`
   without dividing the control delta by dt, suppressing the claimed 0–1
   normalization by 100× at dt 0.1, so the hand-tuned `wSmoothnessYaw 0.2` was
   an effective 0.002. Slice C fixed the normalization and rescaled the asset
   to `0.002` in the same commit, so that operating point is preserved exactly
   and the knob is now live and dt-independent. **Every smoothness weight
   quoted before Slice C is in the old 100×-suppressed scale** — divide by 100
   to read it against the current asset.
2. **The solver already carries an unperturbed incumbent** (candidate 0 is the
   verbatim shifted warm start, `BurstSolver.GenerateCandidatesJob`, since
   #76). The 2026-08-04 handoff's contrary claim is corrected in place. The
   churn generator is *selection*: the one-pass elite **average** (~14
   candidates at eliteFraction 0.113 × 128 samples) blends the incumbent with
   noisy challengers every solve — no argmin, no incumbent preference, no
   hysteresis.

`noiseStd 0.31` sits near the frozen-policy ablation cliff (Dummy closeout
0W/3D at 0.25) yet plays well against a human. Every existing roster number
(K1-4's 66/75, the 699941 rebaseline 63.25) was measured at 0.75, so the
merged config needs a fresh instrumented baseline before any further
controller decision.

## Rulings (2026-08-05, user)

- **Nav-field addback DESCOPED entirely** — no bench, no schema change, no
  observation prior. The agent navigates the field acceptably. Re-raising it
  requires new evidence, not this pass.
- **No more scalar sweeps** (carried from the frozen decision queue). The
  playtest tune is the operating point; next moves are evidence-driven.
- **Pass shape: instrumentation → bench → decide.** Structural controller
  work (incumbent-preferring selection, deterministic candidate, slew
  projection) only if the bench pulls it.
- **Self-play remains unauthorized.**
- Slices A and B build in parallel (agent-4 / agent-5); bench *execution*
  gates on #250 landing.

## Slice A — `controller` probe (lease `retune-probe`, agent-4)

A new session probe registered beside `facing` in `SessionProbes`; probe-only
diff, zero production-code change. Observes existing seams: applied control
via `AICommander.Navigator` → `Navigator.lastControl`, obstacle buffer via
`Navigator.mpc.Solver.Obstacles` (single-assembly internals). Sampled per
fixed step in the `FacingSampler` pattern; per-episode JSONL row + pooled
per-opponent summary sidecar (`-controller.jsonl` / `-controller-probe.json`),
schema id `rl-controller-probe-v1`.

Metrics (the frozen instrument-before-judging set):

1. **Applied yaw torque** — mean |yawTorque|, strict sign-flip rate, and
   deadband-thresholded flip rate (flip counts once |torque| crosses
   `torqueDeadband` on the new side; param, default 0.1).
2. **Anchor angular velocity** — per-step `Cost.AnchorYaw` recomputed from
   both ships' kinematics + the agent's projectile speed; wrapped delta/dt;
   mean and p90 |anchor rate| (deg/s). Separates target-motion yaw demand
   from self-generated churn.
3. **Deadband-thresholded nose reversals** — hysteresis variant of the strict
   sign-flip metric: a yaw-rate flip counts once |yawRate| crosses
   `deadbandDegPerSec` (param, default 10) on the new side. Strict metric kept
   alongside for continuity with all existing rows.
4. **Obstacle-threat split** — each step classified by the solver's own
   collision-course gate (`Cost.TurnAwayCost > 0` against the live obstacle
   buffer; threat = any nonzero deficit). Nose/torque metrics reported split
   threat vs clear, plus the threat-step fraction.

Params ride the probe-param grammar (`controller(deadbandDegPerSec=10,...)`),
known-keys validated. EditMode tests on the sampler math (deadband hysteresis,
anchor-rate wrap, split accounting) per the `FacingSampler` test pattern.
Glossary: register the `controller` probe name (points at the symbol).

Schema-independence: reads kinematics + MPC internals only — no
`PolicyAction`/`IPolicyReadout` dependency — so it builds on pre-#250 main and
survives the K1-3 schema break unchanged.

## Slice B — bench driver (lease `retune-bench`, agent-5)

Python-side replicate driver + aggregator wrapping the existing eval lane —
one invocation runs the whole bench protocol with consistent naming and emits
the aggregate read that was hand-written for the 699941 rebaseline.

- Protocol (defaults, flag-overridable): canonical roster (5 archetypes × 15
  episodes, seeds 2001–2005 ×3, density 2.0) × `R` replicates (default 2) +
  the 15-episode mirror; probes on (list flag, default `facing`; bench runs
  pass `facing,controller` once Slice A lands).
- Aggregation: per-archetype and total mean/stdev across replicates, draw and
  timeout counts surfaced per archetype (the Dummy-closeout / mirror-
  disengagement watch), written as `AGGREGATE.md` + machine-readable JSON in
  the run root under `results/rl-eval/`.
- Seam rule: no parallel Unity-launch path — the driver reuses the eval
  lane's launch/coordinator machinery (wiring rule 6). Replicates run
  sequentially; base-port 5006 stays single-occupancy.
- Smoke-testable pre-#250 against current main (699941 + `facing` probe,
   1-seed short run).

## Slice C — SmoothnessCost normalization fix (LANDED)

One commit, sequenced after #250: the normalization became `deltaControl²/4`
(dt-independent) and every carrier was rescaled by dt² in the same breath, so
cost values are unchanged and the user-approved feel is preserved exactly.
Fix-first ordering was forbidden — it would have silently made the merged 0.2
a hundred times stronger.

Every carrier that reaches a live solve: the formula; `MpcSettings_AgentPilot.asset`
(`wSmoothnessYaw 0.2 → 0.002`; thrust/strafe were 0); and the `MpcSettings`
C# field defaults (`0.5/5.0/0.2 → 0.005/0.05/0.002`), which seed every newly
authored asset and `Navigator.Initialize`'s no-asset fallback. Both shipped
pilot prefabs point at the asset, so the defaults govern authoring and tests
rather than production. The `MpcCostRegroupEditModeTests` fixture keeps
old-scale literals deliberately — it builds a `Config` directly and pins only
`smoothness > 0`. Sweep-based retuning of the now-live knob is a later,
separate decision.

## The bench read → what it decides

Run after #250 lands (Slices A+B merged): instrumented baseline at the merged
config (noise 0.31). Questions it answers:

- Roster strength at the tuned operating point vs the retired 0.75-config
  numbers (new yardstick — old baselines retire at merge).
- Did closeout weakness worsen (Dummy draws, mirror disengagement) as the
  ablation cliff predicts, or does the playtest feel hold up against the
  roster too?
- Thrash decomposition: how much residual yaw motion is anchor-demand vs
  self-generated; threat vs clear split.

Outcomes: (a) tuned config is strong + smooth enough → pass closes cheap,
structural work deferred with evidence; (b) closeout/competence regressed →
the structural controller experiment (incumbent-preferring selection +
deterministic PD candidate + slew projection, obstacle-competence gate before
any solver-budget cut — spec in the 2026-08-04 handoff, as corrected) gets
scoped as its own slice set. That call is the user's, made on the bench read.

Velrebase apparatus disposition rides the same call: it is the natural
controller-A/B instrument if the structural experiment proceeds; if the pass
closes cheap, retire it per its board card.
