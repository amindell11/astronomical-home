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

### Confirmed lever-test protocol (2026-08-06, user-approved)

**Arms** — one weight per arm, all else stock (noiseStd 0.75), strongest-first
adaptive: S-20 (`wSmoothnessYaw` 20) and M-50 (`wMomentum` 50) run first. A
strongest arm with no torque-reversal movement falsifies its lever and skips
its weaker arms (S-2, S-0.2 continuity anchor; M-5); a chatter drop that kills
closeout triggers a bisect instead. Magnitude grounding: `SmoothnessCost` and
`MomentumCost` are both normalized 0–1, so 20 prices a full one-step torque
reversal at 5 (50× a max-effort step's total effort cost) and 50 puts
course-holding at `wVelTrack` parity — deliberately overpowered so "does
wMomentum touch the yaw channel at all" gets a definitive answer.

**Per arm**: `eval_lane.py` ×2 — `--opponent Dummy` then `--opponent mirror`,
seeds 2001-2005, `--episodes-per-seed 3`, density 2.0, `--probes
facing,controller` (the Bench-1 cell protocol, 15 episodes each). Pool slot
pinned @ `7cd7b95a` so the asset line is the only delta vs baseline. The
weight is edited in the slot tree's `MpcSettings_AgentPilot.asset` per arm
(each eval_lane run is a fresh editor boot, so import is guaranteed), restored
via `git checkout --` and verified clean before slot release; never committed.

**Artifacts**: `results/rl-eval/levers-20260806/<arm>/{dummy,mirror}` (arm
dirs `smoothyaw-20/-2/-0p2`, `momentum-50/-5`) + top-level `NOTES.md` carrying
per-arm asset value + asset md5, checkpoint md5, SHA, session dirs.

**Read per lever** — primary: strict + deadbanded torque rev/s vs baseline
(Dummy 11.52/8.30, mirror 11.75/8.54; hull 4–5/s); *material* = ≥~25% drop,
outside the 8.99–11.75 cross-archetype spread (per-step metrics aggregate
~70k steps/session, noise ~±0.5/s). Guards: Dummy wins ≥~5/15 (baseline 6.5,
15-episode noise ±~2) and facing error not materially above 14.5°. Verdicts:
chatter drops and closeout holds at some value → the tuning path lives and the
structural design shrinks (refined sweep next); no movement at the strongest
arm → lever falsified, recorded here, structural design proceeds. Mirror W/L
is not leaned on (0W/4L anomaly); mirror chatter metrics are valid.

**Scheduling**: queued on base-port 5006 behind trainer-1b's curriculum
canary + stock arm; the active-work ledger row is the claim.

### Lever-test results (RAN 2026-08-06) — both levers insufficient

Arms run: S-20, M-50, M-5 bisect (S-2/S-0.2 skipped — no bisect trigger).
Full reads: `results/rl-eval/levers-20260806/NOTES.md`; summary in the pass
memory. **The tuning-only path is now closed by evidence, not by prior:**

- **wSmoothnessYaw 20** (full one-step reversal priced 50× a max-effort
  step): strict torque reversals −14%/−7% (Dummy/mirror), deadbanded
  −22%/−19% — sub-material, nowhere near the hull's 4–5/s. Closeout held
  (Dummy 8W/0L/7D). The churn is not price-sensitive → consistent with plan
  fast-forward, not smoothing pressure, as the generator.
- **wMomentum couples into yaw but is lethal at any effective strength.**
  At 50: strict −23%/−25% but Dummy 3W/5L/7D with *new* field deaths (33 s
  episodes, threat steps 18× baseline). At 5: lethality persists (8L/15
  Dummy) while the chatter benefit vanishes. Velocity-direction freedom is
  load-bearing for obstacle survival (constraint #6, now measured).
- Side observation: S-20's mirror returned to full disengagement
  (0W/0L/15D) — the baseline's 0W/4L mirror asymmetry is
  smoothness-sensitive and remains unexplained.

Structural redesign proceeds from §"Mechanism evidence"; these two
falsifications are additional design inputs.

### Shift-cadence falsification (RAN 2026-08-06) — mechanism CONFIRMED

User-approved same day as the opening move of the structural slice: a
12-line change (agent-1 `ecc62c2d` @ `7cd7b95a`) makes the warm-start shift
consume one rolloutDt slot per rolloutDt of *sim time* instead of one per
50 Hz solve. MPC EditMode tests 66/66; lever-test protocol, stock asset;
full read `results/rl-eval/shift-cadence-20260806/NOTES.md`.

**Plan fast-forward was the churn generator.** Strict torque reversals
11.5 → 5.7-5.8/s (−49/−52%, at the hull's own 4-5/s scale), deadbanded
−59/−61%. **Dummy closeout 6.5 → 14W/0L/1D**; episodes close 2× faster.
The mirror engages and is *symmetric* again (4W/4L/7D vs the unexplained
0W/4L/11D) — the baseline asymmetry was a churn artifact. Motion style
changed: committed sweeping turns (mean |yaw rate| 2.4×, facing error
14.5° → ~39° mean / ~70° p90) instead of micro-thrash on target. The lever
tests' price-insensitivity is explained — no cost weight can fix a wrong
time base.

**Roster no-regress bench (RAN 2026-08-06): ❌ FAILS as a hot-swap.**
`results/rl-eval/bench-shift-cadence-20260806/` (Bench-1 protocol, R2 +
mirror): **43.50/75** vs baseline 63.00. Dummy replicates (14.00/15,
+7.5); movers collapse — Aggressor 5.00 (−10) and Orbiter 4.50 (−9) with
zero draws and 15–23 s episodes (fast deaths, not timeouts), Kiter −4,
Evader −4. Mirror 3W/3L/9D symmetric. Reading: the checkpoint was trained
on the churny controller — calming the controller breaks the
policy+controller couple against movers (and/or committed turns trade
lateral tracking; facing error tripled). **Disposition: the cadence fix is
a training-environment candidate (retrain on top, #263-style env shift),
not a drop-in swap.** Landing gates as a hot-swap are moot; the paired
d3.0 obstacle arms (stock vs fix, same policy) still run — the threat-
metric comparison is policy-light and feeds the redesign.

**Post-film correction (user read, 2026-08-06) — the oscillation moved
down-spectrum; it did not go away.** Reversal-rate × yaw-rate arithmetic
agrees: old ≈ ±5–9° swings at 5.2 rev/s (facing error 14.5°), new ≈
±20–40° at 2.3 rev/s (facing error 39°) — the same yaw limit cycle at
half the frequency and ~4× amplitude. Mechanically the cadence change
makes coherent intent update at ~10 Hz (candidate 0 + elite-average pull
center five consecutive 50 Hz solves on the same plan alignment), so it
traded a 50 Hz-jittered controller for a ~10 Hz one. Dummy improved
because slow large sweeps translate toward a stationary target; tracking
got worse, which is what the roster measured. **Retrain-on-top is
WITHDRAWN as a recommendation** (remains one option). The redesign target
is the limit cycle itself: the yaw channel fails to converge on the
facing target at any cadence tried. Candidate probes: wSmoothnessYaw
layered on the cadence fix (both live for the first time), a fractional
interpolated shift (warm start tracks sim time at 50 Hz), damping
(wYawRate), or selection (elite average never lets the incumbent
settle).

**Rulings (user, 2026-08-06, on the film read):**

1. **The limit cycle is policy-free and archetype-reproducible** — the
   archetypes drive the same Navigator/Mpc stack and the same settings
   asset, and oscillate identically on film. The redesign iterates on
   scripted archetype sessions (minutes per arm, controller probe as the
   read, no RL confound); the HELD velrebase open-loop apparatus is the
   purpose-built instrument for this.
2. **Slow-loop-plus-damping is OFF the table.** 10 Hz coherent intent is
   a diagnostic condition, not a design point — a slowed, damped loop
   buys sluggishness. wYawRate damping and accept-the-cadence variants
   drop to last resort. The live probe family keeps 50 Hz decisions and
   makes them converge: fractional/interpolated shift, and the selection
   question (a 50 Hz re-blurred elite average never lets an incumbent
   settle — the convergence suspect at any cadence).
3. **Converged means both at once**: facing error small AND reversal
   rate ≤ hull rate at full 50 Hz responsiveness — readable on one short
   archetype session; closeout and the moving-archetype bars stay the
   outcome gates.

**Probe 1 — fractional interpolated shift (RAN 2026-08-06): clock
hypothesis DEAD.** ZOH-faithful blend (α=dt/rolloutDt; plan clock true at
50 Hz), MPC gate 66/66, Dummy+mirror at the standard protocol
(`results/rl-eval/shift-cadence-20260806/fractional/` + NOTES table):
6.25–6.31 strict flips/s, facing 35.2–35.3°, yaw ~83 deg/s, Dummy
13W/0L/2D, mirror 2W/2L/11D — the 10 Hz arm's limit cycle reproduced
almost exactly at full 50 Hz coherence. Stock's tight tracking was the
5×-fast bug acting as accidental high-frequency dither, not a working
controller. The cycle is clock- and cadence-invariant. **Prime suspect by
elimination: selection — the elite average re-blurs the plan every solve;
nothing can settle. Probe 2 (incumbent settling) is the successor; the
open-loop probe-allowlist slice unlocks the policy-free archetype loop
for it.**

**Paired d3.0 obstacle arms (RAN 2026-08-06): obstacle competence
SURVIVED the fix.** Evader (the only threat-heavy cell, ~5% threat steps)
holds 11W/1L/3D vs stock 13W/0L/2D; the avoidance reflex fires (threat
yaw 128–177 deg/s vs ~95–118 clear, same shape as stock); the calm holds
under density (torque rev/s ~5.6–5.9 vs stock ~11). The mover collapse is
density-invariant (43/75 at d3.0 ≈ 43.5 at d2.0) → combat deaths from the
broken policy couple, not rocks. Aside for the redesign: stock scored
67/75 at d3.0, above its own 63.0 at d2.0 (single rep). Artifacts:
`results/rl-eval/shift-cadence-20260806/d3-{fix,stock}/`; films
`results/rl-capture/shift-cadence-20260806/`.

### Solver rig (Tier 0) — lease `mpc-rig` (user-approved 2026-08-11)

Probe 2 (incumbent settling in selection) and its successors get a
deterministic, seconds-scale instrument upstream of any Unity session:
`MpcSolverRig` closes the loop between `Mpc.Plan` and the solver's own
`Model` plant at the production 50 Hz solve cadence — no ship, scene,
physics, obstacles, or policy — versus a stationary Dummy anchor
(user-scoped: Dummy-only first pass). Metrics come from the bench's own
`ControllerSampler` (strict + deadband torque/nose reversals, |yaw rate|)
plus anchor facing error, so rig numbers read on the same scale as the
Bench-1 / shift-cadence artifacts; per-tick CSV traces land under
`results/mpc-rig/` for offline spectra (the investigation owns deleting
its artifacts, #303 convention).

Explicitly NOT a parity sim: no collisions, projectiles, or
match-Unity ambition — rig findings are hypotheses, and the confirmation
tiers stay what ruling 1 set: the scripted-archetype session loop, then
the closeout/roster outcome gates. The plant *is* the solver's prediction
model, so a rig result isolates controller-internal dynamics (selection,
noise, warm-start) by construction; plant-vs-Rigidbody mismatch stays a
separately measurable quantity, not a rig concern. Characterization pin:
the rig reproduces the on-target yaw churn signature versus a Dummy at
stock settings (`MpcSolverRigTests.Run_VersusDummy_ReproducesYawChurnSignature`);
a redesign that legitimately calms the loop updates the pin.

### Probe 2 — incumbent settling in selection (RAN 2026-08-11, on the rig)

Minimal apparatus (#388, lease `mpc-probe2`): `MpcSelectionMode` enum on
`MpcSettings` (default `EliteAverage`, zero behavior change, pin untouched)
switching the `SolverBuffers` emit site — `Argmin` emits the single cheapest
candidate; `IncumbentElite` averages only elite candidates strictly beating
candidate 0 and emits the incumbent verbatim when none do — plus rig-side
selection instrumentation (incumbent cost rank, argmin-win fraction,
emit-vs-incumbent yaw delta) read from the already-exposed solver buffers.
Artifacts: `results/mpc-rig/probe2/` (3 modes × start facing error {0°, 90°}
× 3 seeds, 20 s each).

**Phase 0 (stock EliteAverage characterized):** the incumbent wins argmin on
**94–96% of solves** (mean rank 0.1) yet the emitted yaw is dragged a mean
|0.113–0.119| torque off it every solve — the settle-blocker at the operating
point is the *blend*, not incumbent quality.

**Settling is achievable — but only at the exact fixed point.** The on-target
start (facing anchor, zero velocity: the intent's optimum, where the
zero-control plan is also shift-invariant) is held *inertly* by both Argmin
and IncumbentElite: 0.00 flips/s, 0.0° facing error, 100% incumbent wins,
for 22 s. Stock churns even there (self-perturbing, ~16/s).

**But selection alone does NOT converge the loop — the verdict.** From a 90°
start, every mode converges facing fast (≤10° at 1.0/1.8/1.4 s for
stock/Argmin/IncumbentElite) and then churns indefinitely at the same rate:
strict torque reversals 14.3–16.6/s across ALL modes (late-window 15–17/s;
ruling-3 bar is the hull's 4–5/s). Argmin is *worse* in state space (|yaw
rate| 17 vs 10 deg/s, facing p90 to 13.7°) — full-amplitude noise adoption
without the average's variance reduction. Incumbent win rates collapse once
moving: 51–54% (Argmin), 66–68% (IncumbentElite).

**Mechanism synthesis (rig evidence, no fix chosen):** away from the fixed
point the shifted warm start is never a valid continuation — the production
shift consumes plan time 5× sim time (mechanism #2), so only *constant*
plans survive shifting uncorrupted, and the settled zero-control plan is the
only relevant one. A corrupted incumbent loses to noisy challengers about
half the time, and every adoption injects fresh tail noise: the emitted
command churns at the noise rate regardless of selection rule. Probe 1
tested a true plan clock × blur selection (cycle persisted); Probe 2 tested
settle selection × the 5× clock (churn persists). **The untested cell is the
interaction: settle-capable selection × sim-true (fractional) shift** — the
first configuration in which the incumbent both stays valid and is allowed
to win. Fractional-shift code exists on the parked `task/mpc-shift-cadence`
branch (agent-1); running the 2×2 on the rig needs a scope ruling (Probe-2
scope was pinned to the stock shift).
