# Chase Nav — Track B: Eval Harness, Deterministic Sensing, Terminal Field

**Parent:** `Chase_Navigation_Trade_Study.md` (diagnosis/research there; execution
only here).
**Sibling:** `Chase_Nav_Track_A_Solver.md` (sampler hygiene, obstacle cost redesign,
gap primitives). Track B is the independent line: nothing here depends on Track A
landing, but **B1 (eval harness) gates everything** — both tracks report benchmark
numbers from it in every PR.
**Execution:** default agent-worktree PR loop, stacked in one slot. Track A is owned by
a **separate agent**; this agent implements **Track B only**.

**Owns:** eval/benchmark code (new), `AI/Scanning/` (`Scout.cs`,
`ObstacleScanner.cs`), the resurrected NavField module (new home, e.g.
`AI/Navigation/Field/`), the single terminal-cost hook into the solver.
**Does not touch:** `Cost.cs` stage costs, sampling/noise code, `MpcSettings` tuning
weights (except adding `wTerminal` in B3).

### Decisions locked 2026-07-05 (grill session)
- **Ownership/flow (supersedes the parallel-slots framing):** one agent, Track B only.
  Order: **B1 → PR → approve → merge to main** (B1 gates both tracks, so it lands before
  anything continues) → **B2 → PR → approve → merge** → **wait for Track A (A1/A2/A3) to
  land on main** → **rebase onto main → B3 → PR → approve → merge**. B3 last means its
  single terminal hook drops into the *final* redesigned solver — no cross-stack conflict,
  no "whoever lands second rebases" dance.
- **B1 form:** a **two-AI Testbench sector** — pursuer in `MaintainRange` (tight/zero
  band, enemy-anchored), evader in `Flee` — watchable in-editor, **plus** a headless
  PlayMode wrapper (`unity_test_agent.ps1` `ChaseBenchmark` category) that loads the same
  scenario, sweeps N seeds unattended, and writes JSONL. Same scenario, two entry points.
  This also puts `Flee` mode under the benchmark (partial answer to trade-study §7.4).
- **Metric model (shared-MPC confound fix):** both ships run the MPC-under-test, so
  headline metrics are **per-ship** (collisions, impact energy, mean sustained speed,
  control-chatter mean |Δu|/axis/s), aggregated over N seeds as **mean ± spread**; a phase
  "wins" when its per-ship distribution beats baseline. Time-to-intercept / mean-distance-
  behind are kept as **secondary context** (confounded because the evader changes too).
- **Determinism:** sampler RNG is `Time.frameCount`-seeded (`BurstSolver.cs:183`), so runs
  are not bit-reproducible; the benchmark is **statistical** (N-seed mean±spread), not
  replay-based. Add a **small additive test-only seed-override** hook (defaults to the
  current frameCount behavior when unset — zero change to shipped play) so single debug
  runs are reproducible on top of the aggregate.

---

## PR B1 — Chase eval harness (lands before everything)

Scope — trade study §6; this is the measuring stick for both tracks. **Form finalized in
the 2026-07-05 grill (see "Decisions locked" above): two-AI Testbench sector + headless
wrapper, per-ship multi-seed metrics.**
1. **Scenario:** a **two-AI Testbench sector** in `BigFieldSettings` — a pursuer ship in
   `MaintainRange` (tight/zero band, enemy-anchored) chasing an evader ship in `Flee`,
   both weaving through the dense field. No scripted/recorded player path (the evader *is*
   the quarry). Openable in the editor to watch; the same scenario is driven headless by a
   PlayMode benchmark test. N seeds = field seed × start-offset (start ~5×2; configurable)
   to avoid overfitting one layout.
2. **Metrics per run, written to JSONL** (follow the `ai-utility-analysis` logging
   pattern where it fits). **Headline = per-ship, robust to the shared-MPC confound:**
   collision count + total impact impulse, mean sustained speed, control-chatter
   (mean |Δu| per axis per second — the thrash detector), solver time per tick.
   **Secondary context (confounded — evader changes too):** time-to-intercept or
   mean/final distance-behind. Gaps-threaded vs detoured stubbed until A3's detector
   exists. Aggregate across seeds as **mean ± spread**.
3. **Runner:** invocable headless via `scripts/unity_test_agent.ps1` category
   `ChaseBenchmark` (loads the sector, sweeps seeds, writes JSONL), plus a small
   script/doc for diffing two JSONL result sets (baseline vs candidate: per-metric
   mean ± spread over N seeds). A phase "wins" when its per-ship distribution beats
   baseline.
4. Record baseline numbers for current main and commit them (or a doc table) so every
   subsequent PR has a reference row.
5. **Seed-override seam:** small additive test-only hook to pin the sampler base seed
   (`BurstSolver` currently seeds off `Time.frameCount`); unset ⇒ current behavior, so
   shipped play is unchanged. Enables reproducible single debug runs.

