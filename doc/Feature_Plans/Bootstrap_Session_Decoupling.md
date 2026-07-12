# Bootstrap / Session Decoupling (RL prep)

*Draft • 2026-07-02 • status: proposal, awaiting review*

> Supersedes the bootstrap-related assumptions in `RL_Implementation_Plan.md`
> (v0.2, June 2025 — that doc predates the MPC/utility AI, the Sector collapse,
> and the current `MainGameManager`/`PlayerRig` split, so its §2/§5/§9 no longer
> describe this codebase). This doc only covers the **session/bootstrap seam**;
> ML-Agents wiring, observations, and rewards are out of scope here.

## Motivation

`MainGameManager` is the session-tier orchestrator. In preparation for an RL /
training-arena runtime, we want a clean separation between **what's unique to
interactive gameplay startup** and **what a headless arena / RL episode loop
needs** — without forking into a `RLGameManager` subclass (that fights the
composition-over-subclassing idiom we just adopted in the Sector collapse).

## What `MainGameManager` conflates today

`src/Asteroids3D/Assets/Scripts/Game/Bootstrap/MainGameManager.cs` welds three
separable jobs into one coroutine state machine
(`Loading → Start → LoadSector → InSector → Restart → Exit`):

1. **Session composition** — *what exists*. Services (`HandleLoading`), the
   player/camera/UI rig (`HandleStart` → `PlayerRig.Build`), presentation
   (`PresentationInstaller`). Already partly cracked open via loose serialized
   policy: `buildPlayer`, `installPresentation`, injectable `playerRig`.
2. **Lifecycle primitives** — *build / load / reset / teardown*.
   `HandleLoading`, `HandleStart`, `HandleLoadSector`, `TeardownActiveSector`,
   `ExitRoutine`. Universal — gameplay, arena, and RL all need these.
3. **Control policy** — *who advances phases and when*. Unity `Start()` kicks
   the coroutine; `HandleSectorComplete` and player-death
   (`PlayerRig.PlayerDeathBehavior.RestartSector`) hardwire the restart
   trigger; coroutines pace it against the frame loop.

## Key insight

The gameplay-vs-RL difference is **not mainly about what you build** — the
existing flags already produce a headless session (`buildPlayer=false`,
`installPresentation=false`). The real divergence is **who owns the clock and
the reset**:

| | Interactive gameplay | RL / training arena |
|---|---|---|
| Driver | frame-loop coroutine | external step / `FixedUpdate`, time-scaled |
| Reset trigger | in-game events (sector complete, player death) | reward-terminal condition |
| Re-seed | incidental | deterministic per episode |
| Instances | one | possibly N parallel |
| Presentation | full rig | none |

