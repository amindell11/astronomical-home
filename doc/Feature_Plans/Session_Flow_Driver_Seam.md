# Session-Flow Driver Seam (PlayerRig de-accretion + game/RL boundary)

*Draft • 2026-07-11 • status: design agreed (grill session), implementation not started*

> Successor to `Bootstrap_Session_Decoupling.md`. That doc split
> `MainGameManager` into *lifecycle primitives* and an *interactive gameplay
> driver* — but left the split as a **comment inside one class**, and let
> `PlayerRig` accrete the between-run hangar flow and death policy. This doc
> turns the seam into a **real type boundary** and de-accretes the rig.

## Motivation

Two problems, one root:

1. **`PlayerRig` is an accretion sink.** Named "session-tier rig" (build/hold
   world+cameras+UI+player), it has absorbed the whole hangar flow
   (`RunHangar`, input gating, HUD show/hide), the loadout apply/rebuild, and
   the death policy (`PlayerDeathBehavior` enum + `RestartRequested`). Every
   between-run feature gravitates into it because it is the only session-scoped
   seam.
2. **The session startup is biased toward "game."** "Headless/RL" is not a peer
   of "game" — it is *the game path with bools turned off* (`buildPlayer=false`,
   `installPresentation=false`) plus an `if (!PresentationEnabled) ApplyLoadout()`
   early-out buried inside interactive code. The game driver and the universal
   primitives are welded into one `MainGameManager` class; RL would have to reach
   *into* it rather than replace a part of it.

