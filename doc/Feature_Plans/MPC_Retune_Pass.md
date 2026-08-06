# MPC Retune Pass — instrumentation, bench, and contingent controller refactor

> STATUS: live arc — Slices A (#261), B (#260), C (#327) landed; Bench-1 read 2026-08-06 → GO on structural work, mechanism spec RESCINDED same day; open step = the two lever tests, then redesign from §"Structural slice — problem brief".

Shape and slice briefs frozen 2026-08-05 with the user. Nav-field addback
DESCOPED (see Rulings). Bench-1 (2026-08-06) pulled the structural work — GO —
and the inherited mechanism spec was rescinded the same day; the structural
slice starts from §"Structural slice — problem brief" below, not the
2026-08-04 handoff spec.

Entry evidence: memory `handoff_2026-08-04_k1_4_eval_mpc_navfield.md` (K1-4
run record, facing-authority + MPC-noise ablations, obstacle-competence gate
spec) and `project_anchored_k1_arc.md` (arc close-out, #250 disposition).
Predecessor arc: K=1 anchored intent — CLOSED 2026-08-05; its brief was
deleted with the arc, narrative in memory `project_anchored_k1_arc.md`.

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

## Rulings (2026-08-06, user)

- **GO on structural controller work** off the Bench-1 read (outcome (b)
  below); roster/thrash numbers in memory `project_mpc_retune_pass.md`
  §Bench-1.
- **Mechanism spec RESCINDED** the same day, during structural-slice pr-prep:
  the 2026-08-04 handoff's 8-step direction (incumbent-preferring selection,
  deterministic PD candidate, slew projection, lexicographic priority) had
  accreted into an over-convoluted shape — hysteresis plus collision-bypass
  plus obstacle-proximity-conditioned exploration. The redesign starts from
  the problem brief below: evidence only, no solution priors.
- **"No more scalar sweeps" re-opened narrowly** for the two never-run lever
  tests (§problem brief → Next step). The 2026-08-05 ruling predated the
  discovery that `wSmoothnessYaw` was 100×-suppressed during every sweep that
  motivated it.
- **Velrebase apparatus: HELD** — disposition rides the redesign (#289 does
  not fire yet).

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

> RESOLVED 2026-08-06: Bench-1 ran at noiseStd 0.75 (the #250-merged config);
> read in memory `project_mpc_retune_pass.md` §Bench-1. Outcome (b) selected —
> then the mechanism spec it pointed at was rescinded; see Rulings
> (2026-08-06) and the problem brief below.

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
closes cheap, retire it per issue #289.

## Structural slice — problem brief (2026-08-06)

Design re-opened from this brief. The 2026-08-04 handoff's mechanism spec is
RESCINDED — do not treat it as direction; the next design starts from the
evidence below and nothing else.

### The problem

The MPC's yaw-torque command chatters far faster than the hull can answer, and
the cost lands on closeout (all numbers Bench-1, `controller` probe, noiseStd
0.75, @ `7cd7b95a`):

- Torque-command sign flips ~11/s strict, 7.7–8.5/s after the 0.1 deadband, vs
  hull nose reversals 4–5/s — the command reverses ≈2.4× faster than the ship
  responds.
- The churn is self-generated: anchor demand explains only 17–31% of delivered
  yaw outside Orbiter (60%).
- It has no obstacle excuse: threat steps ≤0.06% on four of five archetypes.
  Evader's 4.9% threat share is *legitimate* avoidance (threat-bucket yaw
  98.6 deg/s vs 67.2 clear) — preserve it; it is signal.
- Cost: Dummy closeout 6.50/15, every non-win a 120 s timeout. **Success
  criterion = Dummy closeout**; no-regress bar = the moving-archetype wins
  (Aggressor 15.0, Evader 14.5, Orbiter 13.5, Kiter 13.5).
- Mirror went 0W/4L/11D (was 15/15 draws) — a 0/4 asymmetry in a self-mirror
  is unexplained; don't lean on mirror numbers until it is.

### Mechanism evidence (no fixes chosen)

1. **Selection blends.** The solver output is a one-pass elite *average* (~14
   of 128 candidates at eliteFraction 0.113) — no argmin, no incumbent
   preference (`SolverBuffers.EliteAverage`). Candidate 0 IS the verbatim
   shifted warm start (`GenerateCandidatesJob`, since #76).
2. **Plan fast-forward** (found 2026-08-06, magnitude unmeasured). The solver
   re-plans every 0.02 s fixed step (`AICommander.FixedUpdate` →
   `Navigator.ComputeCommand`) but `Mpc.ShiftSequenceForward` consumes one
   0.1 s rollout step per solve — the warm start advances plan time 5× faster
   than sim time; a 1.7 s plan is consumed in 0.34 s, and every solve's "hold
   u[0] for 0.1 s" prediction is executed for 0.02 s. Knot arithmetic is
   consistent with the measured chatter: 5 noise knots over 17 steps = a noise
   feature every 4 rollout steps, replayed at 50 Hz ≈ 12.5 sign-change
   opportunities/s vs 8.99–11.75/s measured strict torque reversals.
   Observation only; no fix chosen.
3. **Noise is causal but protective** (the one falsified lever). Frozen-policy
   Dummy ablation: noiseStd 0.75 → 2W/1D; 0.50 and 0.25 → 0W/3D. Low noise
   halves yaw motion AND kills closeout; 0.31 also broke Dummy station-keeping
   at the #250 merge gate. This is the real constraint behind "don't just turn
   the noise down".
4. **The smoothness lever was never actually tested.** `wSmoothnessYaw` was
   100×-suppressed during every ablation ever run (fixed by #327); the
   strongest effective value ever tried ≈ 0.2 (old "smooth 20"), which did
   nothing. Untested at meaningful strength on the now-live knob.
5. **`wMomentum` has never been ablated.** It exists (velocity-direction
   change penalty — velocity channel, not yaw), ships at 0.
6. Constraint honesty: the inherited "keep horizon 1.7 s / 128 samples /
   noiseStd 0.75, never cut solver budget" rule was a prior, not evidence.
   The evidence is #3, plus: obstacle competence is unmeasurable at density
   2.0 (threat steps ≤0.06%) — any change that could plausibly trade it needs
   a higher-density arm to be measurable at all.

### Measurement kit (exists — reuse, don't rebuild)

- `controller` probe (#261): per-step torque/nose reversal rates (strict +
  deadband), anchor rate, obstacle-threat split — answers "did the chatter
  drop" on a short run, no roster needed.
- `bench_replicates.py` (#260): full roster ×R + mirror protocol.
- Baseline for any before/after:
  `results/rl-eval/bench-ShipCombat-3500018-20260806-001449/` (@ `7cd7b95a`,
  probes `facing,controller`). ⚠ #263 (field pass) merged after the K1-4
  yardstick — Bench-1 is the comparable baseline, not K1-4's 66/75.
- Base-port 5006 is single-occupancy machine-wide; claim it in the active-work
  ledger before any run.

### Open question

What is the cheapest change — tuning or structural — that drops the
self-generated torque churn and fixes Dummy closeout, without regressing the
moving-archetype wins or (unmeasurable at d2.0) obstacle competence?

### Next step — the two never-run lever tests

Before any structural design: asset-only falsification runs, short Dummy +
mirror sessions, read on the controller-probe metrics.

1. `wSmoothnessYaw` at meaningful strength — sweep upward from 0.002; the knob
   is live and dt-independent since #327.
2. `wMomentum` on — velocity-channel; it may not touch yaw at all, which is
   exactly what the test establishes.

If either moves torque reversals without killing closeout, the structural
design shrinks or vanishes. (This re-opens the 2026-08-05 "no more scalar
sweeps" ruling — user-approved 2026-08-06; that ruling predated the discovery
that the smoothness knob was dead during the sweeps that motivated it.)