Non-goals: minimal game-code changes — prefer existing sector-module/prefab seams; the
only shipped-code touch is the additive seed-override hook. The two AIs use existing
goal modes (`MaintainRange`, `Flee`) — no new controllers.
Acceptance: N-seed per-ship metric **distributions are stable** run-to-run (statistical
determinism — not bit-identical replay, which the frameCount-seeded sampler precludes);
baseline table committed.

## PR B2 — Deterministic-field obstacle sensing

Scope — trade study §3.4 sensing half; a pure upstream swap:
1. Replace the speed-adaptive `Physics.OverlapSphereNonAlloc` asteroid scan in
   `ObstacleScanner`/`Scout` with direct queries of the deterministic asteroid field
   (chunk/registry lookup within a **fixed-size AABB** around the ship — size from
   worst-case stopping distance at `maxSpeed`, a constant, not per-frame speed).
   Ships keep their current registry/scan path (they move; physics or ship-registry
   query, whichever is already cheapest).
2. **Output contract unchanged:** same `ObstacleScan`/`DetectedObstacle` shape, same
   consumer API — Navigator/solver/Track A code must not need edits. Radius fed per
   asteroid should be the authoritative in-plane radius from the field/registry data
   rather than collider bounds, if available.
3. Handle the destroyed/fractured-asteroid case: query must reflect live state
   (registry), not just the deterministic spawn layout — dead asteroids must not be
   reported. (This is the reason the query targets the registry/live chunk data, not
   the raw hash layout.)
4. Kill the speed-coupled scan radius and its churn; delete `obstacleLookaheadTime` if
   nothing else uses it.

Tests: EditMode tests comparing new query vs physics scan on a seeded field (same
obstacle set modulo the fixed-vs-speed-scaled radius difference); PlayMode suite green;
benchmark: identical-or-better metrics vs B1 baseline (this PR should be
behavior-neutral for the solver — flag any metric drift in the PR body).

## PR B3 — Terminal cost-to-go field

Scope — trade study §3.1 / Option C; the topology fix:
1. **Resurrect `NavField`** (Dijkstra core from `5f5a4530~1`) into a Burst-friendly
   form (flat NativeArrays, jobified backward sweep; 8-connected Dijkstra is fine, FMM
   optional later). New home e.g. `AI/Navigation/Field/`. Grid ~cell size between
   ship-length and half typical gap; extent covering the loaded field radius around
   the anchor.
2. **Field service:** one field per chase *target* (player), shared by all pursuers —
   mirror the old `AsteroidNavField` caching/rebuild policy but: rebuild off the
   registry/deterministic data (B2's query path), re-solve when target moves > ~1 cell
   or on registry delta, amortized/jobified off the main thread. No `FindObjectsByType`
   (cache ships via registry — GetComponent-in-Awake rule).
3. **Store time-to-go** (cost scaled by nominal chase speed), bilinear-interpolated
   sampling.
4. **Single solver hook (the one Cost/BurstSolver touch):** rollout cost +=
   `wTerminal * field.SampleTimeToGo(x_terminal)`, `wTerminal ≈ 1` in stage-cost
   units, new `MpcSettings.wTerminal` (asset entry added here). Blocked/off-grid
   terminal states sample a large-but-finite fallback (distance-to-goal), never
   infinity (would destroy elite ranking). NaN/no-solution ⇒ hook contributes 0
   (pure fallback to current behavior).
5. **No goal substitution, no waypoints, no gradient walking** — the explicit
   anti-goals that killed the old design. Evade/flee integration is out of scope
   (open question §7.4 in the trade study).
6. Debug/vis: field gizmo (cost heatmap + blocked cells) behind the existing MPC debug
   editor pattern.

Tests: NavField unit tests (resurrect + adapt the old `NavFieldEditModeTests` from
`5f5a4530~1`), staleness/rebuild policy tests, solver test that a goal behind a wall
of asteroids produces rollouts preferring the around-route (terminal cost visibly
reorders elites), suites green.
Acceptance on benchmark: trap/stuck episodes eliminated on the cluster-weave paths;
time-to-intercept down vs B2 baseline; solver tick budget respected (terminal lookup
is one fetch per rollout — verify no field-rebuild spikes on main thread).

---

## Contracts (agreed with Track A — do not break unilaterally)

- **`ObstacleScan` / `DetectedObstacle` shape frozen**; B2 changes the producer only.
  If Track A's per-rollout cull (A2) needs new fields, negotiate first.
- **Solver files:** B3 makes exactly one narrow insertion into the rollout-cost
  accumulation (terminal hook) + one `Config`/`MpcSettings` field. All other
  `Cost.cs`/`BurstSolver.cs`/`Types.cs` changes belong to Track A. Whoever lands
  second rebases.
- **`MpcSettings.asset`:** never edited by two in-flight PRs; serialize through PR
  order (A1/A2 delete fields, B3 adds `wTerminal`).
- **B1 gates merges:** no behavior-changing PR (either track) merges without its
  before/after benchmark rows in the PR body.
