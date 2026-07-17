---
node_type: feature_plan
status: ready-to-build (grilled 2026-07-08; PR-0 re-pivot is the prerequisite, PR-1 step-0 is the go/no-go gate)
created: 2026-07-08
gated_on: "#78 merged (10303015) — gate now clear"
depends_on: "Asteroid_Mesh_Repivot_And_Radius_Bake.md (PR-0 prerequisite — lobe centers must orbit the true COM)"
related:
  - Chase_Navigation_Trade_Study.md
  - Deterministic_Asteroid_Field.md
  - Asteroid_Mesh_Repivot_And_Radius_Bake.md
---

# Multi-Circle Asteroid Obstacles

> STATUS: live arc — asteroid env + obstacle tokens is the tactical-AI next step

Tighter obstacle geometry for the MPC solver: represent elongated asteroids as
**2–4 baked covering circles** instead of a single mean-radius circle, so
stretched rocks stop contributing phantom berth across their thin axes. Reuses
the solver's existing circle inner loop — no new Burst distance function.

## Motivation

Today every asteroid is one circle at `MeanVertexRadius` (baked per shared mesh,
PR #78 / `10303015`). Mean-vertex is honest for blobby rocks but a single circle
around an elongated rock is as wide as the rock is *long* — permanent phantom
volume across its thin axes. That phantom berth was measured to be a large share
of the AI's leftover chase timidity (the #78 benchmark: dropping circumscribed →
mean radius moved pursuer chase distance 21.6 → 16.0). Multi-circle removes the
rest of it for stretched rocks without paying polygon cost.

## Why multi-circle-snapshot, NOT true convex polygons (for asteroids)

This is a **physics** argument, not a cost argument. True convex polygons were
considered and deliberately reserved for stable-orientation *environment*
objects (stations, wrecks, authored structures) — not asteroids.

- **Asteroids tumble in full 3D.** `AsteroidFieldLayout.cs:363,370` gives each
  rock a uniform-random 3D orientation (`rng.RotationUniform()`) plus three
  independent spin components. Only *position* is plane-locked; rotation tumbles
  freely.
- **A fixed 2D hull spun about the plane normal is therefore wrong** — the plane
  silhouette of a tumbling body doesn't *rotate*, it *morphs* (a long rock
  end-on projects short, broadside projects long). A single baked 2D hull is a
  snapshot of one orientation and under-covers where the lobe swings to.
- **The rotation-robust envelope of a freely tumbling body is round.** For fast
  tumblers the circle is close to *being* the truth, not a cheap approximation
  of it — so polygons buy nothing there.
- **The solver already freezes each asteroid as a static circle for the whole
  horizon** (no obstacle velocity integration — only enemy ships get a predicted
  trajectory). So a per-solve shape *snapshot* decays over the ~1.7 s horizon at
  the same order the already-frozen *position* does — it is a consistent
  approximation, not a new sin. This is what makes the "big enough and spinning
  slow" instinct correct: the only question is how much the silhouette moves over
  the horizon relative to clearance.

Cost is not the blocker: rotating each obstacle's hull can be amortized once per
solve across all ~512 candidate rollouts, so even K-nearest polygons would be
affordable. Tumbling-validity is the blocker for asteroids specifically.

## PR-0 prerequisite — mesh re-pivot (`Asteroid_Mesh_Repivot_And_Radius_Bake.md`)
Multi-sphere **requires** the re-pivot first. Lobe centers are offsets from the
mesh pivot and are projected through the asteroid's rotation; the math assumes
**pivot == rotation center (COM)**. Today Asteroid2/3/4 pivots are 12–20% off
their volume centroid, so lobes on those rocks would orbit a wrong point and the
snapshot would decay *faster* — the exact problem circles were chosen to avoid.
The re-pivot moves each pivot to the signed-tetrahedron volume centroid and
bakes `cachedMeanRadius` into `MeshInfo` via `OnValidate` (deleting the runtime
`MeanRadiusCache`). Multi-sphere builds on that. (Re-pivot is a destructive
one-way vertex rewrite of embedded sub-asset meshes → its own scoping/backup
pass with the user.)

## Step 0 — measurement gate (do first, go/no-go + calibration)

Before writing solver/baking code, **extend the existing
`AsteroidCentroidOffsetReport` tool** (menu `Tools/Asteroids/Log Centroid
Offsets`; headless `-executeMethod`) to also report, per shipped-settings mesh,
the **three area-weighted PCA principal-axis extents** (rods vs plates vs blobs,
not a single ratio) plus the live spin distribution (`spinRange` currently
`(-30,30)` deg/s per axis → ~50° swing over a 1.7 s horizon, *above* the 20–30°
spin gate — a yellow flag to confront). Reuse, don't write fresh.

