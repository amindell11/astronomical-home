# SimplePool Registry + Live-Instance Flush

**Date:** 2026-07-13 (deferred from PR #138 review — Codex P1 on `EpisodeSetup.ProjectileFlush`)
**Status:** Design LOCKED via pr-prep 2026-07-15 — see "Decision brief" below, which **supersedes
the §Design mechanism** (domain `ProjectileService`, not a pool registry). Medium standalone PR.
**Origin:** `doc/Feature_Plans/RL_Episode_Reward_Layer.md` (episode reset needs "return every
in-flight projectile to its pool"); Codex P1 thread on #138 (`EpisodeSetup.cs:41`).

> **One-line intent.** Kill the scene-wide `FindObjectsByType<ProjectileBase>` in the RL
> episode reset by teaching the pool to answer "what's checked out" — via a self-registering
> non-generic pool registry that also removes the rotted reflection helper and its dead code.

---

## Problem

`ProjectileFlush.ReturnAllToPool()` (RL episode boundary, PR #138) scans the whole scene with
`FindObjectsByType<ProjectileBase>`. That violates the repo rule that FindObject-style scans are
Awake-only (`src/Asteroids3D/Assets/AGENTS.md`), and it sits inside the RL loop: once PR-3
training runs episodes back-to-back across N arenas, an O(scene) scan per reset scales with
asteroid count, not projectile count.

The scan exists because the pool cannot be asked. `SimplePool<T>` (`Utils/SimplePool.cs`) is a
**static generic class**: every closed type (`SimplePool<Laser>`, `SimplePool<Missile>`, …) has
its own independent statics, there is no shared base, and the CLR cannot enumerate the
instantiations of a generic. So no "for all pools" operation is expressible without extra
machinery — and the machinery that exists demonstrates the failure mode:

- `SimplePoolManager.ClearAllPools()` reflects over a **hand-maintained list of closed pool
  types** that names only `PooledAudioSource` (plus an "add other types here as needed" comment
  nobody obeyed). It never knew about projectile or VFX pools.
- It has **zero callers** — a rotted registry that is also dead code.

Meanwhile the pool already half-tracks what we need: `InstanceToKey` records every instance it
ever created, and all instances stay parented under the per-type `Pool_<T>` root. The episode
flush was asking the scene for information whose rightful owner is the pool.

**Consumers as of #154 (game capture):** the same scan now has three call sites that all
migrate to the registry when it lands — `ProjectileFlush.ReturnAllToPool()`/`ActiveCount()`
(`RLHarness/EpisodeSetup.cs`), `ShipDiagnosticsOverlay.DrawProjectiles`
(`Editor/Tests/PlayMode/Common/ShipDiagnosticsOverlay.cs`, per-captured-frame trail markup —
flagged by Codex on #154 and accepted pending this PR), and the capture runner's teardown
flush (`CaptureScenarioPlayModeTests`, via `ProjectileFlush`). The overlay wants
`ForEachLive` read-only enumeration, not the flush.

## Design

1. **Non-generic `SimplePools` registry** (same file). Each `SimplePool<T>` self-registers a
   small ops handle on first use (static-init path via `GetOrCreateStack`/`EnsurePoolParent`).
   Self-registration is the structural fix for the enumeration gap: a pool that exists has
   registered, *because registering is part of coming into existence* — the list cannot rot,
   and no reflection is needed.
2. **Explicit `Live` set per pool**: add in `Get`, remove in `Release`, corpse-tolerant
   (destroyed-instance skip, mirroring the existing stack-pop guard). O(live) enumeration and
   the honest data structure for "checked out" (today it is only implied by
   `InstanceToKey ∖ stacks`).
3. **Registry surface:** `SimplePools.ForEachLive(Action<MonoBehaviour>)` and
   `SimplePools.ClearAll()`.
4. **`ProjectileFlush` consumes it**: filter `is ProjectileBase p → p.ReturnToPoolImmediate()`.
   The flush MUST keep going through the projectile's own return path (it resets projectile
   state) — the pool enumerates, the caller owns the domain action. Do not add a raw
   "release everything" that bypasses `ReturnToPoolImmediate`.
5. **Delete `SimplePoolManager`** — superseded by `SimplePools.ClearAll()` (which, unlike its
   predecessor, actually covers every pool).

## Non-goals / notes

- **Not** arena-scoped pooling. `SimplePool` statics remain the documented interim
  process-wide seam; the `Live` set rides inside it and moves with it when the multi-arena
  rethink makes pooling arena-scoped (`doc/Feature_Plans/Multi_Arena_Substrate.md` deferrals).
- Episode-purity side-thought recorded, not scoped: `ForEachLive` would also let episode reset
  flush live pooled VFX/audio, not just projectiles, if training-observation purity ever wants it.

## Tests

- EditMode: registry self-registration (touch two pool types → both enumerable); Live-set
  add/remove/corpse-tolerance; `ClearAll` covers a type the old hand-list missed.
- Existing PlayMode episode smoke already asserts zero active projectiles at episode start —
  it becomes the integration proof for the swapped flush implementation.

## Files

- `Utils/SimplePool.cs` — `SimplePools` registry, `Live` set, delete `SimplePoolManager`.
- `RLHarness/EpisodeSetup.cs` — `ProjectileFlush` swaps scan → registry.
- `Editor/Tests/PlayMode/Common/ShipDiagnosticsOverlay.cs` — `DrawProjectiles` swaps scan →
  `ForEachLive` (drop the one-line scan-justification comment there when doing so).
- `Editor/Tests/EditMode/` — registry/live-set units.

---

# Decision brief (pr-prep, locked 2026-07-15) — SUPERSEDES §Design

**Scope.** Kill the scene-wide `FindObjectsByType<ProjectileBase>` scans (episode flush +
diagnostics overlay) and delete the rotted `SimplePoolManager`. Mechanism changed from the
pool-registry design above to a **domain `ProjectileService`** at the same level as `UnitService`.

**Non-goals.** Unchanged: no arena-scoped *pooling* (though the service is arena-correct by
construction); VFX/audio purity not scoped — the service covers damage-dealing transients
(projectiles + concussion waves).

## Forks

**F1 — mechanism: `ProjectileService` in `GameServices`, not a pool registry** (user rejected
all pool-registry variants). Why: (1) the flush consumer already lives beside services —
`ResetPair` calls `unitService.RespawnShip` then flushes; (2) `SimplePool` statics are
process-wide, so any pool-level registry flush is **arena-blind** — a cross-arena bug once
N-arena training resets episodes asynchronously — while a service inside per-arena
`GameServices` is arena-correct for free; (3) plain injectable class = EditMode-testable, no
static pollution, no `DontDestroyOnLoad` wrinkles; (4) repo philosophy — pool statics are the
documented interim wart, services + DI are the norm. Registration carries the flush action
(`Register(instance, returnToPool)`), so no `IEpisodeFlushable`-style marker seam exists.

**F1b — wiring (option b):** separate injected seam on the ship, **not** `IShooter` (stays
identity-only). `WireShipDependencies` gains `ship.Weapons?.SetProjectiles(...)` beside the
existing `SetRegistry`/`SetArena` lines; `WeaponsController` stores it and pushes into mounted
weapons; `Reequip`/`ReplaceMount` re-pushes to fresh mounts.

**F2 — `ClearAll`: evaporated.** Delete `SimplePoolManager`; add **nothing** to
`SimplePool.cs`. The pool stays a dumb allocator; "what's live" is domain information and the
service owns it.

## Assumptions (locked)

1. `IProjectileService` beside `IUnitService` in `Game.Services`; implementation is a plain
   class (no MonoBehaviour). Surface: `Register(MonoBehaviour, Action returnToPool)`,
   `ReturnAllToPool()`, `ActiveCount`, `ForEachLive(Action<MonoBehaviour>)`.
2. Storage `Dictionary<MonoBehaviour, Action>`; corpse-tolerant reads (skip + prune destroyed
   keys, mirroring the pool's stack-pop guard); flush iterates a **snapshot** — return events
   mutate the set mid-flush.
3. Composed in `SessionHost.ComposeSession`; seventh `GameServices`/`IGameServices` member with
   the same null-check ctor pattern; `ServiceContracts`/`BootstrapContracts` EditMode tests
   updated for the new arity.
4. Null-tolerant wiring: an unwired ship fires fine, just unregistered — only RL/capture
   contexts flush.
5. Registration point: `WeaponBase<TProj>.Fire()` — the single spawn chokepoint for every
   `ProjectileBase`. Flush action = `proj.ReturnToPoolImmediate` (domain return path — reset +
   events + pool release; never a raw pool release).
6. Deregistration: service subscribes the existing `ReturnedToPool` event at register, removes +
   unsubscribes when it fires. `ProjectileBase`'s return path untouched.
7. Consumers: `EpisodeSetup.ProjectileFlush` **deleted** (fixtures call the service they already
   hold); `ShipDiagnosticsOverlay.Draw` takes the service as a parameter, filters
   `is ProjectileBase`, scene scan + its justification comment deleted. The "zero active at
   episode start" assertion keeps its call shape and now also covers waves (the purity win).

## Blindsiders (resolved)

1. **Wave lifecycle.** `ConcussionWave` gains `event Action Released`, raised just before its
   self-release — without it a stale registration lets a later flush `Release` an already-pooled
   instance (duplicate stack entry → pool hands the same wave out twice; corpse guard can't
   catch alive-but-pooled). Grenade→wave registration via **`ITransientSpawner` cascade**:
   `interface ITransientSpawner { event Action<MonoBehaviour, Action> Spawned; }` — at
   `Register` the service subscribes when the registrant implements it, auto-registers announced
   children (same rule recursively), unsubscribes at deregistration; `Grenade.Detonate` raises
   `Spawned(wave, flush)` before its own pool return. A service back-ref stamped on
   `ProjectileBase` was **rejected** (user): service field on a domain object, `ResetState`
   clearing burden, wrong-arena hazard on pooled reuse. Registrants never know the service exists
   — symmetric with `ReturnedToPool` deregistration.
2. **`GameServices.ClearAll()` flushes:** calls `ReturnAllToPool()` — fixes the existing leak of
   live projectiles across sector transitions (they survive under the `DontDestroyOnLoad` pool
   root today).
3. **Tests.** EditMode units for the service (register / flush / corpse / snapshot-reentrancy /
   spawner cascade); existing zero-active PlayMode assertions are the integration proof for the
   swapped flush; extend the existing ConcussionGrenade fixture (at whatever level already
   detonates waves) with detonate → flush → assert wave deregistered + inactive.

## Files (supersedes §Files)

- `Game/Services/Projectiles/IProjectileService.cs` + `ProjectileService.cs` (new, + `ITransientSpawner`)
- `Game/Services/GameServices.cs` + `IGameServices.cs` — seventh service + `ClearAll` flush
- `Game/Bootstrap/SessionHost.cs` — compose
- `Game/Services/Units/UnitService.cs` — `WireShipDependencies` push
- `Ships/Weapons/WeaponsController.cs` — `SetProjectiles` store/push + `Reequip` re-push
- `Combat/Weapons/WeaponBase.cs` — hold ref, register in `Fire()`
- `Combat/Projectiles/Grenade.cs` — `ITransientSpawner`; `ConcussionWave.cs` — `Released` event
- `Combat/Projectiles/ProjectileBase.cs` — **untouched**
- `Utils/SimplePool.cs` — delete `SimplePoolManager` only
- `RLHarness/EpisodeSetup.cs` — delete `ProjectileFlush`
- `Editor/Tests/PlayMode/Common/ShipDiagnosticsOverlay.cs` — service param, drop scan
- `Editor/Tests/PlayMode/RLEpisodePlayModeTests.cs` + `CaptureScenarioPlayModeTests.cs` — call the service
- `Editor/Tests/EditMode/ServiceContractsEditModeTests.cs` + `BootstrapContractsEditModeTests.cs` — arity
- `Editor/Tests/EditMode/ProjectileServiceEditModeTests.cs` (new) + ConcussionGrenade fixture extension

⚠ Overlap: #155 (agent-1) also rewrites `RLEpisodePlayModeTests` — second-merger-adapts.

## Build decisions (2026-07-15, implementation)

- **Post-#155 flush call sites** (new since the brief): `EpisodePair.Reset`, `CheckpointEvaluator.Run`,
  `RLAgentPlayModeTests`, `TrainingHost`/`EvalHost`. The service is passed as an **explicit parameter**
  (`EpisodePair.Spawn(units, arena, projectiles, …)`, `CheckpointEvaluator.Run(units, arena, projectiles, …)`)
  rather than exposed off `UnitService` — no service pass-through seam. `HarnessArena.Compose` creates and
  wires it for the RL host scenes; PlayMode fixtures create + `SetProjectiles` their own beside `SetArena`.
- Naming: `IGameServices.Projectiles`; `UnitService.SetProjectiles` is one-shot, mirroring `SetArena`
  (same wrong-arena hazard). `ConcussionWave` gets a public `ReturnToPoolImmediate()` (raises `Released`,
  then releases) used by both its self-release and the flush action `Grenade` announces.

## v2 (user directive + adversarial review, 2026-07-15): make orphan debris IMPOSSIBLE — supersedes Assumption 4

User rejected debris sweeps as symptom-treatment ("don't make that mistake possible"). Two invariants
replace the null-tolerant wiring assumption:

1. **Registration is mandatory.** `WeaponBase<TProj>.Fire()` with no service wired is **loud + inert**
   (error + no spawn, before conditions consume charge/ammo) — an unregistered projectile cannot exist.
   Production is always wired (`WireShipDependencies`); Unity's test runner fails on the error, so a
   fixture that forgets wiring fails instead of leaking. Assumption 4 ("unwired ship fires fine") is dead.
2. **Live transients ride their context root.** `ProjectileService(Transform liveRoot)` reparents each
   instance under the root at `Register` (session root in production, arena/fixture host in tests, must be
   a non-moving root). Destroying the context physically destroys its in-flight transients — cross-fixture
   and cross-session leakage is structurally impossible, at the cost of pooled instances no longer being
   shared across contexts (corpse guards on both sides absorb that).

Consequences: `PlayModeWorldFixture` owns a per-test `Projectiles` service rooted at its arena host;
firing fixtures wire it in one line; all foreign-debris **sweeps became assertions** (a leak is a fixture
bug to fix at its source, never swept — and the scene-truth assertions restore the falsifiability the
registry-count assertion alone had lost). Review fixes folded in: `SessionHost.UnloadSector` now flushes
(the sector-transition leak Blindsider 2 promised to fix), flush snapshots are per-call (nested-flush
safe), re-registering a live instance logs an error (the only observable signature of a pool double-checkout),
the service field lives on `WeaponBase<TProj>` (hitscan weapons carry a no-op setter only), and
`ITransientSpawner` moved to `Combat.Projectile` (registrants stay ignorant of `Game.Services`).
