# Multi-Arena RL Substrate

*Draft • 2026-07-12 • status: design agreed (two grill sessions + Codex consult), PR-A1 starting*

> Realizes the deferred `project_multi_arena_rethink`. Builds on the landed
> GameDriver/SessionHost seam (#118/#120): per-session `GameSession`, per-arena
> `SessionProfile`, `SessionHost : ISessionPrimitives` driven from above. This doc
> decides the arena isolation mechanism and the world-state ownership that let N
> arenas run in one process for RL self-play.

## The problem

The tactical-RL direction wants N independent arenas per process for self-play
throughput. Three process-wide statics currently hold world-scoped state in a
single slot, so a second arena clobbers the first:

- `ObstacleFields.Active` (`AI/Scanning/IObstacleField.cs`) — the sector's live
  asteroid field; read by `Navigator` + `ObstacleScanner`. **The real
  AI-correctness blocker.**
- `NavFieldService.Instance` (`AI/Navigation/Field/NavFieldService.cs`) — the
  terminal cost-to-go field; read by `Navigator`. Keyed by target transform, so
  data-safe across arenas, but still a static handle.
- `GamePlane` (`Game/GamePlane.cs`) — the world↔plane coordinate convention.
  Configured per-session in `SessionHost`, so multi-arena already needed a
  `!IsConfigured` guard and `TeardownSession` resets it unconditionally (a latent
  multi-arena bug). **It carries zero per-arena state** — every arena shares one
  plane; separation is an in-plane offset applied at placement, not a plane
  reconfigure. So it isn't an isolation blocker; the fix is to stop mutating it.

Plus the raw `Physics.*` query surface (9 sites) that a shared physics space could
leak across.

> **Grill + Codex resolutions (2026-07-12).** The two static→injection PRs are
> fully specified below; the highlights that changed from the first draft:
> - **`ArenaContext` is a plain class** (`new`'d in `ComposeSession`, a bundle peer
>   like `EnvironmentService`), **not** a `MonoBehaviour` sibling. The one thing on
>   the session root that *must* be a component is **`NavFieldService`** (it owns the
>   `-90` Update pump + Burst lifecycle); `ArenaContext` just holds a reference to it.
> - **Provider slots are read per-frame off the handle**, not captured at
>   `Initialize` — this preserves today's register-later timing (the field registers
>   during `sector.Setup()`, after ships are wired). `arena.ObstacleField == null`
>   stays a silent zero-obstacle sense (legitimate between sectors).
> - **Offset is a `Vector2` on `ArenaContext`**, applied by a single `arena.Place(...)`
>   primitive at the ~5 placement sites; **reads stay on the global `GamePlane`**
>   (they convert already-offset entity positions). Below-seam mechanism never sees
>   the offset. Parenting-for-organization folds into `Place` at PR-B.
> - **`GamePlane` is frozen to compile-time constants, not "process-init-once."**
>   Production runs on **`PlaneAxis.Z`, origin 0** (`InitScene`/`TestScene` serialize
>   `planeAxis: 2`); the `PlaneAxis.Y` default in `SessionHost` is **dead code**. See
>   the rewritten `GamePlane` section.
> - **Hard-cut the statics** (delete `ObstacleFields` + `NavFieldService.Instance`),
>   no compatibility shim — a "current arena" shim would just be the global wearing a
>   hat. Proof = existing suites stay green + one new two-arena isolation test.

## Core design — `ArenaContext`

A **standalone below-seam service**, a peer of `EnvironmentService`/`UnitService`
in the `GameServices` bundle, that everything currently reading the three statics
keys off instead. It is the single per-ship world-frame handle, injected the same
way `IShipRegistry` already is (`UnitService.WireShipDependencies`).

`ArenaContext` carries:
- **the per-arena world offset** (`Vector2`, default zero) + the `arena.Place(...)`
  primitive that applies it at placement/reset (see `GamePlane` section),
- **the obstacle-field provider** (mutable slot read per-frame; `AsteroidFieldSpawner`
  registers into `services.Arena`, replacing `ObstacleFields.Register`),
- **the nav-field provider** (a reference to the `NavFieldService` component sibling,
  replacing `NavFieldService.Instance`),
- **the ship registry** (folded in — see below),
- *(deferred, PR-C)* a physics-query facade / `PhysicsScene` handle.

It is a **plain class** with no lifecycle of its own — a stable handle whose provider
slots consumers dereference each frame. `ComposeSession` constructs it:
`new ArenaContext(offset: profile.offset, registry: unitService.Registry,
navField: GetComponent<NavFieldService>())`; the obstacle slot starts null and the
spawner fills it during `sector.Setup()`.

