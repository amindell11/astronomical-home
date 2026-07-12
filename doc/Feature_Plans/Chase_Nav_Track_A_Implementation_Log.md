# Chase Nav — Track A implementation log (A1 → A2 → A3)

Chronological record of the solver-line work on `agent-2`, including the decisions,
diagnostics, dead-ends, and pivots. Companion to the plan doc
`Chase_Nav_Track_A_Solver.md` (execution spec) — this file is "what actually happened".

Branch/stack (all on `agent-2`, stacked on the B1 eval-harness branch):
`A1 → A2 → A3(part1 → salvage → part2 → softmax)`. Benchmarks come from Track B's
`ChaseBenchmarkPlayModeTests` (offset-cross / wide-lateral / near-cluster).

---

## PR A1 — sampler hygiene + knot-correlated noise  (commit `9c525e81`, PR #67)

**Goal:** delete dead solver features and replace i.i.d. per-step Gaussian sampling
with time-correlated noise.

**Did:**
- Deleted (behavior-neutral at asset values 0): `adaptiveDtScale/RefDistance` + the
  dt-scaling + dead dt-resample machinery; `controlSmoothing` (EMA); `relaxMin/Max/Curve`.
- **Knot-based correlated noise**: each candidate draws `K=max(2,noiseKnots)` Gaussian
  knots per channel and linearly interpolates them across the horizon, so one draw holds
  a coherent maneuver. New `noiseKnots` (default 4). Editor gizmos re-pointed at
  `LastControl`.

**Result / finding:** the only real behavior change (correlated noise) **halved control
chatter** (~18→~8 /sec) and made pursuit more committed (speed ↑), essentially
metric-favorable. Same-machine A1-vs-B1 baseline confirmed the effect wasn't machine
variance.

---

## PR A2 — obstacle cost redesign  (commits `d2740403`, `07fb0c95`, PR #74)

**Goal:** replace the graded threshold potential (which the user had neutered) with a
hard collision term + a continuous admissibility term.

**Did (first pass):**
- **Unbaked shipRadius** from obstacle radii (`ConvertObstacles`); the ship footprint
  moved into the cost.
- **Hard collision term** (near-binary): hull `= shipRadius·cos(|strafe|·maxBank)`
  (bank narrows the *hull*, not padding) + a small `obstacleSafetyMargin`; overlap →
  large `collisionPenalty` (10000), decisively dominating stage costs.
- **Admissibility v1 = stopping-distance ratio**: continuous, 0 when brakeable.
- Deleted the threshold fields + `RankedBuffer` harmonic sum; `Config` gained
  `shipRadius`, `maxDecel`.

**Problem found:** the pursuer went **timid** — offset-cross speed 10.7→~6, no
intercept; wide-lateral/near-cluster couldn't even complete the harness lock-wait
(pursuer too slow to close for enemy acquisition).

**Key ablation (`wObstacle=0`, admissibility fully off):** offset-cross stayed timid
(~6.3) and wide-lateral still failed → the timidity is the **hard-collision term + the
Gaussian sampler failing to thread**, NOT the admissibility cost.

**Refinement (2nd pass, `07fb0c95`):** replaced stopping-distance with a
**collision-course-gated turn-away** term — only obstacles the velocity actually leads
*into* (perp < corridor) cost anything; a weaving pursuer steers around off-course rocks
for free. `Config.maxDecel → maxLatAccel`. Near-neutral on the benchmark (confirmed
again: the residual timidity is collision/sampler, i.e. **A3 territory**). Left
`wObstacle=5`, `collisionPenalty=10000`, `obstacleSafetyMargin=0.3`.

**Outcome:** collisions strictly down (0 wherever measured), speed comparable on open
paths (short-a ~9.9), down on dense paths. Acceptance only partially verifiable because
the induced timidity blocked the far-start scenarios — the sampler fix (A3) was needed.

---

## PR A3 — iterative CEM + gap threading  (one PR, built in parts)

### Part 1 — iterative CEM with per-channel adaptive sigma + strafe floor  (`c23f4ed1`)

**Did:** converted `Solve` from single-shot elite-averaging to N-iteration CEM
(budget-neutral, `M = samples/cemIterations`), per-channel `float3` sigma refit from
elites each iteration, floored (`strafeSigmaFloor 0.3`, `sigmaFloor 0.05`) so lateral
exploration never dies. Seams: `LastSigma`, `LastIterationCosts`, `FloorSigma`.

**Regression found:** at `cemIterations=4` the pursuer got fast (offset-cross 12.8) but
**chatter blew up (8→23)** and it **broke lock acquisition even in the easy short-a
scenario** (which passed under single-shot A2).

**Ablations that pinned it:**
- `cemIterations=1` (≈ single-shot) → short-a passes again, chatter ~9. So the
  **multi-iteration CEM** is the cause, not the plumbing.
- `strafeSigmaFloor=0.05` at cem=4 → still fails. So the strafe floor is **not** the
  culprit.
- Mechanism: budget split (512→4×128) gives noisier per-iteration elites, and hard
  per-frame reconvergence "refines away" the warm-start's temporal continuity → step-0
  jitter.

### Salvage — iCEM mean-momentum + cem=2  (`a4cc1ae7`)

**Did:** `cemIterations 4→2` (M=256), and **mean momentum**:
`mean_new = lerp(mean_prev, eliteAvg, meanMomentum)`, `meanMomentum 0.5`. At iter 0
`mean_prev` is the shifted warm-start, so the returned mean stays anchored to last
frame's solution → temporal continuity restored.