So the valuable refactor splits **control (#3) away from lifecycle (#2)** and
consolidates **composition (#1)** into one object. This keeps a single concrete
`MainGameManager` configured by data — mirroring the Sector-as-prefab-of-one-class
pattern.

## Proposed decomposition (value order)

### A. Injection / Awake hygiene for sibling services *(smallest, no behavior change)*

> Reframed. The earlier idea — fold `buildPlayer`/`installPresentation`/`playerRig`
> into one `SessionProfile` object — is rejected: it conflates **policy** (the
> bools) with an **injected dependency** (`playerRig` is a serialized reference,
> i.e. already injection). Don't bundle a concrete object reference into a config
> object. The policy bools may be *lightly* grouped later, but if so the group
> holds policy only, never the rig — and that's optional, not its own PR.

The salvageable, worthwhile cleanup here is the injection consistency fix:
`HandleLoading` currently calls `GetComponent<UnitService>()` /
`GetComponent<ObjectiveService>()` **mid-coroutine at runtime**, violating the
project's "GetComponent only in Awake" rule.

- `UnitService` / `ObjectiveService` are `MonoBehaviour` siblings
  (`[RequireComponent]`), so they can't be constructor-injected — cache them in
  `Awake` (or serialized refs) and have `HandleLoading` read the cached fields.
- The other three services (`EnvironmentService`, `CameraService`, `UIService`)
  are POCOs `new`'d inline — leave as-is.
- Future headless knobs (timeScale, deterministic seed, auto-start) stay as
  plain serialized fields unless/until grouping earns its keep.

### B. Extract driver-agnostic lifecycle primitives *(the actual RL enabler)*

Pull the build steps out of the coroutine into idempotent methods:

- `ComposeSession()` — services + rig + presentation.
- `LoadSector()` — current `HandleLoadSector` body.
- `ResetSector()` — teardown + reload (current `HandleRestart` body,
  minus the trigger).
- `TeardownSession()` — current `ExitRoutine` body.

The existing coroutine state machine becomes a thin **interactive gameplay
driver** over these primitives. A later RL harness drives the *same* primitives
from `FixedUpdate` / an external step, without inheriting restart-on-complete.

**Per-instance shaping (decided).** Single-process, multi-arena is the primary
RL scaling axis — it amortizes engine/asset/comms overhead, shares one physics
tick across all arenas, and batches inference. So even though single-arena
training works now, the primitives are shaped **per-instance**: they take/return
an explicit session/services container instead of assuming the process-singleton
`UnitService`/`ObjectiveService`. This keeps single-arena working today and makes
in-process tiling an additive change, not a signature-breaking retrofit.

- `GameServices` and its two `MonoBehaviour` services (`UnitService`,
  `ObjectiveService`) become an owned-per-session unit rather than one
  process-wide singleton read off the manager's own GameObject.
- **Physics isolation is a follow-on, not part of B.** In-process arenas share
  one physics `Scene` by default and would collide; isolating them (spatial
  tiling or a `PhysicsScene` per arena) is deferred. B only makes the *ownership*
  per-instance so that later work has a seam to attach to.

### C. Make the reset trigger a policy seam *(small, once B exists)*

Route "episode/sector ended" through one hook. Gameplay wires it to
`Sector.OnSectorComplete` + player death (as today); RL wires it to its terminal
condition plus a deterministic re-seed. Removes the hardwired
`HandleSectorComplete → Restart` and `PlayerRig` death→restart coupling from the
universal path.

## Sequencing

1. **PR A** — Awake-cache the sibling MonoBehaviour services; `HandleLoading`
   reads cached fields instead of `GetComponent`. No behavior change. Optional
   companion cleanup, not a `SessionProfile` object.
2. **PR B** — extract lifecycle primitives; coroutine reduced to gameplay
   driver. Behavior-preserving; character tests over `MainGameManager` guard it.
3. **PR C** — reset-trigger seam. Enables an RL driver to reuse the primitives.

A is independent of B/C and can land anytime (or fold into B). B and C are the
substance.

RL-specific work (ML-Agents Agent, observations, rewards, arena scene) sits on
top of B/C and is tracked separately.

## Open questions

- If the composition policy bools ever get grouped (optional), does the group
  live as a `ScriptableObject` (shareable across arena instances) or a serialized
  inline struct? Not blocking A/B/C either way — and it never holds `playerRig`.
- Where does deterministic seeding belong — a separate concern owned
  by the sector's spawners (cf. untracked `Deterministic_Asteroid_Field.md`)?
- Parallel arenas: **resolved** — B shapes primitives per-instance (see §B).
  Physics isolation for co-located arenas remains a separate follow-on.

## Touch points (for whoever implements)

- `Game/Bootstrap/MainGameManager.cs` — the refactor target.
- `Game/Bootstrap/GameState.cs` — state enum (may stay as-is under A/B).
- `Player/PlayerRig.cs` — composition consumer; death→restart wiring (C).
- `Game/Sectors/Sector.cs` — `OnSectorComplete`, `Initialize`, `Setup`,
  `Teardown`, `PlayerStart` (the primitives call into these).
- Tests: `Editor/Tests/EditMode/BootstrapContractsEditModeTests.cs`,
  `GameContextDecouplingEditModeTests.cs`,
  `Editor/Tests/PlayMode/SectorLifecyclePlayModeTests.cs`.
