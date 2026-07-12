# Asteroid Field: Overlap Drift + Player Exclusion

Handoff for two related follow-ups on the deterministic asteroid field (merged
to `main` via #57/#59). Both are about **where baseline asteroids are allowed to
exist**. Neither is started. Written against `main` @ `995f0a3d`, then refined
through a design review (the decisions below supersede the original draft).

Key files:
- `src/Asteroids3D/Assets/Scripts/Asteroids/Fields/Core/AsteroidFieldLayout.cs`
  — headless baseline generator (the LUT). This is where both fixes mostly land.
- `src/Asteroids3D/Assets/Scripts/Asteroids/Fields/Core/AsteroidFieldModel.cs`
  — `GetChunkContents` calls `Layout.GenerateCell(chunk.x, chunk.y)`; **a chunk is
  a single cell**, streamed in independently.
- `src/Asteroids3D/Assets/Scripts/Asteroids/Fields/UpdatingAsteroidField.cs`
  — MonoBehaviour streaming tier (spawn/unload, dynamic-world knowledge).
- `src/Asteroids3D/Assets/Scripts/Asteroids/Fields/AsteroidFieldSettings.cs`
  — tuning asset (add new knobs here).
- Tests: `.../Editor/Tests/EditMode/AsteroidFieldCoreEditModeTests.cs`
  (determinism-level unit tests — extend these; both fixes are headless-testable).

The hard constraint for **both** items: the field is deterministic and
session-persistent. "A given spot always holds the same asteroids" must survive
every change here. Any suppression that depends on runtime/dynamic state must
happen at *instantiation* time and must NOT mutate the LUT.

Sequencing: **Item 1 → Item 2a**, two independent PRs through the normal
worktree loop. **Item 2b is deferred** (see below).

---

## Item 1 — Asteroids spawn inside each other → "unprogrammed drift"

### Root cause (confirmed by static reading)

`AsteroidFieldLayout.GenerateAsteroid` places each asteroid at a uniform-random
position inside its cell with zero awareness of any other asteroid's position or
radius:

```csharp
// AsteroidFieldLayout.cs, ~line 204
var cellMin = new Vector2(id.CellX * p.CellSize, id.CellY * p.CellSize);
var position = cellMin + new Vector2(rng.Range(0f, p.CellSize), rng.Range(0f, p.CellSize));
```

With `averageAsteroidsPerCell = 6` (× up to `densityMultiplierRange.y = 1.7`) in
a 40u cell, asteroids routinely spawn interpenetrating. Physics resolves the
**deep** overlaps by ejecting the pair — **that ejection is the visible drift.**
It is not a kinematics/authoring bug; it is spawn-time overlap.

Sizing context (from `SpawnSettings.asset`): mesh volumes 4.2–121 →
sphere-equivalent radii ≈ 1.0–3.1u; `massScaleRange (0.5, 2.5)` →
`scale = massFactor^(1/3)` ∈ [0.79, 1.36], so **max effective radius ≈ 4.2u**,
typical 1.5–2.5u. Max radius ≪ cell (40u) — this is what makes a 3×3
neighbourhood provably sufficient.

### Fix — deterministic priority-rejection (bounded, chunk-local)

Do the rejection inside the headless layout so it stays reproducible and
unit-testable. The rule must be evaluable from a **bounded** neighbourhood
without recursion — otherwise deciding one cell would require an unbounded
sweep over all earlier cells (the same load-order dependence that rules out the
naive "check the live pool and retry" approach).

**The rule:**

> Candidate X is **rejected** iff there exists an overlapping candidate Y with a
> lexicographically-smaller key `(cellX, cellY, index)`. Nothing else.

- This is decidable by inspecting only candidates within `maxRadius` of X — a
  bounded neighbourhood — and is fully order/load-independent. It never recurses:
  a rejected Y **still counts as a blocker** for a lower-priority X (we do not
  ask whether Y itself was accepted; that would reintroduce recursion). The cost
  is that holes compound slightly — accepted.
- This is *priority rejection*, not true Poisson-disk blue-noise. It kills
  overlaps deterministically and cheaply; it does not maximise packing.

**Neighbourhood: full 3×3 Moore (all 8 neighbours).** Since max radius (~4.2u) ≪
cell (40u), no asteroid can reach past an immediate neighbour, so 3×3 catches
every cross-cell overlap. A plus-shape (4-neighbour) was considered — corner
overlaps are ~2–3% of total and the perf saving is negligible — and rejected:
skipping corners leaves a permanent deterministic residual (a diagonal pair
where neither cell sees the other, so both spawn and eject forever). Full Moore
gives **zero residual** for a few hundred cheap extra RNG draws per streamed
cell.

**Overlap radius:** volume-sphere, with a tunable margin:

```
r = (3 * cachedVolume / (4π))^(1/3) * scale * packingMargin
```

with a `minSpacing` floor for tiny meshes. This targets the deep interpenetration
that causes *violent* ejection; grazing contact at the boundary produces only
sub-unit depenetration nudges (imperceptible). The conservative alternative —
circumscribing-sphere radius (`bounds.center.mag + extents.mag`), which would
guarantee literally zero collider overlap — was rejected: it is ~1.7× the
volume-sphere radius → ~3× interaction area → severe density loss, to eliminate
a cosmetically-invisible failure. `packingMargin` (~1.15–1.25) approximates the
convex hull's real reach; tune in-editor.

### Structure (preserve the determinism keystone)

- The accept/reject predicate lives in `GenerateCell`, **not** in
  `GenerateAsteroid`. `GenerateAsteroid(id)` must keep returning the same *pose*
  regardless of neighbours — the keystone test
  `GenerateAsteroid_FromIdAlone_MatchesCellGeneration` and the tombstone/overlay
  pipeline depend on it. A rejected ID simply never gets added to `results`; it is
  never a candidate to tombstone, so the overlay stays consistent.
- Extract a lightweight `GenerateCandidate(id) → (position, radius)` that performs
  exactly the **first 4 RNG draws** (position ×2, meshIndex, massFactor → radius)
  in the same stream order as `GenerateAsteroid`, then stops. `GenerateAsteroid`
  calls `GenerateCandidate` and continues drawing; the neighbourhood test calls the
  same `GenerateCandidate`. One source of truth for the opening draw sequence, so
  "cell C evaluating neighbour D" and "cell D evaluating itself" can never diverge.
- **Count-per-cell is still decided first** (`CountForCell`, unchanged), then
  accept/reject. Stable IDs `(cellX, cellY, index)` stay intact; a rejected
  asteroid is a deterministic skip, not an ID renumber — exactly like the existing
  `fieldRadius` cull in `GenerateCell`.
- **Scope: baseline-vs-baseline only.** Rejection ignores authored/overlay entries
  (fragments) and tombstones. Fragments spawn at runtime-dependent positions;
  folding them into layout acceptance would reintroduce load-order nondeterminism.
  Fragment-on-baseline spawn overlap is left to physics.

### Why not the alternatives
- "Check the live pool and retry on collision" → depends on load order →
  non-deterministic. Rejected.
- "Walk a global order, accept if it clears already-accepted" → "already-accepted"
  is defined by an unbounded global walk → deciding one cell needs every earlier
  cell. Replaced by the local priority rule above.
- Jittered sub-lattice / relaxation → kills the organic Perlin look and doesn't
  handle cross-cell overlap or variable radii cleanly.

### New settings knobs (`AsteroidFieldSettings`)
- `packingMargin` (float, multiplier on the summed radii; default ~1.15–1.25).
- `minSpacing` (float, floor on edge-to-edge gap for tiny meshes).
- Effective density drops after rejection — bump `averageAsteroidsPerCell` to hit
  the same visual density. The compensation multiplier is **empirical**, not
  analytic (rejection rate rises nonlinearly with density); tune in-editor.

### Tests (extend `AsteroidFieldCoreEditModeTests`)
- **Min separation:** over a patch of cells, no two accepted asteroids are closer
  than `(r_i + r_j) * packingMargin`.
- **Determinism:** same seed → identical accepted set (IDs + poses), including the
  rejections, across two independent generations.
- **Reload identity:** a rejected ID stays rejected on regen; IDs do not renumber.
- **Candidate consistency:** the radius/position a cell computes for a neighbour
  (via `GenerateCandidate`) matches what that neighbour computes for itself.

### Caveats to record (not decisions)
- The editor density preview (`UpdatingAsteroidField.Editor.cs` heatmap) reads
  `CountForCell`, which is **pre-rejection**, so it now *overstates* actual on-screen
  density. `AsteroidFieldTuningEditModeTests` still passes (count is decided first,
  unchanged). Note this so nobody "fixes" the discrepancy.
- Neighbour candidates are regenerated up to 9× across a streamed region (no
  cross-cell caching). Redundant but cheap and stateless — keeps determinism with
  zero shared state. Accepted.

---

## Item 2a — Don't spawn on the player start ("hole" at spawn)

### Why this is safe to bake (unlike dynamic occupancy)

`Sector.PlayerStart` is a **static, authored** plane-space `Vector2` (resolved
from a hand-placed `PlayerStartMarker` child). Because it never moves, an
asteroid whose home falls inside a start bubble can be culled at generation time
with full determinism and persistence — exactly parallel to the existing
`fieldRadius` cull. The result is a **permanent** clearing: a home you can fly
back to, not one that refills the instant you leave (which is what a purely
transient approach would give).

### Design

- Add a general `ExclusionVolumes` list of `(center, radius)` (field-relative
  plane-space) to `FieldGenerationParams`; cull in `GenerateCell` /
  `GenerateAsteroid` right next to the `fieldRadius` check
  (`AsteroidFieldLayout.cs` ~line 191). An asteroid whose home lies inside any
  exclusion volume is never generated.
- **v1 populates the list from PlayerStart only**, via one authored
  `startClearRadius` knob (default ~30–40u ≈ one cell; tune in-editor). The list is
  general so structures can wire into it later — but no station/gate needing
  exclusion exists in the tree yet, so building the general collection plumbing now
  would be speculative (YAGNI). Add it when a structure that needs it lands.
- **Coordinate conversion (correctness trap):** spec `PlanePosition` is
  *field-relative* (`UpdatingAsteroidField.ToWorld` adds `fieldOriginPlane`), while
  `Sector.PlayerStart` is *absolute* plane-space. Convert before feeding the
  layout: `center_fieldRel = PlayerStart - fieldOriginPlane`.

### Tests
- Headless (core tests): an asteroid whose home is inside an authored exclusion
  volume is never generated; determinism otherwise unchanged.

---

## Item 2b — Dynamic spawn-time deferral: DEFERRED (until observed)

Original idea: for live "protected" colliders (player, adopted ships), when a
chunk loads and a baseline asteroid's home is currently occupied, skip
instantiating it this frame and retry later (the asteroid still deterministically
*exists* — it's just not placed while something sits there). Never touches the LUT.

**Why deferred:** with 2a carving the start clearing, 2b's remaining trigger
surface is narrow. Sector entry is covered by 2a; normal flight loads chunks
~80u *ahead* (`loadRadius = 80`), so rocks appear out in front, not on the player.
The only genuine cases left are park-and-return (fly away so a chunk unloads,
then back while it reloads near you) and moving adopted ships parked on a baseline
home. Those are niche, and 2b is the expensive item (streaming-tier changes,
a pending set, PlayMode tests).

**Decision:** ship Item 1 + 2a, then watch play. Build 2b **only if a rock is
actually observed ejecting off the live player or a ship** — against a reproduced
case, not a hypothesised one. If built, it lands in
`UpdatingAsteroidField.SpawnFromSpec` / `ProcessLoadQueue` (~lines 204–303):
before `AsteroidSpawner.Spawn`, overlap-test the spec's world position against a
small protected-collider set (the player anchor is already available via
`SetPlayer` / `CurrentAnchorPos`); if blocked, defer rather than cancel; drop from
pending if the chunk unloads first. **Do not** delete/tombstone baseline asteroids
for dynamic occupancy, and do not fold this into chunk load-state.

---

## Also open (separate handoff, not covered here)

Field-follow bug ("FieldFollower is broken"). Static reading of the
streaming-anchor wiring looked correct — needs runtime verification (Unity MCP)
to root-cause before touching. Not part of this doc.