**Result:** short-a passes again (chatter ~9), the long benchmark completes all three
scenarios once (0/0/0 pursuer collisions vs A1's 1/4/2), chatter ~12–13, solve ~0.55ms.
Dense scenarios no longer regress vs A2. **Kept iterative CEM as the foundation.** (Note:
wide-lateral lock was marginal — ~1/4 pass — the pursuer closed more purposefully but
reliable dense closing still needed the gap layer.)

### Part 2 — gap detector + hysteresis + seeded primitive injection  (`f88c00d7`)

**Did:**
- **`GapDetector`** (plain C# over `ObstacleScan`): egocircle/disparity — blocks each
  obstacle's true angular silhouette on a 180-bin egocircle, reads free runs as gaps,
  measures linear width / depth / mouth distance, classifies open / bank-only /
  impassable, scores by goal-alignment + width + depth, returns top-3. Unit-tested
  (two-disc axis+width, occlusion doesn't split, bank-only class, empty).
- **`GapSelector`** — frame-to-frame hysteresis (keep the chosen gap unless a competitor
  beats it by `gapHysteresisMargin`).
- **`GapPrimitives`** — forward-simulated seeds: yaw-PD to the gap axis + a *tight bank
  pulse timed (closed-loop) to the mouth crossing*. (Banking is coupled to lateral strafe
  translation, so a wide bank drifts the hull into a wall — a short pulse narrows the hull
  just at the mouth.) Unit-tested (primitive threads where a straight rollout collides).
- **Injection** into the CEM candidate set (iteration 0, over slots 1..count).
- `Navigator.Gaps` wiring + `GapsThreaded` telemetry; gizmos; harness one-liner
  (`gapsThreaded = pursuerCmdr.Navigator.GapsThreaded`).

**Problem found:** injection was **INERT** in the closed loop — offset-cross ON vs OFF
identical, `gapsThreaded=0`, wide-lateral still failed. Root cause: the applied control
is the **uniform elite MEAN**, which dilutes a lone low-cost primitive to ~1/eliteCount
(≈1/28) weight → the pursuer never commits through a gap. Unit test peak-strafe was ~0.04.
Stopped for review rather than changing the refit unilaterally.

### Softmax — cost-weighted elite mean  (`3cbc4e80`)

**Did (user's call):** replaced the uniform elite average with a **softmax** one:
`w_i = exp(-(cost_i-minCost)/tau)`. Adaptive temperature — after a false start with the
spread-only `tau = eliteTemperature·(meanCost-minCost)` (which **over-sharpened open
fields and broke straight-line waypoint tracking**, `Plan_HeadsTowardGoal` veered), used
`tau = eliteTemperature·max(|meanCost|, meanCost-minCost)` so sharpness depends on
*relative* cost differences: open fields stay soft/momentum-smooth, a dramatically
cheaper winner dominates. New `eliteTemperature` (0.2).

**Result — acceptance NOT met:**
- Softmax mode-commitment **un-timids** the pursuer (offset-cross 6.7→~10.5) WITHOUT the
  cem=4 chatter blowup.
- But **injection is still inert** (ON vs OFF identical; `gapsThreaded=0` every run) —
  the speedup is softmax alone. Bank-only gaps essentially never occur at these field
  densities/radii (open gaps dominate → primitives are just "steer straight" ≈ what CEM
  already does).
- **No interception** (minSep *rises* to 13–16 as both ships speed up symmetrically);
  **wide-lateral lock 0/5**; chatter up to ~15–18 (over the ≤15 target).
- **Sanctioned knobs exhausted:** `maxBank 50` didn't raise gaps and worsened lock;
  `meanMomentum 0.7` re-timids (mode-commitment and chatter are coupled — no sweet spot).
  Left at `eliteTemperature 0.2 / meanMomentum 0.5 / maxBank 35`.

**Read:** the dilution fix works *mechanically* (pursuer commits), but the gap-threading
*goal* isn't achieved — the chase deficit here is evade/pursue dynamics (symmetric
speed-up), not tight-gap threading. STOPPED for a keep-softmax-vs-revert-to-salvage
decision; not pushed.

---

## Cross-cutting method notes

- **Ablation-first debugging paid off repeatedly:** `wObstacle=0` (A2), `cemIterations=1`
  and `strafeSigmaFloor=0.05` (A3-P1), injection ON/OFF (A3-P2 & softmax). Each cleanly
  attributed a regression to its true cause instead of guessing.
- **Benchmark is stochastic and the harness lock-wait is brittle** to a slow pursuer;
  always report pass-*rates* over several runs, not one lucky row. Clear
  `Library/BurstCache/` before every test run.
- **This game couples banking to lateral strafe translation**, which makes the
  "bank-only" gap regime razor-thin — the recurring reason bank-threading is hard to both
  synthesize and unit-test cleanly.

## Current state (as of this log)
- Commits `9c525e81`(A1) → `07fb0c95`(A2) → `c23f4ed1`/`a4cc1ae7`/`f88c00d7`/`3cbc4e80`(A3)
  on `agent-2`. A1 (#67) and A2 (#74) are open PRs; A3 is committed but **not pushed**
  (acceptance not met, awaiting decision on softmax-vs-salvage).
- All unit/integration suites green: EditMode MPC 35/35, PlayMode MPC 7/7,
  AIIntegration 10 pass/1 skip/0 fail.