### Why standalone, not an extension of `EnvironmentService`

`EnvironmentService` is Unity scene / `WorldRoot` **lifecycle** (LoadSceneAsync,
SpawnWorld, follower). The AI's world model needs the **spatial frame + world
providers**, a different concern. Injecting `EnvironmentService` into `AICommander`
would hand the AI `LoadSceneAsync` — a fat, leaky interface. A purpose-built
handle exposes only what the mechanism consumes, and it's the natural home for the
"one handle replaces the ad-hoc `SetRegistry`" consolidation the wiring philosophy
(pt 3) calls for. The two are both per-arena but co-vary for different reasons.

### Registry folds in

`WireShipDependencies` today does exactly `Targeting?.SetRegistry(...)` +
`aiCommander.SetRegistry(...)`. It becomes a single `aiCommander.SetArena(arena)`
(passed on into `Navigator.Initialize` — the per-ship idiom), with the narrow
consumer keeping its narrow dependency sourced from the handle:
`Targeting.SetRegistry(arena.Registry)`. Ownership stays with `UnitService`;
`ArenaContext` holds a reference — it is an aggregate injection surface, not an
owner of everything it exposes.

#### The `UnitService` arena-wiring invariant (PR-A1 Codex P2 resolution)

`WireShipDependencies` dereferences `arena` (it must, to pass it to each
`AICommander`), so `UnitService` needs the arena *set* before the first ship is
wired. `GameServices` now *requires* an `ArenaContext` but that alone doesn't set
it on the unit service — a naive reading (`GameServices` ctor calls
`unitService.SetArena`) makes the invariant unrepresentable but puts **behavior in
a DTO constructor**, which is the wrong ownership and seeds construction-order
fragility (Codex + grill agreed). **Resolution (PR-A1):** keep `GameServices` a
pure immutable container; add `SetArena` to the `IUnitService` **contract** (so the
requirement is discoverable), make it **one-shot** (throw on a conflicting re-set),
keep the ordered wiring in `ComposeSession` (the composition root), and **fail fast**
in `WireShipDependencies` (`InvalidOperationException`, not an NRE) if the arena is
unset.

**Standing rule this codifies:** *services are constructed with assignment only —
no callbacks, virtual calls, event publication, provider reads, or spawning during
construction; `ArenaContext` stays passive arena-scoped data/providers, never a
service locator services pull each other through; cross-service wiring and
initialization happen only in the composition root or at first use.*

**Deferred (Mid Dev Pool) — the strongest, high-impact-area version:** make the
arena *required constructor injection* into `UnitService` so the missing-wire case
is truly unrepresentable. This needs breaking the current ownership cycle
(`ArenaContext` needs the registry that `UnitService` owns, while `UnitService`
would need the completed context) — e.g. extract the `ShipRegistry` as an
independently-constructed dependency both take. Over-scoped for a P2 that the
container-stays-pure + contract + guard resolution already closes; revisit if the
registry-ownership seam is refactored for other reasons.

### This is below the seam — and it is not RL-leaning

The single-player game runs in **one** arena (offset 0, one field, shared physics
scene) — today that arena is implicit, smeared across the statics. `ArenaContext`
just names it. The mechanism sees only `arena.ObstacleField` / `arena.Registry`
and cannot tell game from RL, or one arena from one-of-M.

The seam holds: `ArenaContext` (the *service*) is below-seam, constructed by
`SessionHost.ComposeSession`. The per-arena **offset value** is a composition
parameter on `SessionProfile` — driver-supplied, exactly like
`buildPlayer`/`presentation`/`vfx`. The game leaves it `0`; the RL harness supplies
`offset_K` per arena. "M arenas vs 1" is a count the driver controls via profiles,
identical to "M `GameSession`s vs 1".

## `GamePlane` — freeze to constants (Option W, sharpened by Codex)

`GamePlane` stays a global convention (Option W: not injected — injecting a
stateless static isolates nothing, and it is a coordinate *convention*, closer to
`Mathf` than to a service). But the first draft's "promote to process-init-once"
is **superseded**: since the plane is a compile-time constant, the cleanest move is
to have **no runtime init at all** — no `Configure`/`Reset`/`_configured` latch,
values as `static readonly`/expression-bodied constants. There is then no process
lifecycle to coordinate (dodging the domain-reload-disabled static-reset problem
entirely) and teardown *cannot* invalidate a sibling arena, because there is
nothing to invalidate.

