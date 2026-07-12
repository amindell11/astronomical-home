# Chase Nav — Track A: Solver Line (hygiene → obstacle redesign → gap primitives)

**Parent:** `Chase_Navigation_Trade_Study.md` (read it first — diagnosis, research, and
rejected alternatives live there; this file is execution only).
**Sibling:** `Chase_Nav_Track_B_Field_And_Eval.md` (eval harness, deterministic sensing,
terminal cost-to-go field). Track B's **eval harness lands first**; every Track A PR
reports before/after benchmark numbers from it.
**Execution:** default agent-worktree PR loop, one slot per PR, sequential within this
track (each PR is the next one's baseline). May run concurrently with Track B PRs
subject to the interface contracts (§Contracts).

**Owns:** `AI/Navigation/MPC/` (`Mpc.cs`, `BurstSolver.cs`, `Cost.cs`, `Types.cs`,
`Model.cs`, `MpcSettings.cs` + asset), gap-proposal code (new), `Navigator.cs` plumbing
for it.
**Does not touch:** `Scout.cs`/`ObstacleScanner.cs` internals (Track B), eval harness
internals (Track B), NavField/terminal-cost module (Track B).

---

## Grill decisions (2026-07-05) — read before executing

Resolved with the user; these override the raw item text below where they conflict.

1. **Benchmark sequencing.** B1 (eval harness) does not exist yet and the user is
   building it separately, *first*. Track A **code** is built now regardless (only
   *merging* is gated on numbers). No Track A PR merges until B1 numbers are attached
   or the user gives an explicit go. A1/A2/A3 report benchmark rows once B1 lands.
2. **PR packaging.** Three **stacked** PRs in one `agent-N` worktree: A1 base `main`,
   A2 base A1's branch, A3 base A2's branch. Push A1 -> user reviews -> stack A2 ->
   review -> stack A3. Distinct diffs; sequential review.
3. **A1 correlated noise = knot-based, all channels.** ~4-5 interpolated knots over the
   17 steps, single `noiseKnots` field, applied uniformly to thrust/strafe/yaw. (Not OU.)
4. **A1 deletions are behavior-neutral at current asset values** (verified in
   `MpcSettings.asset`): `adaptiveDtScale 0`, `controlSmoothing 0`, `relaxMin 0 /
   relaxMax 0` (relax guard `relaxMax > relaxMin` already false). So A1's **only** real
   behavior change is the correlated noise.
5. **`relax*` - delete now** (fields + `Mpc.Plan` block + asset entries). Behavior-neutral
   per (4). This also removes the "exempt seeded primitives from relax" complication A3
   would otherwise carry.
6. **A1 item 3 (strafe variance floor) - DEFERRED, now FOLDED INTO A3.** The solver is
   **single-shot elite-averaging with a fixed global `noiseStd`**, not iterative CEM -
   there is no adaptive per-channel covariance to collapse, so a "sigma floor" has nothing
   to floor. A1 did correlated noise only. Trade weighed at A2 start (2026-07-05):
   **A2 stays obstacle-cost-only** (clean benchmark attribution); the single-shot ->
   **iterative CEM with adaptive, floored per-channel covariance** switch + the strafe
   sigma floor **move into A3**, where the gap-primitive injection already touches the
   sampler. Budget-neutral target (e.g. 512x1 -> 4x128). Check A2 numbers first: A1's
   knot noise already ~halved chatter, so confirm strafe under-exploration still bites
   before committing the rearchitect.
7. **A2 admissibility term = stopping-distance ratio** (committed). Continuous in
   position *and* speed, 0 when brakeable: cost smooth in `(stoppingDist - clearance)`,
   `stoppingDist = closingSpeed^2 / (2*maxDecel)`, `maxDecel` from ship `Dynamics`,
   `clearance = dist - hullRadius`. (Not geometric TTC - discontinuous at closingSpeed->0.)
8. **`maxBankAngle` stays 35 deg** through A1/A2. Only raise in A3 if gap-threading
   underperforms *because* geometry rarely permits it - documented in the A3 PR body.
   (Benchmark ship = DefaultSettings: maxBankAngle 35, shipRadius 1.4.)

## A2 outcome + A3 decisions (2026-07-05, after A1/A2 built on agent-2, unmerged)

- **A2 built & refined** (PR #74). Ablation proved the residual chase timidity is the
  **hard-collision term + Gaussian sampler failing to thread dense fields** (with
  `wObstacle=0` the pursuer was equally timid) - NOT the admissibility term. Admissibility
  was refined to **collision-course-gated turn-away** (only obstacles the velocity leads
  into, sidestep-feasibility not full-stop). A2 is correct but a dense-path pursuit
  regression vs A1 until A3 restores gap-threading. This is the proof that A3's sampler
  work is load-bearing.
- **Do NOT merge A1/A2**; push straight through A3 stacked on A2 in agent-2. Merge
  A1+A2+A3 together later (after B2/B3 land + retarget bases to main).
- **A3 = ONE combined PR** with two build steps (checkpoint between): **Part 1** iterative
  CEM (budget-neutral, per-iter = samples/cemIterations, ~4x128) + per-channel sigma with
  **strafe sigma floor** (the folded-in A1 item 3); **Part 2** analytic gap detector
  (egocircle/disparity over ObstacleScan, do not touch Scout) + hysteresis + seeded
  primitive injection (align->max-strafe->hold->unwind) into the CEM sample set +
  `gapsThreaded` telemetry to the B1 benchmark + gap gizmos.
- Defaults (change only if benchmark says so): `cemIterations=4`, `strafeSigmaFloor~=0.3`,
  top-k=3 gaps, 3-5 primitives/gap, hysteresis via frame-to-frame gap association +
  switch margin, sigma reset per solve (shifted mu is the cross-frame memory).

## FINAL OUTCOME (2026-07-06): A1+A2 shipped, A3 DROPPED/shelved

Track A landed as **A1 + A2 only**. A3 (gap layer) was built, unit-proven, and then
**dropped** by user decision after the benchmark showed it inert. Full A3 stack archived
(recoverable) on branch `task/chase-nav-a3-shelved` (commits c23f4ed1..3cbc4e80).

- **A1 (PR #67)** — knot-correlated noise + dead-feature cleanup. Clean win: control
  chatter ~halved vs main; all deletions behavior-neutral. KEEP.
- **A2 (PR #74)** — obstacle cost redesign: hard collision (bank narrows the hull) +
  collision-course-gated turn-away admissibility; threshold potential deleted; shipRadius
  unbaked. 0 collisions where measurable. Correct + genuine improvement. KEEP. (Trade-off:
  turning avoidance ON re-introduced dense-path timidity vs main's neutered avoidance.)
- **A3 (shelved)** — iterative CEM (regressed → salvaged with iCEM mean-momentum + cem=2)
  + analytic gap detector + hysteresis + seeded bank-primitive injection + cost-weighted
  softmax elite mean. All unit-correct. **But inert on the B1 benchmark:**
  `gapsThreaded=0` in every field/config, injection ON≡OFF. Two root causes, both saying
  A3 isn't the lever this benchmark exercises: (1) the benchmark fields present **open**
  (wide) gaps — bank-only gaps essentially never occur at these densities/radii, so the
  detector only ever synthesizes "steer straight" ≈ what CEM already does (raising maxBank
  to 50 didn't help); (2) the chase failure is **chase/evade DYNAMICS, not navigation** —
  `minSep rises` (evader matches/outruns pursuer symmetrically), which no gap-knifing can
  close. Softmax did mechanically fix the 1/28 elite-dilution (pursuer commits forward,
  speed 6.7->10.5) but at higher chatter (15-18) and worse minSep, without enabling
  gap-threading — not a clear win, so not shipped.
- **Lessons for any A3 revival:** need (a) a benchmark field dense enough to actually
  produce sub-diameter/bank-only gaps (current B1 fields don't), and (b) the chase-dynamics
  deficit (evader speed vs pursuer) addressed separately — it's out of Track A scope and
  dominates the current intercept metric. The gap detector + injection + softmax code is
  sound and sits on `task/chase-nav-a3-shelved` if that regime is ever set up.

---

## PR A1 — Sampler hygiene + dead-feature cleanup

Scope (all solver-internal, no new behavior systems):
1. **Delete `adaptiveDtScale`/`adaptiveDtRefDistance`** and the dt-scaling block in
   `Mpc.RefreshConfig` (`Mpc.cs:212-237`), plus the now-dead `ResampleWarmStart` path if
   nothing else triggers dt changes. Remove fields from `MpcSettings` + asset.
2. **Time-correlated noise:** replace i.i.d. per-step Gaussian perturbations in
   `BurstSolver` sampling with temporally correlated noise (Ornstein–Uhlenbeck or
   spline/knot-based — pick the cheaper in Burst; knot-based at ~4-5 knots over 17
   steps is the simple option). Goal: a single draw can express "hold hard strafe for
   0.5 s". Expose correlation/knot count in `MpcSettings`.
3. **Strafe variance floor:** CEM elite refit must never collapse the strafe channel's
   sigma below a configurable floor.
4. **Review `relaxMin/relaxMax` urgency scaling** (`Mpc.cs:116-124`): keep or delete
   after measuring; if kept, it must be bypassable per-solution (A3 needs seeded
   primitives exempt). Document the decision in the PR body.
5. Delete the dormant `controlSmoothing` code path (asset value already 0) or justify
   keeping it.

Tests: existing MPC EditMode/PlayMode suites green (remember `rm -rf
src/Asteroids3D/Library/BurstCache/` before runs); add an EditMode test asserting
correlated-noise draws hold sign over ≥5 consecutive steps at meaningful probability,
and one asserting the strafe sigma floor.
Acceptance: benchmark (Track B) shows no regression vs main; solver cost/solve time
within budget (profile before/after).

## PR A2 — Obstacle cost redesign (replaces threshold potential)

Scope — implements trade study §3.4; this is the load-bearing PR:
1. **Stop baking `shipRadius` into obstacle radii** (`BurstSolver.ConvertObstacles`,
   `BurstSolver.cs:358`). `ObstacleData.radius` becomes the true obstacle radius; the
   ship footprint moves into the cost evaluation.
2. **Hard collision term:** per rollout state, overlap test against
   `shipRadius * profileScale` where `profileScale = cos(|strafe|·maxBankAngleRad)`
   (the honest bank model — narrowing applies to the hull, not padding). Overlap ⇒
   large fixed penalty (near-binary; make it decisively dominate stage costs, e.g.
   ≥10× max per-step stage cost, tunable). Small constant safety margin for model
   error — **not** speed-scaled.
3. **Admissibility/speed-shaping term** replacing the graded repulsion: continuous
   time-to-collision / stopping-distance cost along current velocity (penalize states
   from which braking/turning away is no longer possible). No range boundary, no
   speed-inflated threshold, no top-8 ranked buffer churn (evaluate against all scanned
   obstacles or a per-rollout spatial cull — measure; 128 obstacles × 17 steps × 512
   samples may need the cull).
4. **Delete** `obstacleThreshold`, `obstacleSpeedMargin`, `obstacleFalloffCurve`,
   `obstacleClosingScale/HalfSpeed`, the `RankedBuffer` harmonic sum, and their
   `MpcSettings` fields/asset entries. Keep `wObstacle` as the admissibility weight or
   rename honestly.
5. Keep `LosCost` behavior intact (it uses raw obstacle radii — verify it still gets
   them after (1)).

Tests: unit tests for the collision term (banked vs unbanked clearance on a
just-too-narrow gap: unbanked rollout collides, banked at max strafe clears), TTC term
monotonicity, and existing suites green.
Acceptance on Track B benchmark: collision count strictly down vs A1 **and** mean chase
speed not down more than marginally; no boundary thrashing (control chatter metric or
manual observation in Testbench). This PR re-enables obstacle avoidance that the user
deliberately neutered — expect tuning iteration; keep weights in the asset.

## PR A3 — Analytic gap layer + seeded primitive rollouts

Scope — implements trade study §3.2/§3.3 coupling:
1. **Gap detector** (new file, plain C# over the existing `ObstacleScan` — do not
   modify Scout): closed-form egocircle/disparity pass over scanned circles → angular
   free intervals inflated by `shipRadius` (and `shipRadius·cos(maxBank)` for
   bank-only gaps) → top-k (k≈3) gaps scored by width, depth, alignment to chase goal,
   and admissibility (reachable given current velocity/turn radius).
2. **Hysteresis at the gap level:** frame-to-frame gap association; switch chosen gap
   only when a competitor beats it by a margin. This is where oscillation is killed —
   not inside the solver.
3. **Primitive injection (Biased-MPPI pattern, CEM flavor):** per solve, synthesize
   3–10 scripted control sequences per top gap — align yaw to gap axis → ramp to max
   strafe (bank) → hold through traversal → unwind — and inject them into the CEM
   sample set alongside Gaussian samples. No importance correction needed (CEM refits
   elites). Exempt injected solutions from `relax*` attenuation if A1 kept it.
   Keep the last gap-threading elite as an extra warm-start seed.
4. Debug/vis: gizmo for detected gaps + chosen gap + injected-primitive best cost
   (follow the existing MPC editor/debug pattern in `AI/Navigation/MPC/Editor/`).

Explicit non-goals: no subgoal replacement (only fall back to it if injection
underperforms — record in PR body), no ensemble-of-solvers, no changes to obstacle
sensing.

Tests: gap detector unit tests (two discs → one gap with correct axis/width; occlusion;
bank-only gap classification), injection test (a contrived narrow-gap scenario where
Gaussian-only sampling fails to thread but injected primitive wins the elite), suites
green.
Acceptance on benchmark: gaps-threaded count up, time-to-intercept down vs A2, in
`BigFieldSettings` chase scenario.

---

## Contracts (agreed with Track B — do not break unilaterally)

- **`ObstacleScan` / `DetectedObstacle` shape is frozen** (position, radius, collider).
  Track B may change how it's *filled* (deterministic field query); Track A consumes it
  as-is. If A2's per-rollout cull needs more than (position, radius), negotiate first.
- **`Cost.Evaluate` seam:** Track B will add exactly one terminal-cost hook (a sampled
  cost-to-go lookup at rollout end) in its own PR. Track A must not restructure the
  rollout-cost accumulation loop signature in `BurstSolver.Solve` without flagging
  Track B. Whoever lands second rebases.
- **`MpcSettings` churn:** A1/A2 delete fields; B3 adds `wTerminal`. Never edit the
  .asset in two in-flight PRs at once — serialize asset changes through PR order.
- Benchmark numbers come from Track B's eval harness (B1). A1 may not merge before B1
  exists; if B1 is delayed, A1 lands with manual Testbench evidence and backfills
  numbers.
