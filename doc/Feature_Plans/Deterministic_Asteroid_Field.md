# Deterministic Asteroid Field (Perlin LUT + Streaming + Override Overlay)

## Goal

Replace the current random, player-following annulus spawner with a
**deterministic, persistent** asteroid field. A given spot in a sector always
holds the same asteroids; fly away and back and they're still there. Player
edits (destruction, damage) persist for the session. Removes the
speed-dependent "weird behaviors" of the moving-annulus spawner.

## Current system (what we're replacing)

- `UpdatingAsteroidField` follows the player; every `densityCheckInterval`
  (0.15s) runs `ManageField`, spawning asteroids in a **moving annulus**
  (`updateMinSpawnDistance` 50 → `updateMaxSpawnDistance` 80) until
  `TotalVolume >= TargetVolume` or `ActiveCount >= maxAsteroids`.
- Positions and every attribute (mesh, scale, mass, velocity, spin) are pure
  `UnityEngine.Random`. Nothing is reproducible.
- Asteroids drift (linear velocity), spin, and can be fragmented/destroyed
  (`Fragger`). They're culled by a `CullingBoundary` `SphereCollider` trigger
  (`AsteroidController.OnTriggerExit`).
- Per-asteroid collider LOD lives in `AsteroidController.LateUpdate`: distance
  to `worldFollowTransform` toggles the detailed `MeshCollider` vs the cheap
  `SphereCollider` at `detailedColliderEnableDistance` (75u).

Key files: `AsteroidField.cs`, `UpdatingAsteroidField.cs`, `AsteroidSpawner.cs`,
`AsteroidFieldSettings.cs`, `AsteroidController.cs`, `AsteroidFieldSpawner.cs`
(sector adapter), `Fragger.cs`, `SpawnPool.cs`, `Registry.cs`.

## Design decisions (resolved)

1. **Bounded sector.** Finite deterministic set, not infinite streaming.
   Chunking is an internal spatial index for load/unload, not an infinite
   generator. Sector edge = **empty space beyond** (no wall this pass); if a
   playable-radius concept already exists in the sector systems, size the field
   to match it.
2. **Procedural baseline + sparse override overlay** (the Minecraft / No Man's
   Sky pattern). We never store the field — only *deviations from* it.
   - **Baseline layer:** the Perlin/hash LUT. Deterministic, regenerated on
     demand, never persisted. ~99% of asteroids.
   - **Override layer (session-only, in-memory):** tombstones + authored
     fragments + damage state (see below).
3. **Perlin drives density (organic clumps/voids), over hashed jittered cells.**
   - Space divided into cells; each cell seeds an RNG from
     `hash(sectorSeed, cellX, cellY)`.
   - Perlin sampled at the cell modulates a **density multiplier** around the
     authored average (e.g. 0.3×–1.7×) → clusters, belts, empty corridors.
   - Count per cell = `baseDensity × perlinMultiplier`; each asteroid gets a
     jittered position inside the cell from the cell RNG (no visible grid).
   - Every attribute (mesh, scale, mass, spin, initial orientation) is drawn
     from the same per-asteroid seeded stream, keyed on the asteroid's stable
     ID. **No `UnityEngine.Random` survives in the field.**