**Ground-truth correction — the convention is `Z`, not `Y`.** `InitScene` and
`TestScene` both serialize `planeAxis: 2` (`PlaneAxis.Z`, the XY plane), origin
zero. The `PlaneAxis.Y` default on `SessionHost` is **dead code** — every shipped
scene overrides it to Z. So the frozen constants are the **Z** branch
(`Normal = forward`, `Forward = up`, `Right = right`,
`PositionConstraint = FreezePositionZ`), origin `Vector3.zero`. Freezing to Y — the
naive reading of the old draft — would have silently rotated the entire coordinate
frame while leaving the Y-using tests green: a clean, catastrophic regression.

Why this is arena-safe: spatial offset is **in-plane**, so all arenas lie on the
*same* 2D plane in 3D; the only genuinely per-arena spatial fact is **where an
arena's content sits** — a world-space in-plane translation applied at
placement/reset via `arena.Place(...)`, *not* a plane reconfiguration. Placement
sites route their point-conversion through the offset-applying `arena.Place`; every
*read* site (`Gunner`/LOS aim at an enemy's plane position, all the editor gizmos)
keeps calling the global `GamePlane`, because it converts an entity position that
**already carries the offset** (the entity is physically at `offset_K + local`).
The below-seam mechanism therefore never learns about arenas.

**PR-A2 is thus a test-reconciliation PR, not a two-line wart deletion:**
1. Freeze canonical `GamePlane` to constants `(Z, origin 0)`; delete
   `Configure`/`Reset`/`_configured`/all getter guards. Fixes the stale-Y-default
   latent bug as a side effect.
2. Extract an immutable **`GamePlaneFrame(axis, origin)`** value type holding the
   generic basis math; define canonical `GamePlane` *as* the frozen
   `GamePlaneFrame(Z, zero)` (single source of truth). Point the axis/origin-
   *parameterized* tests (`GamePlanePlayModeTests` tests `Configure(Y, (10,0,5))` —
   the machinery being deleted) at `GamePlaneFrame` directly.
3. Re-verify the Y-*using behavioral* tests (`PlayModeWorldFixture`,
   `MultiSphereObstacle`, `RespawnEditMode`) on Z — some may bake in Y-plane world
   coords. The Z-using tests (`ObstacleSelection`, `TacticalObservation`,
   `ChaseBenchmark`) already match production; they just drop their `Configure(Z)`.
4. Delete `planeAxis`/`planeOrigin` from `SessionHost` and strip the dead keys from
   the scene YAML.

`GamePlane` behavior is **identical for production** (already Z); the risk lives
entirely in step 3. Still independent of PR-A1 (which does not touch `GamePlane`),
so the two land in either order.

Fully de-staticizing `GamePlane` (~55 sites, incl. pooled projectiles + presentation
that never run headless) is a legitimate purity play but a bad trade here — zero
isolation/throughput benefit, and it doesn't gate multi-arena. Filed as an
**optional non-blocking follow-up**, not part of this work.

## Isolation mechanism — spatial-offset first, `PhysicsScene` deferred

Arenas are laid out on an in-plane grid, each at `offset_K`, sharing one
`PhysicsScene`. Physical isolation is by **distance**: a query at arena K's
position won't reach arena J if `spacing > 2·(bound + overshoot + maxRange)`.

### The leakage hazard and its fix

Isolation-by-distance is an invariant on **ship position**, and in self-play
position excursion is adversarially maximized: there's no arena wall today, `Flee`
actively drives ships outward, and an RL agent will discover running to the
boundary as a tactic. A fleeing agent at the arena edge queries `maxRange` past it;
a stray surviving missile physically crosses the gap and detonates in the neighbor.
This does not crash — it **silently poisons the observation/reward stream**, the
worst RL failure mode.

**Fix: bound the arenas.** Out-of-bounds **episode termination** (not a physical
wall — which adds terrain the MPC must handle — nor a hard clamp — unphysical
velocity discontinuity) bounds excursion, and `spacing` carries a one-tick
overshoot margin (`maxSpeed·dt`) so the frame between "crossed" and "episode ends"
can't leak. Bounded combat volume is also a *feature* (forces engagement,
AlphaDogfight-style). The bound lands with the episode/reset layer (roadmap PR-2),
**with a flee penalty** so "leave the map" isn't a safe-reset exploit.

### Physics facade is deferred to PR-C (not phase 1)