- **Go** if a meaningful share of spawned asteroid *volume* is elongated
  (aspect > ~1.3) AND slow enough to clear the spin gate.
- **No-go / refocus** on stable-orientation environment objects if it's mostly
  blobby/fast — this is the A3-inert-build failure mode and this pass exists to
  avoid repeating it.
- The histogram also *calibrates* the classifier thresholds below (derive them
  from real data, don't guess). User's prior: expects it to pass.

**RESULT (PR #85, 2026-07-08) — GO.** Tool shipped (`AsteroidCentroidOffsetReport`
extended with area-weighted PCA + spin readout). Against shipped
`SpawnSettings.asset`:
- **6 / 10 meshes are ROD** (Asteroid 3, 4, 6, 7, 8, 10); 4 BLOB; **0 PLATE**
  (elongation always trips e1/e2 before e2/e3). `e1/e2` min 1.04, median ~1.39,
  max **1.79** (Asteroid4). Strongest: A4 1.79, A10 1.76, A6 1.52, A8 1.52;
  marginal (near threshold): A3 1.39, A7 1.38.
- **Spin is a NON-issue** — shipped `spinRange=(-3,3)` deg/s → ~5.1° swing over
  the horizon, far below the ~14.7 deg/s gate → **0%** gated out. The earlier
  yellow flag was the *code default* `(-30,30)`; the asset overrides it
  ([[feedback_check_so_assets]]). Spin gate stays in for correctness but never
  fires on today's field. (Caveat: base spin is mass-scaled; base is so low that
  even large mass factors stay well under the gate.)
- **Calibration takeaways for PR-2:** (a) elongation is *mild* (max 1.79) → the
  sphere schedule effectively collapses to **K∈{1,2}**; K=3 (aspect>2.5) never
  fires on this set — build for 2 lobes, keep 3 as dormant headroom. (b) A3/A7
  sit right at 1.3–1.4; the aspect threshold choice (1.3 vs 1.35) swaps them
  in/out — a real tuning knob, decide against Tier-1/Tier-2.
- **Re-pivot (PR-0) coupling nuance:** the strongest ROD rocks are ALREADY
  well-pivoted (A4 11.7%, A6 1.5%, A10 0.7%, A7 0.8%, A8 2.6% offset), so their
  lobes barely orbit a wrong point. **Asteroid3 is the one rock that is both a
  ROD (1.39) and badly off-pivot (20.4%)** — it's the single strongest reason
  PR-0 precedes PR-2. (PR-0 still independently justified for the off-pivot BLOB
  Asteroid2 (13.5%): gyration/COM realism.)

## Design

### Representation — multi-SPHERE snapshot (CONFIRMED 2026-07-08)
Bake 2–4 covering **spheres** per mesh along its principal axis (3D local center
+ radius each). Per solve, project each sphere **center** through the asteroid's
current 3D world orientation onto the plane (drop the plane-normal component);
the projected disc **radius is the sphere radius, unchanged**. Feed the
resulting in-plane circles into the **same** obstacle loop the solver already
runs.

- **Why spheres, not 2D hulls / ellipsoids:** a sphere is the one primitive
  whose orthographic projection is a circle of its own radius from *every*
  direction — projection-invariant radius. Only the **spacing between projected
  centers** changes as the rock tumbles (end-on → centers overlap → looks round;
  broadside → centers spread → looks long), which is exactly the honest
  behavior. Discs/ellipsoids would change projected size with orientation and
  force per-orientation recomputation — the cost we're avoiding.
- **Known conservatism:** a sphere is fatter than a plate-like lobe across its
  thin axis. For a *tumbling* body that over-berth is honest (the rotation-robust
  envelope of a tumbling plate is fat); for *stable* objects it isn't — which is
  why polygons/ellipsoids stay on the environment-object rung.
- Reuses the exact circle inner loop — no new Burst distance function, no
  variable-length vertex arrays, no per-step polygon code. Multi-sphere = 2–4
  `ObstacleData` rows instead of 1.
- Baked once per shared mesh (like `MeanVertexRadius`); only the per-solve
  center projection is per-instance.
- Captures **elongation, not concavity** (an L-shape gets its bounding lobes,
  not the notch) — acceptable for asteroids, where the swept envelope is what we
  want.