4. **Static baseline positions; drift resets on reload.**
   - Baseline asteroids may carry a (seeded, deterministic) small ambient
     linear velocity + spin for visual life, but their **LUT home is truth**.
     On reload, untouched asteroids respawn at home with their seeded velocity;
     the accumulated drift/rotation is discarded (imperceptible — small drift,
     and nobody tracks a rock's phase).
   - This is what keeps the sparse overlay sparse: ambient motion never forces
     persistence.
5. **Overlay contents (all keyed by stable ID, tiny + sparse):**
   - **Tombstones** — destroyed baseline IDs (skipped on load).
   - **Authored fragments** — pose + mesh/scale/mass, for fragments produced by
     destruction (they have no LUT home). Snapshotted on unload, respawned at
     rest on reload. *(Velocity/spin persistence deferred — see below.)*
   - **Damage state** — baseline IDs shot-but-alive, so they reload chewed-down
     rather than pristine.
   - Everything else regenerates from the LUT at home for free.
6. **Destruction persists its *outcome*.** Destroying a baseline asteroid
   tombstones its ID **and** writes its fragments as authored entries. Reload:
   skip the original, spawn the fragments. Fragments later destroyed leave the
   overlay and tombstone/spawn their own sub-fragments (bounded by min fragment
   size).
7. **Positions never persisted for the baseline field** (option A). A
   player-shoved-but-alive asteroid resets to home on reload like anything else
   — we deliberately do **not** persist displacement (avoids authored-pose +
   cross-chunk handoff machinery). What players remember — destruction and
   damage — is what persists.
8. **Stable IDs.** `(cellX, cellY, indexInCell)` or a hash thereof, independent
   of load order. Fragments get fresh authored IDs.
9. **Streaming.** Unified grid: cell = chunk = load/unload unit, **~40u**.
   Load radius ≈ current 80u; **unload at ~1.5× load radius (hysteresis)** so a
   player on a boundary doesn't thrash, and — critically — so the fragment
   freeze case (below) happens only out of view. Load work budgeted across
   frames like today's `maxSpawnsPerFrame`. All through the existing
   `SpawnPool`/`Registry` (allocation-free after warmup).
10. **No hard `maxAsteroids` runtime clamp.** A content-clipping cap would
    reintroduce load-order nondeterminism (which N survive depends on load
    order). Count is naturally bounded by `loadArea × baseDensity ×
    maxNoiseMultiplier`; use that computed worst-case to **pre-size the pool**,
    and keep a `maxAsteroids`-style number only as an **editor-time sanity
    warning**.
11. **Seed** is an authored `int` on the placed field component (serialized in
    scene/prefab instance), so two sectors sharing an `AsteroidFieldSettings`
    asset still get distinct layouts. Tuning (density, chunk size, noise
    frequency, mesh set) lives in the shared settings asset. Never
    time/runtime-derived.
12. **Collider LOD stays a separate axis from load/unload.** Because the load
    area is deliberately big, *loaded ≠ near* — we must not collapse
    collider-LOD into load-state. Keep the per-asteroid `LateUpdate` distance
    check as-is for now; chunk-granular collider-LOD is a deferred, profile-
    driven optimization.
13. **Core is headless-testable.** LUT generator + override overlay are plain
    C# classes, separable from MonoBehaviours, so determinism/persistence are
    unit-tested without physics.

## The fragment-freeze failure mode (understood + mitigated)

Worst case: destroy an asteroid (fragments flying fast) → chunk unloads → return
immediately → fragments frozen mid-arc at their snapshot pose. Mitigations:
- **Wide hysteresis + big load area:** the fragment chunk only unloads once the
  player passes the (large) unload radius, i.e. far enough that the freeze is
  unwitnessable. "Fly away and right back" near the blast never crosses it.
- **Deferred insurance:** persist fragment linear/angular velocity in the
  authored entry and resume motion on reload. *Not in the initial PR* — added
  only if playtesting shows a visible seam. (Space has no drag, so fragments
  never naturally settle, which is why velocity persistence — not
  snapshot-at-rest — is the real fix if needed.)

## PR sequencing

### PR1 — Prep refactor (behavior-preserving, no gameplay change)

Clean the spawner seams so chunks drop in.
- Extract an `AsteroidAttributes` struct (mesh, scale, mass, velocity, spin,
  orientation) and an attribute-provider seam. `AsteroidSpawner.SpawnRandom`
  becomes "roll random attributes → `Spawn(pose, attrs)`". The attribute
  *decision* moves out of the spawn call.
- Tighten the `AsteroidField` → `AsteroidSpawner.Registry` reach-through into a
  minimal query surface.
- `SpawnRandom` has exactly one caller (`AsteroidField.CheckAndSpawnAsteroids`,
  going away in PR2) — low risk. `SpawnFragment` (Fragger) stays.
- Note (don't yet execute) that `AsteroidFieldSettings` annulus/timer fields
  will be orphaned by PR2.
- **Tests:** structural EditMode tests proving the extraction is
  behavior-preserving (same inputs → same spawn call shape).

### PR2 — Deterministic streaming field + override overlay (the main event)

- New chunk-streaming manager (LUT generator + load/unload + override overlay)
  replaces `UpdatingAsteroidField`'s `Update` loop. **Keep the class
  name/adapter seam** — `GameConfig`, `SectorManifestSync`, `AdoptEntry`, and
  `AsteroidFieldSpawner` reference the concrete `UpdatingAsteroidField` type;
  preserving it avoids churn across four files.
- **Delete** the annulus brain from `AsteroidField`: density timer,
  `ManageField`/`CheckAndSpawnAsteroids`/`GetRandomFieldPos`,
  `TargetVolume`/`RecalculateTargetVolume`.
- **Remove the culling-boundary trigger** (`AsteroidController.OnTriggerExit` +
  `AsteroidCullingBoundary` tag) — chunk-unload replaces it. *(Leave
  per-asteroid collider-LOD `LateUpdate` alone.)*
- Seeded attribute provider replaces the `UnityEngine.Random` rolls
  (`GetRandomMeshInfo`, `RandomVelocity`, `RandomAngularVelocity`,
  `Random.rotationUniform`, mass/scale roll). `SpawnRandom` is fully replaced by
  the seeded path.
- Override overlay: tombstones + authored fragments (pose only for now) + damage
  state; session-only, in-memory, owned by the field; designed as a
  serializable POCO for future disk saves.
- Dirty/destruction hooks: destruction → tombstone + author fragments; damage →
  record damage state. Ambient asteroid-on-asteroid collisions are **ignored**
  (not persisted).
- Pre-size the pool from the computed worst-case count.
- Settings migration: retire annulus/timer fields; add chunk size, load/unload
  radii, noise frequency, density average + variation, editor-time count
  warning.
- **Tests (headless where possible):**
  - *Determinism:* same seed + cell → identical set (IDs, poses, attributes)
    across two independent generations.
  - *Reload identity:* load → unload → reload → untouched asteroids reappear at
    home identically.
  - *Persistence:* destroy → reload → original tombstoned, fragments present;
    damage → reload → damage retained.
  - *Hysteresis:* sitting on a boundary doesn't thrash load/unload.

## Deferred (tracked, not in PR1/PR2)

- **Full `AsteroidView` renderer rig extraction** — move `Renderer`/`MeshFilter`
  (and possibly collider LOD) out of `AsteroidController` into an injected rig,
  mirroring the ship `ShipView`/rig work. Belongs under the existing sim/visual
  decoupling program, not on the determinism critical path.
- **Chunk-granular / event-driven collider LOD** — profile-driven; only if the
  big load area makes per-asteroid `LateUpdate` a measured cost.
- **Fragment velocity/spin persistence** — resume fragment motion on reload;
  add only if the freeze seam is visible in playtesting.
- **Disk / save-game persistence** — serialize the overlay across app restarts;
  overlay is already designed as a serializable POCO.
- **Sector playable-radius boundary mechanic** — align the field to it if/when
  one exists.

## Open questions

- Exact chunk size vs noise frequency tuning (start ~40u chunks; tune by feel).
- Whether the seeded ambient drift is worth keeping at all vs pure-static
  baseline (cheap to toggle; decide during playtest).