Routing the 9 `Physics.*` sites through an `arena.Overlap*` facade is *only* needed
to make the per-arena `PhysicsScene` swap cheap. With bounded spatial-offset, phase
1 doesn't need it: the below-seam code keeps calling the global `Physics.*` (a Unity
*engine* API, not our shared state — safe under bounds+spacing). Per "prefer zero
new wiring until a seam earns its keep," the facade + 9-site routing move into PR-C,
where per-arena `PhysicsScene` makes them load-bearing.

### `PhysicsScene`-per-arena (PR-C, if it bites)

Isolation by construction (no spacing invariant, shared origin → no float32
precision ceiling), plus independent per-arena reset/stepping that async episode
boundaries want anyway. Cost: N separate `Simulate()` calls (fixed per-call
overhead + reduced solver batching — material only at high N), `autoSimulation`
off, and `MoveGameObjectToScene` on every spawn. Introduce **only** if a throughput
benchmark or a leak signal demands it; the facade makes it a near one-file swap.

## N-arena stepping model

An arena is the unit: **one session-root = `SessionHost` + `UnitService` +
`ObjectiveService` + `NavFieldService`** (the component siblings, per the landed
`RequireComponent` cluster), composing its own `GameSession` with its own offset +
providers. `ArenaContext` is the plain-class handle those compose into — not itself a
component. M arenas = M roots.

The step **clock is ML-Agents' Academy**, not a bespoke loop — it already ticks
every Agent per `EnvironmentStep` and batches inference across all of them (Agents
live on the ships across all arenas). The per-arena "driver" role collapses to a
thin **reset-only `RLDriver`** (sibling to its host, like `GameDriver`): on episode
end it atomically resets both competitors via `UnloadSector`/`LoadSector`. The
landed host seam stays untouched (host is externally driven, as designed).

> The roadmap **PR-2** headless episode runner (pre-ML-Agents) is the interim
> stepper that exercises the substrate before the Academy exists; the
> host + reset-driver structure is stable across both.

## PR-B — resolved design (recon + grill 2026-07-12)

Four parallel recon sweeps (placement sites, parenting/pool hazards, origin-anchored
reads, 2-arena composition path) invalidated parts of the sketch above; the grill
resolved each fork. This section supersedes the PR-B bullet's original wording.

**Recon ground truth.** The "~5 placement sites" is really ~13 sites across 5
chokepoints, and they bifurcate cleanly: sites reading a *sector-child transform*
(`SingleSpawner`, `RingSpawner`, asteroid-field origin, `PlayerStart` marker) inherit
a root translation for free, while sites converting *authored plane-space constants*
(the `KeyPickupEncounter`/`ExtractionEncounter` spawns,
`SessionRig.playerSpawnPosition`, revive resolution) need explicit offset conversion.
`ChaseBenchmarkModule`'s field-offset sweep is a working precedent for in-plane
translation. NavField/MPC/asteroid-field *reads* are confirmed arena-invariant
(fields are target/self-anchored, cost math is difference-based) — the read-site
claim above holds.

**Decisions:**