### Baking algorithm (CONFIRMED 2026-07-08)
Baked into `MeshInfo.cachedLobes` (centers + radii + λ₁/λ₂ aspect) via
`OnValidate`, **alongside `cachedMeanRadius`** from PR-0 — NOT a runtime
`Dictionary` cache (runtime needs no CPU-readable meshes). Deterministic — no
`Math.random` (RL reproducibility).

- **Area-weighted throughout.** PCA covariance and lobe fit are **triangle-area-
  weighted**, not raw-vertex — matching PR-0's signed-tetrahedron centroid
  choice and dodging tessellation-density bias (a densely-tessellated end would
  otherwise pull the axis/placement toward it). Lobe centers are offsets from
  the (post-repivot) volume-centroid pivot = the true orbit center.
- **Placement:** area-weighted PCA (covariance eigenvectors) → principal axis;
  partition via **k-means seeded at even axial quantiles**, ~5 fixed Lloyd
  iterations (seeding is axis-order → deterministic). One covering sphere per
  lobe (3D local center + radius).
- **Per-lobe radius statistic — MUST match #78:** radius = **mean** (same
  quantile knob #78 exposes) of the lobe's assigned distance distribution
  (area-weighted) — NOT circumscribed max. Keeps "round" and "elongated" rocks
  on one berth philosophy; a per-lobe circumscribe would silently re-introduce
  the phantom berth #78 removed and give elongated rocks *more* berth than blobs.
- **Sphere count schedule** (stingy — every extra sphere spends the fixed 64
  solver budget, see below; thresholds calibrated by step-0):
  - aspect `< ~1.3` → **1** — must be **byte-identical** to today's single
    `MeanVertexRadius` circle (the "round" branch is a provable no-op).
  - `~1.3–2.5` → **2**
  - `> ~2.5` → **3** (reserve 4 only if step-0 shows genuinely rod-like rocks)
  - Hard cap: **3**.

### Plumbing, selection & budget (CONFIRMED 2026-07-08)
Composes with #80's nearest-N selection (`ObstacleSelection.KeepNearest`).

- **Solver-side expansion only.** Multi-sphere applies to the **MPC stage cost
  only**. `QueryObstacles` and `FieldBaker`/NavField stay one-circle-per-asteroid
  (coarse ~3-unit grid makes lobes inert for routing; keeps the field bake
  untouched and cheap).
- **Atomic selection at asteroid granularity.** `KeepNearest` keeps ranking
  *whole asteroids* (untouched); expansion happens **downstream** of selection,
  so a rock is never partially kept (no holes in an envelope). Expand-then-
  select-circles is forbidden — it breaks atomicity.
- **Lobe data rides on `DetectedObstacle`, no runtime `GetComponent`.**
  `QueryObstacles` (holds `ast`) attaches the baked per-mesh lobe set + the
  asteroid's current world rotation to each `DetectedObstacle`; ships attach
  nothing → single circle. `ConvertObstacles` projects centers and emits 1–3
  `ObstacleData` rows per obstacle. (Reaching lobes from `ConvertObstacles` via
  `GetComponentInParent` is banned — [[feedback_getcomponent_in_awake]].)
- **Budget: solver `ObstacleData` array 64 → 96, atomic-admit.** Admit whole
  asteroids until the next rock's lobes wouldn't fit, then stop. ~+50%
  worst-case on the hot obstacle loop (`512 × ~17 × obstacles`); est. solver
  ~0.5 → ~0.6 ms — **measure on the benchmark row**, don't guess. (Chose 96 +
  measure over holding 64 & seeing fewer asteroids.)

### Classifier — circle vs multi-sphere (CONFIRMED 2026-07-08)
Most rocks stay single-circle; only big/elongated/slow ones upgrade. Three
degenerate cases collapse back to one circle:

| Case | Test | Where | Result |
|---|---|---|---|
| Round / plate | **aspect = λ₁/λ₂** (two largest area-weighted PCA extents) `< ~1.3` | bake time, per mesh | circle |
| Too small to matter | world radius `< ~1 ship radius` / `~1 NavField cell` | query time, per instance (scale) | circle |
| Spinning fast | `\|ω\|·horizon > ~20–30°` silhouette swing | query time, per instance (live `Rb.angularVelocity`) | circle |
| **Otherwise (big, rod-like, slow)** | — | — | **multi-sphere** |

- **Aspect is λ₁/λ₂, NOT λ₁/λ₃** — a *plate* (λ₁≈λ₂ ≫ λ₃) classifies as round
  and stays a single (fat) circle: a chain of spheres along one axis can't
  represent a plate, and a tumbling plate's rotation-robust envelope IS a fat
  circle. Only genuine **rods** (λ₁ ≫ λ₂) get lobes.
