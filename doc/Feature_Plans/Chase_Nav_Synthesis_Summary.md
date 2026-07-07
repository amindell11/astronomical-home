# Chase Nav — Synthesized Feature Summary

**Branch:** `task/chase-nav-synthesis` (base: `fable/chase-combined`).
**Inputs:** three parallel implementations of `Chase_Nav_Track_A_Solver.md` +
`Chase_Nav_Track_B_Field_And_Eval.md` — Fable (`fable/chase-combined`), Opus team
(`agent-4` stack), Codex (`task/chase-nav-track-b-b1-eval-harness`). Full three-way
review: `Chase_Nav_Implementation_Comparison.md`. This branch is the synthesis decided
2026-07-06: Fable base + Opus grafts; the Codex branch was discarded in full
(maxDecel mass bug, hot-path perf hazards, main-thread Dijkstra, no gap hysteresis).

## What ships

- **A1 — knot-correlated sampler noise** (Fable): Gaussian noise drawn at `noiseKnots`
  (asset: 5) evenly spaced knots and linearly interpolated across the horizon, so one
  candidate can hold a maneuver. Dead features deleted: `adaptiveDt*`,
  `controlSmoothing`, `relax*` (all behavior-neutral at asset values).
- **A2 — obstacle cost redesign** (Fable structure, **Opus semantics**): hard
  hull-overlap collision term (bank narrows the hull: `shipRadius·cos(|strafe|·maxBank)`
  + constant margin ⇒ fixed `collisionPenalty`, applied **outside** the terminal ramp) +
  **collision-course-gated turn-away admissibility** — only obstacles the velocity leads
  into cost anything; the cost measures whether lateral thrust can still sidestep the
  deficit in the time available. Chosen over the stopping-distance ratio on the strength
  of the Opus A2 ablation (braking-based cost ⇒ chase timidity). Tuning from the Opus
  branch: `wObstacle 5`, `collisionPenalty 10000`, `collisionSafetyMargin 0.3`.
  `shipRadius` is no longer baked into obstacle radii.
- **B1 — chase eval harness** (Fable): `ChaseBenchmarkSector` prefab (watchable
  in-editor) + `ChaseBenchmarkModule`/`ChaseMetricsProbe` headless driver, field-seed ×
  field-offset sweep via `CHASE_BENCH_*` env vars, per-ship headline metrics, JSONL +
  `scripts/chase-benchmark/compare_chase_benchmark.py`. Committed baselines in
  `doc/Feature_Plans/chase-benchmark-baselines/`.
  Sampler seed seam: `SolverBuffers.SamplerSeedOverride` — the pinned mode now keeps the
  per-ship position hash in the seed (Opus's design), so two pinned ships never share a
  noise stream.
- **B2 — deterministic-field obstacle sensing** (Opus's query, interim wiring): the
  `IObstacleField` chunk-cell query implemented by `UpdatingAsteroidField`, published
  through the `ObstacleFields.Active` access point: the sector's `AsteroidFieldSpawner`
  registers on build / unregisters on teardown, and consumers (the obstacle scanner,
  the terminal nav field) pull it directly — **no per-ship wiring through
  AICommander/UnitService/services**. The scanner owns its fixed worst-case query
  envelope (computed once from dynamics). No speed churn, no physics round-trip,
  destroyed asteroids drop out immediately, radius-aware AABB culling (a rock
  protruding into the box is reported even when its center is outside).
  **The access point itself is explicitly INTERIM** — a process-wide static assumes one
  field per process, which breaks once multiple arena instances run in one process
  (planned for RL training). How world-scoped state should be organized is deliberately
  deferred until after this work; the seam is kept minimal so it's cheap to replace.
- **B3 — terminal cost-to-go field** (shared Fable/Opus core + Opus service): Burst
  Dijkstra `NavField` + `NavFieldService` (double-buffered, off-main-thread bakes, one
  field per chase target) **fed by the same B2 `IObstacleField`** — one obstacle
  producer for both the scan and the field. Solver hook: one terminal fetch per rollout,
  `wTerminal 1`.

## Scrapped: A3 (gap layer + seeded primitives) — deliberate, evidence-based

**No gap layer ships in this synthesis.** Both A3 implementations were removed/excluded
by user decision (2026-07-06), on the Opus team's benchmark evidence: injection was
**inert** — `gapsThreaded = 0` in every field/config, injection ON ≡ OFF — because
(1) the benchmark fields present wide-open gaps (bank-only gaps essentially never occur
at these densities/radii), and (2) the chase deficit is pursue/evade **dynamics**
(the evader matches the pursuer symmetrically), which no gap-knifing can close.

- Fable's A3 (arc-geometry detector + hysteresis + open-loop bank primitives + injection
  slots) was built into the base branch and has been **removed here** (`GapDetector.cs`,
  injection plumbing in `BurstSolver`/`Mpc`/`Navigator`, gap settings + gizmos + tests).
  It remains recoverable on `fable/chase-combined` / `fable/chase-a3-gap-primitives`.
- Opus's A3 (raster egocircle + closed-loop primitives + iCEM/softmax rearchitecture)
  stays archived on `task/chase-nav-a3-shelved`.
- **Revival conditions** (per `Chase_Nav_Track_A_Solver.md` FINAL OUTCOME): a benchmark
  field dense enough to actually produce sub-diameter/bank-only gaps, and the
  chase-dynamics deficit addressed first — it dominates the intercept metric.

## Known limitation: asteroid-only obstacle sensing (no physics fallback)

The obstacle scan now queries the live asteroid field **only**. When no
`UpdatingAsteroidField` is registered (e.g. a scene without an asteroid field), ships
sense **zero static obstacles** — the legacy speed-adaptive `Physics.OverlapSphere`
path was removed rather than kept as a fallback (user decision: keep the seam minimal
now). `Scout.SetObstacleExclusion` is consequently a no-op (the field query never
reports ships; ships still arrive via the merged `ShipScanner`).

**Future work:** generalize `IObstacleField` beyond asteroids — additional producers
(stations, debris, authored geometry) registering into the same seam, so non-asteroid
obstacles become visible to the MPC again without reintroducing physics scans.

## Tuning summary (MpcSettings.asset)

`noiseKnots 5` · `wObstacle 5` · `collisionPenalty 10000` · `collisionSafetyMargin 0.3`
· `wTerminal 1`. Deleted keys: `adaptiveDt*`, `controlSmoothing`, `relax*`, the
threshold-obstacle family, and the gap-primitive family.

## Validation

- Full EditMode + PlayMode suites on this branch (BurstCache cleared) — see PR body.
- Fresh 10-episode chase benchmark (5 seeds × 2 offsets, 40 s) vs the committed
  `baseline-main` sets — rows attached to the PR body. Expect the collision/chatter
  gains of the reviewed branches; chase-geometry metrics remain confounded by the
  shared-MPC design (see comparison doc §3 — pursue/evade dynamics is tracked as its
  own follow-up).