**The design test (user's words):** *"a clear seam where you unplug `game` and
plug in `RL sim`, and everything below that just works."*

## The two axes (do not conflate them)

The decoupling doc already found the key: the game-vs-RL difference is **not
mainly about what you build**. There are two independent axes:

1. **The driver seam** — *who owns the clock and the reset*. Game: frame-loop
   coroutine + death→restart + sector-complete→restart. RL: external/timescaled
   step + terminal-condition→deterministic-reset. **This is the type boundary.**
2. **The "what exists / what gets simulated" axis** — `buildPlayer`,
   `presentation`, `vfx`. Orthogonal to the driver (you may watch an RL run
   *with* presentation, or a spectator game *with* no player). These are
   **composition parameters the active driver supplies**, not driver-private
   state and not host defaults.

## Target architecture

One session-root GameObject; two peer components; dependency points **strictly
upward** (driver → host), never down.

```
Session-root GameObject (DontDestroyOnLoad)
├─ UnitService, ObjectiveService     (RequireComponent siblings — below seam)
├─ SessionHost   ── BELOW SEAM (universal, "just works" for any driver) ───────
│    implements ISessionPrimitives:
│      ComposeSession(profile) · LoadSector() · UnloadSector() ·
│      TeardownSession() · ApplyLoadout()
│    owns: GameSession, SessionRig ref, GamePlane config
│    consumes: a SessionProfile handed in at compose
│    reads GameSession.OnSectorComplete / OnPlayerDeath to inject them
│    DOES NOT know the driver exists
└─ GameDriver    ── ABOVE SEAM (pluggable) ────────────────────────────────────
     owns: coroutine state machine (GameState), frame pacing,
           hangar SCREEN prefab, loadout CATALOG, LoadingSplash,
           deathBehavior enum + RespawnPolicy asset
     builds the SessionProfile + the two policy callbacks, sets them on
       GameSession, then drives the host primitives
     caches the SessionHost sibling in Awake
     ↑ swap this one component for RLDriver later — everything below unchanged
```

`MainGameManager` retires into `SessionHost` + `GameDriver`.

### The seam contract — `ISessionPrimitives`

Driver-agnostic operations the host exposes; the driver depends only on this:

- `ComposeSession(SessionProfile profile)` — services + rig build + presentation
  policy. Creates the player-or-not per the profile. Configures `GamePlane`
  (moved **down** from the game driver — it is universal world setup).
- `LoadSector()` — instantiate + wire the sector; inject
  `GameSession.OnSectorComplete`; reset the persistent player to `PlayerStart`.
- `UnloadSector()` — cancel pending revives; run sector teardown; destroy content.
- `TeardownSession()` — drop sector, tear down rig, wipe registries.
- `ApplyLoadout()` — forwards to `SessionRig.ApplyLoadout()` (install the pending
  loadout onto the player; rebuild if chassis changed; reequip; rewire; rebind
  HUD). Universal: game calls it after the hangar screen; RL calls it silently.

### `SessionProfile` (driver-supplied composition input)

Small serializable value the driver builds and hands to `ComposeSession`:

- `sectorEntry` — which sector to load (game: campaign; RL: training arena).
- `buildPlayer` — false = spectator (game-only case; RL always builds its agent).
- `presentation` — ship visual rigs + **HUD/UI-cam/minimap** on/off.
- `vfx` — the not-yet-rig-migrated explosion effects on/off.

The host consumes it and holds **no** mode opinion. The **loadout catalog** (the
choices the hangar offers) is *not* in the profile — it is `GameDriver`-only,
because RL has no menu. Only the *what-exists* knobs are shared parameters.

> Inline serialized value for now. `Bootstrap_Session_Decoupling.md` left
> SO-vs-struct open; revisit only if arena instances need to share one profile.

**Single sector today.** The game is single-sector: one `SectorEntry`, replayed
on death/completion (`HandleSectorComplete` ignores the `SectorResult` and just
restarts the same sector — no sequencing/campaign/progression exists). The
profile therefore holds one `sectorEntry`. This is the natural home for future
multi-sector work: a campaign or RL curriculum lands as a sector *sequence* in
the profile (or a sector *provider* it points at), with the driver deciding how
to advance it (game: next level; RL: next-episode sector) and consuming the
now-unused `SectorResult`. Keeping it singular now makes that an additive change,
not a retrofit — do not generalize speculatively.

### `GameSession` — session state **and** the driver's policy-hook surface

POCO container, owned by `SessionHost`, one per arena:

- `Services`, `Rig`, `ActiveSector` (as today).
- `OnSectorComplete` (field, as today) — driver-supplied; injected by `LoadSector`.
- `OnPlayerDeath` (**new** field) — driver-supplied; injected onto the player.

`GameSession` is deliberately the **one place an RL author looks** to see every
policy seam a driver must plug. If these hooks multiply (episode reset, reward,
…), group them into a nested `SessionPolicy` object — *later*, not now.

### Reset policy: one pattern for both triggers

Both reset triggers are **driver-supplied behaviors the host injects into
mechanism-only objects** — death is unified with the sector-complete path that
already works:

```
Driver supplies behavior   Host injects it where needed              Mechanism (policy-free)
────────────────────────   ─────────────────────────────────────    ───────────────────────
OnSectorComplete        →  LoadSector → sector.OnSectorComplete   →  Sector
OnPlayerDeath           →  ComposeSession/Build →                     SessionRig / Ship
                             player.Damage.OnDeath += onPlayerDeath
```

- `GameDriver` owns the whole death **policy**: it serializes `deathBehavior`
  (`None`/`RespawnInPlace`/`RestartSector`) + `RespawnPolicy`, and *builds the
  callback* — `RestartSector` → `() => RequestRestart()`; `RespawnInPlace` → a
  closure that revives via the services it got from the host; `None` → null.
- The host reads `GameSession.OnPlayerDeath` and passes it into `SessionRig.Build`,
  which wires `player.Damage.OnDeath += onPlayerDeath` **synchronously at spawn**.
  That kills the spawn-frame-death timing problem the current `RestartRequested`
  event exists to solve — no event, no pre-subscribe dance.
- `SessionRig` **stores** the injected callback and re-applies it to any player
  it (re)builds, so `RebuildPlayer` re-wires for free. The rig holds **zero**
  death policy — no enum, no `RespawnPolicy`, no `RestartRequested`. Pure
  mechanism (the Initialize-param wiring idiom).
- The rig never back-references `GameSession`; it only receives the callback via
  its `Build` parameter and stores it.

### `SessionRig` (renamed from `PlayerRig`)

Below-seam builder/holder. **Keeps** (all universal construction): `Build`,
`Teardown`, `Player`, `Loadout` (persistent session state — the player's chosen
kit, survives restarts), `ApplyLoadout`, `RebuildPlayer`, `RebindHud`, and the
stored `onPlayerDeath` callback. **Sheds** (all → `GameDriver`): `RunHangar`,
`SetPlayerInputEnabled` (hangar input gate), `PlayerDeathBehavior` enum,
`WireDeathPolicy`, `RestartRequested`.

One object for now. Its internal **infra (world/cameras/UI) vs player** seam is
documented but *not* cleaved — a later pass can split `SessionInfra` +
`PlayerUnit` if the player-construction cluster grows (equipment/module system,
player-identity consolidation). The wiring philosophy prefers zero new seams
until one earns its keep. The expected decision point is the multi-arena
substrate's PR-B (`Multi_Arena_Substrate.md` → PR sequence), where world,
cameras/UI, and player get per-arena-vs-per-process answers that may diverge.

### The between-run hangar flow, split

- **Interactive shell → `GameDriver`:** show the hangar screen, gate player input
  (Fire1 shares mouse-0 with UI), hide/restore the HUD, `WaitUntil` launch. RL
  never sees this.
- **Universal core → `SessionRig.ApplyLoadout` (host primitive):** install the
  loadout, rebuild-if-chassis-changed, reequip, rewire, rebind HUD. Both drivers
  call it. This is exactly what the buried `if (!PresentationEnabled)
  ApplyLoadout()` early-out was trying to express — now a structural boundary,
  not a runtime branch.

The `GameDriver` reads `host.Session.Rig.Loadout`, hands it to the hangar screen
(edited in place), then calls `host.ApplyLoadout()`. `RLDriver` writes
`rig.Loadout` directly, then calls the same `host.ApplyLoadout()`.

## Game-biased placements corrected by this design

- `GamePlane.Configure` — moves **down** into `ComposeSession` (universal).
- `LoadingSplash`, hangar screen, loadout **catalog**, `deathBehavior` — move
  **up** into `GameDriver` (interactive/game-only).
- HUD overlay / UI-cam / minimap build — gated on `profile.presentation` (today
  they build unconditionally; an RL headless run should build no Canvas).
- `buildPlayer`/`presentation`/`vfx` — become `SessionProfile` params supplied by
  the driver, not host serialized fields with game defaults.

## Sequencing — rig-first, 3 behavior-preserving PRs

Each PR keeps behavior identical and is guarded by the existing test net
(`BootstrapContractsEditModeTests`; `HangarFlow`/`HangarInputGate`/
`HangarShipSwap`/`ShipReequip`/`SectorLifecycle` PlayMode).

**PR1 — de-accrete the rig.** Rename `PlayerRig` → `SessionRig`. Move `RunHangar`
+ input-gate + death policy **up** into `MainGameManager` (still the monolith —
it *is* the game driver). Introduce `onPlayerDeath` injection: `SessionRig.Build`
takes it, stores it, wires at spawn/rebuild; `MainGameManager` builds the callback
from its now-owned `deathBehavior` enum. Add `GameSession.OnPlayerDeath`. Gate the
HUD/UI build on presentation. *Directly fixes the named "PlayerRig is legacy"
pain.*

**PR2 — `SessionProfile` + tier corrections.** Introduce the `SessionProfile`;
push `buildPlayer`/`presentation`/`vfx`/`sectorEntry` into it; `ComposeSession`
consumes it; move `GamePlane.Configure` down into `ComposeSession`. Prep for the
split (composition now takes a profile).

**PR3 — cleave the seam.** Extract `ISessionPrimitives` + `SessionHost` (owns
`GameSession`; the primitives forward/build) from `GameDriver` (coroutine SM +
hangar + death policy + profile-building) into two sibling components on the root.
Ship a **seam proof-of-life test**: a tiny test driver that runs
`Compose → Load → ApplyLoadout → reset → Teardown` *without* the game state
machine or hangar screen. That test is what proves "unplug game" holds and is the
skeleton an `RLDriver` later fills in. `RLDriver` itself is **not** built here.

## Scope boundary (multi-arena)

Multi-arena-*friendly*, not multi-arena-*complete*: per-session `GameSession`,
per-arena `SessionProfile`, **no new process statics**. This pass does **not**
build N-arenas or unwind the existing interim statics (`GamePlane`,
`GameSettings.PresentationEnabled`, `ObstacleFields`/`NavFieldService`) — those
stay flagged interim and remain the real N-arena blockers under
`project_multi_arena_rethink`. The driver seam is the rethink-proof piece, so it
is safe to build now.

## Touch points

- `Game/Bootstrap/MainGameManager.cs` → splits into `SessionHost` + `GameDriver`.
- `Game/Bootstrap/GameSession.cs` — add `OnPlayerDeath`; (later) `SessionPolicy`.
- `Game/Bootstrap/GameState.cs` — moves with the state machine into `GameDriver`.
- `Player/PlayerRig.cs` (+ `.Editor.cs`) → `SessionRig`; sheds flow + policy.
- `Game/Sectors/Utils/SectorUtils.cs` — `BuildAndWirePlayer` unchanged (rig uses it).
- `UI/HangarScreen.cs` — unchanged; its driver moves to `GameDriver`.
- New: `SessionProfile`, `ISessionPrimitives`, seam proof-of-life test.
- Tests retarget: BootstrapContracts → host primitives; Hangar* → GameDriver;
  Reequip → `SessionRig.ApplyLoadout`; SectorLifecycle → host primitives.