- **Fast spinners stay single-circle by design** (CONFIRMED). The solver freezes
  each obstacle at its t=0 snapshot orientation for the whole horizon; a rock
  rotating ~50° across 1.7 s would point its elongation the *wrong way* by
  end-of-horizon — worse than an orientation-agnostic circle. Rejected
  mitigation: per-horizon-step re-projection via constant ω — it's the per-step
  rotation cost spheres exist to avoid and breaks the flat unchanged
  `ObstacleData` array. Multi-sphere is explicitly a **slow-rock optimization**.
- **Spin-gate contingency** (if step-0 shows the field is mostly fast tumblers):
  pick *then*, with data — (1) relax the gate toward ~45° and let the benchmark
  say if a stale-but-elongated envelope still beats a circle, (2) lower shipped
  `spinRange` (game-feel, out of scope, user call), or (3) ship narrow (helps
  only the slow tail). Not pre-committed.

Thresholds above are starting points; **step-0 calibrates them**.

### Eval — two-tier (CONFIRMED 2026-07-08)
Multi-sphere's exposure risk is **structurally lower than A3's**: A3 needed a
field *geometry* (sub-diameter gaps) the benchmark lacked; multi-sphere needs
elongated *meshes*, and the benchmark draws from the **same shared
`AsteroidSpawnSettings` set as production** — so "does the benchmark exercise it"
reduces to step-0's "are the meshes rods." Still two-tier:

- **Tier 1 — mechanism proof (deterministic, gating).** Targeted micro-scenario:
  one elongated rock at fixed slow spin, ship passing close along its thin axis;
  measure **min clearance accepted on the thin vs fat axis**. Single-circle
  over-berths the thin axis; multi-sphere should permit a tighter thin-axis pass
  with fat-axis berth unchanged. Isolates the feature from chase noise; becomes a
  regression test. **Must pass to ship** (this is the end-to-end proof A3 lacked).
- **Tier 2 — aggregate (mandatory).** Existing chase-benchmark row vs committed
  synthesis baseline: chase distance (down = good), collisions + impact impulse
  (up = cost of cutting closer), solve ms (the 64→96 cost). Committed baseline
  JSONL + python compare, same as #78.
- **Decision rule:** Tier 1 must show the mechanism works. If Tier 1 works but
  Tier 2 is flat, ship narrow (situational, not inert — unlike A3), don't kill.

### Wiring & config — single gate site, zero new seams (CONFIRMED 2026-07-08)
Satisfies CLAUDE.md "zero new wiring" rule — no new service, no Commander/
UnitService pass-through, no `Initialize` signature change.

- **Round/plate gate is baked, not runtime.** The K decision is frozen into
  `cachedLobes` in `OnValidate` (a plate/blob bakes to a single lobe), so there
  is no runtime aspect threshold to thread anywhere.
- **One runtime gate-evaluation site: `ConvertObstacles`** (already reads
  `Config` from `MpcSettings`). `QueryObstacles` attaches
  `{cachedLobes, worldRotation, angularVelocity}` to each `DetectedObstacle`
  (ships attach nothing → single circle); `ConvertObstacles` applies the small +
  spin gates using the carried scale/ω, emits baked lobes or the single circle,
  and does the atomic-admit. Lobe data rides the `DetectedObstacle` that already
  flows producer→solver — no new seam.
- **Config homes:** aspect threshold → bake-time only (`OnValidate`); small +
  spin thresholds → `MpcSettings.asset`.
- **Kill switch / A-B:** one serialized bool `MpcSettings.multiSphereObstacles`
  forces K=1 at the conversion site. Provably safe — K=1 is byte-identical to
  today's single-circle path. (The feature is also self-disabling via its own
  gate, but the bool gives a clean one-line benchmark A/B and ship-dark.)

## Fallback ladder (for later, non-asteroid objects)
Recorded in PR #78 body. When non-spherical *environment* objects arrive:
1. **Multi-circle decomposition** (this doc) — same inner loop, captures
   elongated / L shapes, rotation handled by transforming centers.
2. **Convex polygons for the K nearest obstacles** if (1) is insufficient —
   worth it only for stable-orientation objects where tightness survives the
   horizon.
3. **Arbitrary silhouettes stamped into the terminal NavField grid**, which
   already handles any shape for the routing/topology half (milder staleness
   caveat: field rebuilds every ~0.15–1 s; at ~3-unit cells silhouette-vs-circle
   differences mostly vanish into the grid).