1. **Hybrid offset application.** The session-root GameObject (already
   `SessionHost`+`UnitService`+`ObjectiveService`+`NavFieldService`) doubles as the
   **arena root**, positioned at `GamePlane.PlaneDirToWorld(arena.Offset)`. The
   sector, adopted/spawned ships, and `WorldRoot` parent under it — the
   detach-to-null sites (`SessionHost.LoadSector`, `UnitService.AdoptShip`,
   `EnvironmentService.AdoptWorld`) retarget to the arena root. Authored sector
   content then inherits the offset by hierarchy (future content can't forget it);
   `arena.Place(planePoint) = PlanePointToWorld(planePoint + Offset)` exists **only**
   for the plane-constant sites: the two encounter spawns, the player initial spawn,
   and revive resolution. *(Build outcome:)* `HomeToStableScene` was **deleted
   outright** — `SetParent` across scenes already moves the child into the
   parent's scene, so adopting the sector into the arena root lands it in the
   root's persistent scene (never the swappable locale) with nothing to move.
   *(Build outcome, Codex P1:)* the encounter bundle parents under its **module's
   transform — inside the sector subtree — never the arena root**: hierarchy edge =
   ownership/lifetime (the Objectives rethink's bundle convention), so
   `TeardownSession`'s sector destroy (runTeardown:false) takes the running
   encounter and its spawned key/zone with it instead of leaking them on the
   (DDOL-in-production) arena root. With that, nothing outside composition needs
   the root as a parent target, and `ArenaContext` exposes **no `Root` transform**.
2. **PR-B0 prep PR — kill `transform.root` identity first.** Parenting ships under a
   shared root breaks the self-hit filters (`ProjectileBase`, `Railguns` compare
   `transform.root`; `LockOnSensor` passes `TargetPoint.root` to `LineOfSight`'s
   IsChildOf transparency check — under a shared root, projectiles treat every
   same-arena ship as "self" and LOS reads clear through same-arena asteroids).
   Root fix (user-directed), two halves: the **self side is pure injection** —
   `IShooter` (implemented by `Ship`, already injected into every projectile via
   `Initialize`) gains the identity anchor `Rigidbody Body { get; }` (Ship's
   Awake-cached rb), so projectiles/railguns compare against injected identity
   instead of re-deriving it from hierarchy. The **hit side cannot be injected**
   (physics callbacks hand a raw `Collider`) — collider→entity resolution uses the
   engine's own map, `hit.attachedRigidbody == Shooter.Body`
   (hierarchy-above-agnostic, null for static colliders → correct no-skip). A
   collider-keyed ship registry is the fully-injected endgame (old board card) but
   is wrong wiring for pooled projectiles — pools are process-global and
   arena-blind, so they must not hold arena-scoped references. `LineOfSight` gets
   the target ship's own transform (via `ITargetable`), not `.root`.
   Behavior-identical under today's flat hierarchy; lands with a parented-ship
   combat regression test; makes parenting safe permanently.
3. **Latent origin bugs riding in PR-B** (all inside touched placement code):
   `KeyPickup.SpawnKey` scatters on world X/Z but the frozen plane is X/Y — fix the
   basis; player FixedPoint revive resolves raw `policy.point` with no producer base
   — route through `Place`; `Respawn.FollowerAnchor` + `ObserverCam` `Vector2.zero`
   fallbacks — route through `Place`. **Deferred (board):** `Gunner.HasTarget =>
   Target != Vector3.zero` origin sentinel (only arena-0-at-origin can trip it;
   pre-existing; AI code PR-B doesn't otherwise touch).
4. **Smoke = two rig-less `SessionHost` roots** (`playerRig` unassigned,
   presentation off — sidesteps the rig's unconditional WorldRoot/observer-cam and
   the process-global `GameSettings` flags), each loading a real sector (field + AI
   ships) at grid offsets. Asserts: per-arena provider reads, no cross-arena damage,
   and (stretch) **mirror-determinism** — with `arenaBaseSeed` still 0, identically
   seeded arenas must evolve identically iff isolated; tolerance-based, since float
   math isn't translation-invariant. *(Build outcome:)* mirror-determinism was
   implemented, run, and **dropped** — a twin diverged 39 plane units in 4 sim-
   seconds at offset 1000 (float non-invariance + unordered physics-query results
   feeding a chaotic closed-loop controller; global-`Random` consumers also
   interleave across arenas). No stable threshold separates drift from leakage
   over multi-second horizons — **do not re-attempt it as a gate in PR-2/PR-4**;
   the smoke asserts deterministic leak-specific invariants instead (pairwise
   placement offset, per-arena provider isolation, ships confined to their own
   arena through live combat, cross-arena field queries empty). Lives in
   `Tests.PlayMode` (not `Game.RLHarness.Editor` as first sketched — that asm has
   no test-framework references; #132's own tests live in `Tests.PlayMode` too).
5. **Per-arena seeds stay deferred** (S1b hook, consumer is PR-4 self-play) —
   identical streams are what the mirror smoke *wants*.
6. **Spacing: document + defer.** Formula recorded, not enforced: min spacing ≈
   `2·(fieldRadius + maxSensorOrWeaponReach + margin)` ≈ **920** for the standard
   400-radius field (~20k for BigField; float32 comfortable at standard scale, prefer
   a square grid over a line for big fields at high N). The runtime arena *bound*
   doesn't exist until roadmap PR-2, so there's nothing to enforce against yet; the
   smoke hand-picks a safe spacing. **Deferred (board): arena-size dependency pass**
   — inventory what actually depends on arena size before building the bound/layout
   setup.
7. **Known limitation recorded:** `GameSettings.PresentationEnabled`/`VfxEnabled`
   are process-global (written per-compose) — mixed-presentation multi-arena is
   unsupported until that's revisited; benign while all arenas share flags.
8. **`SessionRig` cleave: deferred with evidence.** The rig-less smoke proves PR-B
   needs no rig changes; the cleave becomes live only when RL composes arenas
   *through* rigs (PR-2b/PR-4). The `worldPrefab`-move and `PlayerRig` rename riders
   stay optional.

## PR sequence

**Rethink-proof — behavior-identical in single-arena, land first, safe in parallel
with the in-flight single-arena RL interface work:**

- **PR-A1 · `ArenaContext` + provider injection.** New plain-class handle in the
  `GameServices` bundle + a `NavFieldService` component sibling on the session root;
  injected per-ship via `WireShipDependencies` (`SetArena`, passed into
  `Navigator.Initialize`). Hard-cut the statics: delete `ObstacleFields` and
  `NavFieldService.Instance`/lazy-create; `ObstacleFields.Active` →
  `arena.ObstacleField` (read per-frame), `NavFieldService.Instance` →
  `arena.NavField`; `Targeting` gets `arena.Registry`. `AsteroidFieldSpawner`
  registers into `services.Arena`. `Vector2 offset` lands on `SessionProfile` +
  `ArenaContext` (zero, unused until PR-B). Proof: existing AI/MPC/Scanner/NavField
  suites stay green (rewired to inject via a small `ArenaContext` test fixture) +
  one new two-arena isolation test (a ship wired to arena A sees A's field, not B's).
- **PR-A2 · `GamePlane` → frozen constants + `GamePlaneFrame`.** Freeze canonical
  `GamePlane` to `(Z, origin 0)`; delete `Configure`/`Reset`/latch; extract
  `GamePlaneFrame` for the parameterized-math tests; reconcile the Y/Z test split;
  drop the dead serialized fields + scene keys. Behavior-identical for production
  (already Z). Independent of PR-A1.

**Mechanism-specific — first actual N>1:**

- **PR-B0 · Entity-identity prep.** Replace `transform.root` self-hit/LOS identity
  with `attachedRigidbody`/ship-transform identity (3 sites) + parented-ship combat
  regression test. Behavior-identical; unblocks arena-root parenting.
- **PR-B · Spatial-offset bring-up.** Session root = arena root at the offset;
  detach sites retarget to it; `arena.Place(...)` at the plane-constant sites only;
  latent origin bugs fixed in passing; 2-arena rig-less smoke with mirror-determinism
  check. Keeps global `Physics.*`. Full resolved design in the PR-B section above.

**Deferred / separate track:**

- **PR-C · Per-arena `PhysicsScene`.** Introduces the physics facade, routes the 9
  sites, `MoveGameObjectToScene` on spawn, manual `Simulate`. Only if
  throughput/isolation demands it.
- **Arena bound + episode/reset/reward** = roadmap **PR-2** (not substrate); the
  bound + flee penalty live there.
- **`SessionRig` infra/player cleave.** The seam doc left the rig one object —
  session infra (world/cameras/UI) + the player unit share a component with a
  documented-but-uncleaved internal seam (`Session_Flow_Driver_Seam.md` →
  "`SessionRig`"). **PR-B is the decision point**: per-arena composition forces
  separate per-arena-vs-per-process answers for the world (`WorldRoot` today is
  one instance spawned by the rig), cameras/UI (RL arenas run presentation-off),
  and the player unit (RL builds agents, not "the player"). Cleave
  `SessionInfra`/`PlayerUnit` only if those answers diverge — don't split ahead
  of the evidence. **Resolved at the PR-B grill (2026-07-12): deferred** — the
  rig-less smoke needs no rig changes; the decision point moves to whenever RL
  composes arenas through rigs (PR-2b/PR-4). Two smaller items can ride any earlier PR touching this code:
  `worldPrefab` arguably belongs beside `GamePlane` on `SessionHost` (both are
  "universal world setup" — the same argument that moved `GamePlane` down), and
  the `PlayerRig.prefab` / `SessionHost.playerRig` naming residue is a
  rename-only cleanup.

**Scope claim:** PR-A1/A2 are pure static→injection refactors — no single-arena
behavior change, no dependence on the isolation mechanism — so they land
immediately and in parallel with the velocity-reference RL work (#122). **None of
this blocks the *first* learned agent** (roadmap PR-1/2/3 are single-arena);
multi-arena is purely the self-play throughput unlock (PR-4).

## Related

- `doc/Feature_Plans/Session_Flow_Driver_Seam.md` (the landed seam this builds on)
- `doc/Feature_Plans/Tactical_AI_Audit_And_Roadmap.md` §4′ (the RL PR sequence)
- memory `project_multi_arena_rethink`, `feedback_dependency_wiring_philosophy`