## PR sequencing (CONFIRMED 2026-07-08)
Each PR independently green and reviewable.

- **PR-0 — mesh re-pivot — DONE (#87, merged `ee684ad7`).** NOT a destructive
  rewrite: `AsteroidPivotPostprocessor` (`AssetPostprocessor.OnPostprocessModel`)
  recenters imported asteroid meshes on their **signed-tet VOLUME centroid**
  (= rigidbody COM / rotation center, NOT the tessellation-biased vertex mean)
  at import time — reversible, zero runtime cost. Verified: volume-centroid
  offset ~0% on all 10 meshes; full suite 322/0/3. Report tool gained a
  `volOffset` column as the oracle. NOTE the effect lives only in each machine's
  gitignored `Library/` cache (produced by the import) — a warm Library needs a
  reimport of `HD_Asteroids/Models/` to pick it up (fresh clone/CI = automatic).
  (`cachedMeanRadius` bake deferred to PR-2's `OnValidate` work.)
- **PR-1 — step-0 measurement.** Extend `AsteroidCentroidOffsetReport` with
  area-weighted PCA extents + spin-swing. Output = **go/no-go + threshold
  calibration**. Tiny, no runtime code. Explicit checkpoint: read its numbers
  before committing to PR-2; no-go stops here cheaply.
- **PR-2 — multi-sphere.** `cachedLobes` bake in `OnValidate` + area-weighted
  k-means baker; `DetectedObstacle` carries lobes/rotation/ω; `ConvertObstacles`
  single-site gating + atomic-admit; buffer 64→96; `MpcSettings.multiSphereObstacles`
  bool; Tier-1 micro-scenario + Tier-2 benchmark row.

## Test list (PR-2)
1. **Baking determinism** — same mesh → identical lobes (RL reproducibility).
2. **Projection radius-invariance** — a lobe's projected disc radius is constant
   across an orientation sweep (the load-bearing property).
3. **K=1 byte-identical** — round/plate/small/fast rock → exactly today's single
   `MeanVertexRadius` circle (self-disable + kill-switch guarantee).
4. **Atomic-admit** — at the 96 boundary the last asteroid contributes all-or-
   none of its lobes; never a partial rock.
5. **Classifier boundaries** — plate (λ₁≈λ₂≫λ₃) → 1; rod → 2–3; small/fast → 1.
6. **Tier-1 micro-scenario** — thin-axis accepted clearance drops vs single
   circle; fat-axis unchanged (doubles as mechanism proof).

Solve-time is covered by the Tier-2 benchmark's solve-ms column (no separate
perf assertion).

## Resolved design decisions (grill 2026-07-08)
All confirmed with the user:
1. Step-0 measurement is a **go/no-go gate** (and gate-calibration). Prior:
   expected to pass.
2. Representation = **multi-sphere** (projection-invariant radii), NOT 2D
   hull/ellipsoid. True polygons reserved for stable-orientation env objects.
3. Baking = **area-weighted PCA + axis-seeded k-means**, mean-per-lobe radius
   (shares #78's knob), baked into `MeshInfo.cachedLobes` via `OnValidate`.
   Sphere schedule 1/2/3 by λ₁/λ₂, hard cap 3.
4. Plumbing = **solver-side expansion only** (NavField stays single circle),
   **atomic** nearest-N-asteroid selection, lobes ride `DetectedObstacle`,
   buffer **64→96 + measure**.
5. Classifier: aspect = **λ₁/λ₂** (plates stay round); **fast spinners stay
   single-circle** (slow-rock optimization; no per-step reprojection).
6. Depends on **PR-0 re-pivot** (lobes must orbit the true COM).
7. Eval = **two-tier** (Tier-1 micro-scenario gates; Tier-2 chase row aggregate).
8. Wiring = **single gate site in `ConvertObstacles`**, zero new seams;
   `MpcSettings.multiSphereObstacles` kill bool.

## Constraints / notes
- Touches the MPC obstacle hot path → **must carry its own chase-benchmark row**
  (compare against the committed synthesis baseline), same as #78.
- The cheap sphere collider is a **cull / self-collision** trigger, not the
  ship-impact surface (that's the LOD mesh collider) — so the AI obstacle radius
  and the physics sphere are free to diverge; multi-circle changes only what the
  *AI solver* sees, not physics.
- Baking parallels `MeanVertexRadius` in `AsteroidController` — same
  per-shared-mesh cache lifetime.
