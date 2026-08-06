# Objectives / Encounters / Sector Rethink

> STATUS: living — objectives/encounters design reference; the #134–#153 arc is COMPLETE.
> The next objectives PR opens with token-authoring design (issue #334); converge superseded sections then.

*Draft • 2026-07-12 • status: PR-1..PR-4b (#134/#135/#141/#147/#153) all merged; PR-5 signal-port refactor was built then SHELVED as #156 — its decision brief is retained below as the retry's seed (see "PR-5 — decision brief")*

> Realizes the deferred `project_objectives_encounters_rethink`. The presenting
> symptom was "combat encounters spawn one at a time instead of together," but the
> root cause is topological: the code models a sector as a **linear queue of
> full-screen missions** (`EncounterSequenceModule`), while the design
> (`Obsidian: Design/World/{Encounters,Sectors}`) wants a **spatial field of
> concurrent, trigger-gated encounters**. This doc decides the primitive that
> replaces the queue and the objective model that unblocks concurrency.

## The problem

A `Sector` (`Game/Sectors/Sector.cs`) builds content in `Setup()` — adopt +
spawners up front, then `modules[]` in list order — and today's encounter
sequencing is one such module:

- **`EncounterSequenceModule`** (`Game/Sectors/Elements/EncounterSequenceModule.cs`)
  is a strict linear queue: one live `_activeEncounter`, torn down and `Destroy`d
  before the next is instantiated. Wrong topology — the design is a graph of
  concurrent encounters at different places, not a single-file line.
- **`IObjectiveService`** (`Game/Services/Objectives/IObjectiveService.cs`) is
  **single-tenant**: one `CurrentTracker` / `CurrentState` / `CurrentTarget`. This
  is *why* the module must be serial — two live encounters would clobber the one
  objective channel.
- **Spatial detection is smeared across three layers.** The trigger colliders live
  on the *entity* (`KeyPickup`/`ExtractionZone` own colliders + `OnTriggerEnter`);
  the *meaning* ("done when in zone") lives in the objective *state*
  (`ExploreState`/`ExtractionChallengeState`, which only poll a boolean); and
  *sequencing* lives in the module. "When does this encounter turn on" is owned by
  no one — the exact three-way tangle.

The three systems are tightly coupled because a concept is **missing**: there is no
representation of *activation* ("this content turns on when …"). The only two ways
a thing comes alive today are *spawn-at-build* (adopt/spawners) and
*next-in-queue* (the serial module's hardcoded "when previous completes").

## Design intent (from the vault)

`Design/World/Sectors.md` + `Encounters.md` + `Gameplay/Rogue-like.md`: a sector is
a **bounded open-space overworld**, a field of POIs. Encounters are objective-first,
**coexist** at different places, some marked and some fire "organically as you pass
through an area." They **chain by events/leads** — scan → lead; key → extraction
arms; escort → wingman. The sector spine is `acquire Key → Extract`, possibly gated
by an Extraction Challenge. Some world objects (the extraction gate) are **present
and flyable-to from sector spawn**, even before the player has the objective.

## Core design

### The primitive: an activation rule `predicate(terms) → effect`

The minimal reusable unit is **not** "an encounter" — it is an **activation rule**:

- **Terms**, AND-ed together, of four flavors:
  - **state** — mission stage / has-item / flag (polled or latched)
  - **spatial** — *in* a volume / within a proximity, read as a **level**
    ("are you in it"), **not** an enter-edge
  - **event** — a named bus event has fired (latched boolean)
  - **time** — elapsed since armed / since sector start
- Evaluated as a **standing predicate**, re-checked whenever any term changes, and
  **latched to Active on first satisfaction** — so it fires exactly once and does
  not re-arm when the player leaves and re-enters.
- **Effect** — thin (start a challenge) or fat (spawn content + install a local
  objective + publish on-complete events).

**Why standing-and-latched, not an edge.** `ExtractionEncounter.OnSetup` currently
carries a `yield return null` whose comment is *"give physics a frame so
OnTriggerEnter fires even if the player is already overlapping the zone at spawn."*
That hack exists **only** because activation is coded as an enter-edge, and the
player can be parked in the extraction zone *before* qualifying (they pick up the
key while sitting in the gate). A standing predicate (`stage==ready AND in-zone`)
handles parked-then-qualified for free and the frame-delay hack is deleted.

### An encounter is the fat version of a rule; a fixture-with-behavior is the thin version

- A full **encounter** = a named bundle `{ activation rule + lazily-spawned content
  + local objective + on-complete events }`.
- The **extraction gate is not an encounter** — it is a **present sector fixture**
  plus a *thin* rule whose effect is "start the extraction challenge." Same
  machinery, thinner effect, no owned content. This is why it "isn't really an
  encounter, just part of the sector."
- "You need a mission item / prior objective to activate this encounter" is just a
  **state term** in that encounter's predicate — no special case.

### Presence ≠ arming ≠ firing

Three separate concerns per world object, currently fused:

1. **Presence** — does it exist in the world? Sector-owned. Spawned at build for
   persistent fixtures (gate, key, derelict) or **lazily on activation** for
   expensive content (enemy waves). *Fix:* the extraction zone must become a sector
   **fixture**, not instantiated in the encounter's `OnSetup`.
2. **Armed** — is the rule's trigger listening yet? Gated by the predicate's
   standing-state terms. Before armed the object is **present-but-inert** — you fly
   through the ring and nothing happens.
3. **Firing → Active** — predicate satisfied → effect runs.

**Encounters bind to anchors; they do not own them.** An encounter references an
existing fixture (the gate) *or* declares content to lazy-spawn; the world object's
existence is independent of the encounter's state.

### The event bus is the coupling seam

A per-sector **event bus** is where the three systems meet without reaching into
each other. A rule's **on-complete event** is another rule's **event/state term**,
so `key picked up → extraction arms` becomes two data rows, not hardcoded serial
logic. Spatial activation and event activation are the *same* bus term from
different **sources** (a `TriggerVolume` vs. an objective completion). The bus is
**per-sector**, which is also **per-arena** — aligning with the arena-scoping
direction in `Multi_Arena_Substrate.md` (no new process-wide static).

### Activation vs completion stay distinct primitives

They only *look* alike (both "player in a volume"):

- **Activation** = a compound standing predicate that arms/fires content. Edge-ish
  in spirit but evaluated as a level to survive already-satisfied terms.
- **Completion** = the objective's own **win predicate**, often stateful/compound
  (`in extraction zone AND no chaser within 20m`), stays on the objective/target
  and is **polled** each tick. **Do not** force completion through the bus — "in
  zone AND unblocked" is not an edge event.

### Two-tier objectives (retire single-tenant)

Replace `IObjectiveService` with two tiers:

- **Sector Objective** — the `key → extract` spine, single, the prominent HUD
  marker. Its **stage is a state term** other rules read (this is how extraction
  gates on "have the key").
- **Encounter Objectives** — local, each owned by its encounter, shown contextually
  when active/nearby. Concurrent — this is what the single-tenant service blocked.

A fully generic multi-slot objective system is **explicitly deferred** — two tiers
match the design's spine/local shape with far less machinery.

## Worked example — the demo (key + extraction), rebuilt

- **Key** — present fixture (`KeyPickup`) at spawn. Encounter: rule `entered-sector`
  (or immediate) → effect spawns nothing extra, installs the local "find the key"
  objective; on-complete publishes `key-acquired` and advances the **sector spine**
  to `ready-to-extract`.
- **Extraction gate** — present fixture (`ExtractionZone`) at spawn, flyable-to,
  **inert**. Thin rule: terms `spine-stage == ready-to-extract` **AND**
  `player-in-gate-volume` → effect starts the extraction challenge (spawn/activate
  chaser, install the extraction local objective). No `OnSetup` instantiation; no
  physics-frame hack; parked-in-gate-then-get-key works.
- Wiring `key-acquired → extraction arms` is a **bus term**, not a serial step.
  `EncounterSequenceModule` is **retired** — serial is just the degenerate wiring
  "B's rule has A's on-complete event as a term."

## Proposed concrete shapes (open to change at build)

Grounded in the existing sector grain (hand-placed children reconciled by
`SectorManifestSync`):

- **`TriggerVolume`** — a placed sector child (collider + `OnTriggerEnter/Exit`
  maintaining an *in/out level*), publishing a named token to the sector bus.
  Generalizes what `KeyPickup`/`ExtractionZone` already do; a spatial **term
  source**, decoupled from any specific objective.
- **Activation rule / encounter** — a placed child carrying a small serialized
  **term list** (flavor + key/target + params) and an **effect** (content
  spawn-spec + local-objective builder + on-complete event tokens). Terms
  reference fixtures / volumes / spine-stage / event tokens by serialized
  reference or token.
- **Two-tier objective service** — spine API (`SetSectorObjective` / stage) +
  local-slot API (`OpenLocal` / `CloseLocal`) replacing the single
  `SetObjective` / `SetTarget`; HUD reads spine prominently + active locals
  contextually.

### Resolved build design (2026-07-12)

Locked with the user during the PR-1 build; supersedes the proposal bullets
above where they differ.

- **Unified boolean signal bus.** `SectorEventBus`
  (`Game/Sectors/Activation/`) is a plain C# class carrying named boolean
  signals: `Set(token, bool)` (level semantics — volumes), `Latch(token)`
  (event semantics — true forever; `Set(false)` on a latched token is
  ignored), `Get(token)`, and `Changed` (raised only on actual value change).
  Tokens are **strings** for inspector authoring. The bus is **per-sector**
  and created **fresh on every `Setup()`** (handed to modules via
  `SectorBuildContext.Bus`), so a restart / RL episode reset never sees stale
  latched tokens — no static anywhere, per `Multi_Arena_Substrate.md`.
  `Sector.Teardown()` calls `Freeze()` **before** module teardown: a frozen
  bus ignores `Set`/`Latch` and never raises `Changed`, so no rule fires or
  publishes while the sector dismantles (modules tear down sequentially).
- **Spine stage rides the same bus.** The sector objective's stage is
  published as latched tokens (e.g. `spine:ready-to-extract`), so **state
  terms are just signal terms** — one term kind covers state + spatial +
  event sources. Wired in PR-3; PR-1 ships `Signal` and `Time` term kinds
  only.
- **No new manifest slice.** `ActivationRule`, `TriggerVolume` (and PR-3's
  encounter) are `SectorModule` subclasses and ride the existing `modules[]`
  slice via `SectorManifestSync`'s module crawl. Rules chain in data:
  `publishOnFired` tokens latched by rule A are signal terms of rule B.
- **Bundle/authoring convention** — *hierarchy edge = ownership/lifetime;
  serialized ref = binding*:
  - A **thin rule sits directly ON the persistent fixture GO** it gates (the
    extraction-gate case) — the rule component is the fixture's arming logic
    and owns no content.
  - A **fat encounter is a sector-level child** that *owns* its private
    fixtures as children (spawn points, proximity volumes — they live and die
    with it) and *binds* to shared fixtures by in-prefab serialized
    reference.
  - Worked demo tree:

    ```text
    CombatSector (Sector)
    ├─ KeyPickup                      ← shared fixture, present at spawn
    ├─ ExtractionGate                 ← shared fixture, present + inert
    │   ├─ TriggerVolume  → "in-gate"
    │   └─ ActivationRule ["spine:ready-to-extract" AND "in-gate"]
    │        → start extraction challenge          ← thin rule ON the fixture
    └─ AmbushEncounter                ← fat encounter, sector-level child
        ├─ ActivationRule [time ≥ 30 AND "near-derelict"] → spawn waves
        ├─ TriggerVolume → "near-derelict"          ← private fixture (owned child)
        ├─ WaveSpawnPoint ×N                        ← private fixtures (owned children)
        └─ gate ⇢ ExtractionGate (serialized ref)   ← binding, not ownership
    ```

## PR sequence

**Rethink-proof — behavior-identical until the payoff PR, land first:**

- **PR-1 · Event bus + activation-rule engine + `TriggerVolume` (dormant). BUILT.**
  Introduce the per-sector bus, the `ActivationRule` primitive (terms →
  standing/latched predicate → effect), and `TriggerVolume` as a spatial term
  source. Nothing real consumes it yet. **Behavior-identical** (no wiring).
  Keystone; proven by unit tests: a `state AND spatial` rule fires once when both
  hold; **parked-then-qualified** fires (level, not edge); it latches (no re-arm on
  leave/re-enter); an event term fires from a published token.
- **PR-2 · Two-tier objective service.** Replace single-tenant `IObjectiveService`
  with spine + local slots; migrate the demo's current objective onto the **spine**
  tier with **zero locals**. **Behavior-identical** (one spine today). Independent
  of PR-1; either order.

**Payoff — flips behavior, deletes the queue:**

- **PR-3 · Encounter-as-rule + presence/anchor split; retire the serial module.**
  Rebuild `Encounter` as the bundle; make `ExtractionZone` a **sector fixture** the
  extraction rule **binds** to (not `OnSetup`-instantiated); convert the demo per
  the worked example; wire `key-acquired → extraction` via the bus; **delete
  `EncounterSequenceModule`** and the physics-frame hack. This is where the
  extraction gate becomes present-at-spawn and activation becomes a gated standing
  predicate.
  - *Effect binding (decided in PR-1 review):* an encounter subclasses
    `ActivationRule` and overrides `protected OnFired()` — the subclass IS the
    effect, so there is no post-Setup binding race against an already-fired
    (e.g. empty-term immediate) rule. Fire order is pinned: `OnFired` →
    `Fired` event → publish `publishOnFired` tokens, so a rule's own effect
    completes before any downstream rule runs. The public `Fired` event +
    `HasFired` remain for external/late binders.
  - *Spine-stage publisher needs a step-change seam (deferred from PR-2 #135
    review):* `ObjectiveType`-based events are insufficient —
    `ObjectiveTracker` suppresses transitions between string steps that share
    an `ObjectiveType`, and objective set/clear emit no event. When PR-3
    builds the bus publisher for spine-stage tokens, it must add a step-level
    change signal on the objective service, not reuse the type-level event.
- **PR-4 · Author a real overlapping/timed combat encounter (the original ask).**
  A combat encounter whose rule uses spatial + delay terms, lazily spawns its
  wave, completion = cleared. Proves the model delivers what started this —
  several things alive at once, each on its own schedule. **Split into PR-4a
  (command-edge cleanup) + PR-4b (ambush) — see "PR-4 — decision brief".**

**Scope claim:** PR-1/PR-2 are additive/behavior-preserving and land immediately;
PR-3 is the single behavior-flipping change (well-covered by the demo as an
end-to-end test); PR-4 is pure authoring on the finished mechanism.

### PR-3 — resolved scope (grill 2026-07-13)

Locked with the user before the build; supersedes the PR-3 bullet above where
they differ. Ground truth from a full blast-radius sweep: `CombatSector.prefab`
is the **only** YAML carrier of `EncounterSequenceModule`; the only objective UI
consumer is the spine-only `MinimapObjectiveMarker`; nothing in production calls
`OpenLocal` or `Encounter.Fail()`.

- **Spine-only demo; zero locals.** The vault's spine IS `acquire Key →
  Extract`, so the worked example's "local find-the-key objective" is treated as
  a sketch artifact. The sector installs **one** unified spine mission
  (`explore → key-acquired → extraction → completed` — the full tracker chain
  `ObjectiveTrackerEditModeTests` already pins). Minimap untouched (same
  `ObjectiveType`s). Locals get their first production consumer in PR-4.
- **`Encounter` base + both subclasses + both encounter prefabs are DELETED in
  PR-3**, not rebuilt. The new `Encounter : ActivationRule` bundle is deferred
  to PR-4 so its API is designed against its first real consumer (the ambush),
  not speculatively.
- **New `SectorSpineModule`** (rides `modules[]`) is the queue module's
  successor and the spine's single owner: serialized refs to the fixtures it
  binds (`KeyPickup`, `ExtractionZone`), builds mission + state builders in
  code, initializes fixtures with player identity from `ctx.Player`, publishes
  spine steps as latched bus tokens (`spine:<step>`), maps spine terminal →
  `RequestSectorEnd(Extracted/Failed)` (Failed path has no live trigger today —
  future-proofing).
- **Step-level service seam:** `ObjectiveTracker` + `IObjectiveService` gain a
  step-change event (type-level `OnStateChanged` suppresses same-type step
  transitions — confirmed at code level; the token publisher needs steps).
- **CombatSector.prefab restructure:** `KeyPickup` + extraction gate become
  authored present-at-spawn children (nesting the existing `Key Radio` /
  `Station Extraction Zone` prefabs). The gate carries `ExtractionZone` +
  `TriggerVolume`("in-gate") + a thin `ActivationRule` subclass whose `OnFired`
  activates the serialized chaser ref.
- **Chaser timing = key-acquired (behavior parity):** the thin rule's terms are
  `[spine:ready-to-extract]` only — the chaser hunts the player en route, as
  today. The in-gate volume exists and publishes but is not a term yet
  (arrival-gated challenge is a later gameplay-tuning option). The
  parked-then-qualified case is still exercised via the zone's polled
  completion predicate.
- **Key keeps `SpawnKey` scatter** at setup (behavior parity, per-run variety).
- **PlayerMarker→rigidbody identity sweep rides in** (the three consumers are
  exactly this PR's rewrite surface): delete `PlayerMarker` + the `SectorUtils`
  runtime stamp; `KeyPickup`/`ExtractionZone`/`TriggerVolume` compare
  `other.attachedRigidbody` against injected player identity. The `"Player"`
  **tag stays** (`ShipVisualRig` consumer). Compound-collider occupancy test
  debt rides along.
- **Tests:** module/encounter tests die with their classes; replaced by an
  end-to-end demo PlayMode test (key→extract via bus, incl. parked-in-gate-
  then-get-key at the real gate) + `SectorSpineModule` lifecycle coverage
  (destroy-without-teardown in PlayMode — `OnDestroy` only fires on awakened
  components, the PR-2 lesson).
- **Sequencing vs multi-arena #137:** five-file overlap
  (`ExtractionEncounter`, `KeyPickupEncounter`, `EncounterSequenceModule`,
  `SectorUtils`, `KeyPickup`). PR-3 branches **off #137's branch**
  (`task/pr-b-spatial-offset`) and merges after it, adapting to
  `arena.Place`/root-parenting as second mover — and likely deleting the two
  encounter `Place` call sites, since authored fixtures inherit the arena
  offset by hierarchy.
- **Out of scope:** lazy-spawn ownership/teardown (PR-4 — no lazy content in
  this demo; the chaser is pre-placed), locals UI, chaser-at-gate gameplay
  variant, collider-keyed registry.

### PR-3 — build decisions (2026-07-13)

Decisions made during the build, within the locked scope:

- **Spine step ids:** `explore → key-acquired → ready-to-extract → completed`
  (+`failed`), constants on `SectorSpineModule`. The extraction-challenge step
  is literally named `ready-to-extract` so the published token is
  `spine:ready-to-extract` — the exact term the thin rule gates on; no
  alias/mapping layer.
- **Occupancy is a per-rigidbody level** (`RigidbodyOccupancy`, shared by
  `TriggerVolume` and `ExtractionZone`): collider enter/exit counts per
  `attachedRigidbody`. This kills the compound-collider double-enter/exit bug
  AND lets player identity arrive *after* the player is already parked inside
  (identity is compared against buffered physical truth, never against an
  enter-edge) — which is what makes parked-in-gate-then-get-key work with no
  physics-frame hack anywhere.
- **`ExtractionZone.Initialize(Rigidbody player, Transform blocker = null)`:**
  the spine module injects identity at Setup (occupancy must be tracked from
  spawn); the rule re-calls it at fire time to bind the chaser blocker.
  Initialize never resets occupancy — occupancy is physical truth owned by
  trigger events.
- **Step seam shape:** `ObjectiveTracker.OnStepChanged(string)` fires on every
  transition; `IObjectiveService.OnSpineStepChanged` forwards it and also fires
  on `SetSpineObjective` with the initial step (so the publisher latches
  `spine:explore` without a special case). `SpineStep` property added
  alongside `SpineState`.
- **Key scatter home:** the module captures the fixture's authored position on
  first Setup and scatters around it every (re)Setup — restarts don't drift
  the scatter center.
- **`ArenaEncounterPlacementPlayModeTests` deleted** with the encounter
  classes it exercised; its arena-offset guarantee is inherited by hierarchy
  (authored fixtures ride the sector root; no `Place` call sites left) and the
  prefab wiring is pinned by `CombatSectorPrefabEditModeTests` (manifest
  drift + fixture plane positions + rule terms), the demo flow by
  `SectorSpineDemoPlayModeTests`.
- **Codex review round (pre-merge):** `RigidbodyOccupancy` tracks the actual
  colliders per rigidbody and prunes destroyed/disabled/inactive ones on read
  (Unity fires no `OnTriggerExit` for a collider deactivated inside a trigger —
  a dead ship must not hold a zone), and `TriggerVolume` re-publishes its level
  each `FixedUpdate` so the bus follows the prune. `ExtractionZone` split into
  `BindPlayer`/`Arm`/`Disarm`: unarmed reads as not-in-zone, so a missing or
  mis-wired challenge rule can never complete extraction silently
  (`ExtractionChallengeRule` validates both serialized refs and goes inert-with-
  error like the spine module). Spine mutation moved behind
  `SpineObjectiveHandle`, mirroring `LocalObjectiveHandle`: `SetSpineObjective`
  returns the handle (with optional install-time target); ambient
  `SetSpineTarget`/`FailSpine`/`RestartSpine`/`ClearSpine` removed from
  `IObjectiveService`; mutation through a superseded handle is a no-op.
- **PR-4 note:** fat encounters follow the ownership-handle pattern
  (`OpenLocal`/`SetSpineObjective` handles own their teardown); never call
  ambient `ClearAll` from encounter code — it belongs to session sweep only.

### PR-4 — decision brief (pr-prep 2026-07-14)

Frozen with the user; the implementing agent builds, it does not re-decide.
PR-4 splits into **PR-4a** (behavior-identical cleanup) then **PR-4b** (the
ambush encounter). Both branch off current main; no in-flight collisions
(#143/#144/range-hold touch no sector/objectives files).

**Standing rule adopted** (codified in root `CLAUDE.md` wiring philosophy §5
and `feedback_dependency_wiring_philosophy`): *refs bind and observe; signals
cause.* A serialized/held ref exists to bind (Initialize-style injection) or
observe (poll state, read a target) — never so one peer can command another at
runtime. Runtime causation between peers rides a bus token / event the actee
subscribes to; command calls are legitimate only downward (owner→owned,
caller→service) or during setup/teardown orchestration. A 2026-07-14 audit
found the tree unanimous except `ExtractionChallengeRule.OnFired`. Rolling
back PR-3 #141 was considered and **rejected** — the violator is two lines;
the rest of #141 is the rule-following substrate itself.

#### PR-4a — retire the command edge (behavior-identical)

- New `ActivateOnToken : SectorModule` on the chaser GO: serialized token +
  `Configure` test seam (mirror `TriggerVolume`); subscribes at Setup,
  `SetActive(true)` on its **own** GameObject when the token goes true (the
  actee subscribes — `KeyPickup` self-toggle precedent); Teardown restores
  inactive. Works while the GO is inactive (module Setup is a plain iterator
  call from `Sector`, not a Unity lifecycle event).
- `ExtractionChallengeRule`: `publishOnFired` gains
  `extraction-challenge-started`; the `chaser` field, both its command lines,
  and the chaser half of the editor `Bind` seam die. `Arm` stays — the thin
  rule is the fixture's co-located arming logic (ownership edge).
- `ExtractionZone` gains a serialized blocker ref (in-prefab binding —
  observation, sanctioned); `Arm()` drops its parameter.
- Parity: the token latches in the same synchronous `EvaluateNow` chain the
  direct `SetActive` ran in — the chaser wakes the same frame.
- `CombatSectorPrefabEditModeTests` re-pin the token wiring (they currently
  pin the violating ref — the ratchet working as intended).

#### PR-4b — ambush encounter (the payoff)

Forks (locked, with why):

- **Bespoke `AmbushEncounter : ActivationRule`** — no base-class extraction,
  no generic data-driven `CombatEncounter`; generalize only when a 2nd
  encounter type exists (evidence gate; issue #306). *Why:* design against
  the first real consumer; most of "an encounter" is already generic
  substrate (rule terms, bus, spawner params, `OpenLocal`).
- **Demo = one encounter, single wave: enter area → short timer → spawn.**
  A second *instance* of the same class in PlayMode tests pins N-concurrency.
  *Why:* proves sequencing + spine-concurrency + lazy spawn + first
  production local with minimal authored surface.
- **New `fireDelaySeconds` on `ActivationRule` (default 0):** the predicate
  latches on first satisfaction (leaving the area does NOT cancel); the whole
  fire sequence (`OnFired` → `Fired` → publish) runs as one unit after the
  delay; Teardown/bus-freeze cancels a pending fire. *Why:* "enter, then
  timer" is not expressible in the term algebra (Time terms count from
  Setup); rule-level delay keeps the causal-order pin and composes in data.
- **Lazy spawn = token-gated production.** `SectorSpawner.Build` becomes a
  sealed template: capture ctx; empty serialized activation token → produce
  now (default — behavior-identical for all existing spawners); token set →
  subscribe, produce exactly once when it goes true. No public `Produce()`
  command seam; the chain is data:
  `TriggerVolume("near-derelict") → AmbushRule[near-derelict]+delay →
  publishOnFired["ambush-started"] → RingSpawner(activation="ambush-started")`.
  *Why:* the bus is the coupling seam; spawner already receives it via ctx;
  teardown/despawn and restart ride the existing sector-owned paths
  unchanged — the deferred "lazy-spawn ownership" question dissolves.
- **Locals HUD deferred** (issue #307). Locals visualized by an editor-only
  `[DrawGizmo]` static over `ObjectiveService` drawing live local targets
  (editor-split recipe; no component attachment, so the parked
  editor-viz-unattachable issue is moot).

Assumptions (confirmed):

1. Encounter = sector-level child owning its `TriggerVolume` + `RingSpawner`
   as children (hierarchy = ownership); spawner bound by serialized ref for
   **observation only** (completion predicate).
2. `OnFired` opens the local via `OpenLocal`; the encounter closes its own
   handle on completion; never ambient `ClearAll`.
3. Completion = polled `ObjectiveState` over `spawner.Spawned` all-dead
   (null-or-death-flagged), `ExploreState(IKeyTracker)` shape; fail-closed at
   Setup (missing ref → loud error, inert rule).
4. On cleared: latch serialized `publishOnCleared` tokens
   (`ambush-cleared`) — completion-time signal, distinct from
   `publishOnFired`.
5. New `ObjectiveType.ClearHostiles` for the local; minimap spine whitelist
   untouched by construction.
6. Wave = existing `RingSpawner` (serialized team/commander; respawn None),
   dormant via its activation token. Size/delay are authoring knobs
   (~2-4 ships, ~5-10s) — implementer tunes.
7. Coexists with the existing adopted live enemy; authored in
   `CombatSector.prefab` in place (it is *the* shipped demo sector).
8. Reset/teardown need nothing new: fresh bus per `Setup()`, freeze cancels
   pending delay, spawner Teardown despawns products.
9. Tests: PlayMode end-to-end (enter → delay → spawn → concurrent-with-spine
   → clear → local closes → token latched) + two-instance concurrency +
   EditMode prefab pins updated; `[Category("Sectors")]`.

### PR-4 — build decisions (2026-07-14)

Decisions made during the build, within the frozen brief:

- **Manifest crawl extension (PR-4a, brief-forced):** `SectorManifestSync`'s
  child-module crawl skipped recognised content nodes entirely, so a module ON
  the chaser (a `Ship`) read as an orphaned manifest entry. The crawl now
  collects modules carried on a recognised node while still never descending
  into its subtree; pinned by an EditMode test.
- **`HasFired` = fire-sequence-ran (PR-4b):** with `fireDelaySeconds`,
  predicate satisfaction and firing separate; `HasFired` is now a flag set by
  the fire sequence (reset each Setup), not an alias of `predicate.Satisfied`.
  All existing consumers hold under both readings for delay 0.
- **Token-gated production drains `Produce` synchronously** so the wave lands
  in the same frame as the latch (parity with the eager path producing inside
  Setup). All shipped producers are synchronous iterators.
- **`IHostileTracker` lives beside `ClearHostilesState`** (the `IKeyTracker`
  precedent, co-located with its consumer); `AmbushEncounter` implements it —
  the encounter polls its spawner, the state polls the encounter. Wave-not-yet-
  spawned reads as not-cleared (fail-closed against a misconfigured spawner).
- **Authored knobs:** trigger radius 20 at plane (-10, 25), `near-ambush`
  token, 6 s fire delay, 3-ship wave at ring radius 12, team 1, respawn None,
  `ambush-started`/`ambush-cleared` tokens.

### PR-5 — signal-port refactor decision brief (pr-prep 2026-07-15)

Frozen with the user (Codex design consult + 24-mode adversarial round folded);
the implementing agent builds, it does not re-decide. Seed:
`feedback_token_authoring_fragility` — free-text bus tokens are the fragile
seam; the user's bar was **foolproof, not validated**. Scope: free-string
tokens die end-to-end; wiring becomes component-reference identity. Non-goals:
no GraphView window (issue #305), no scene overlay, no locals HUD, no
`MissionDefinition`/objective changes, no bus-lifecycle changes.

Forks (locked, with why):

- **Token identity = publisher-owned `SignalPort` component refs** (Codex
  rank 1 of 7; strings+cross-check ranked 4). A `SignalPort` component IS the
  signal; `SectorEventBus` keys on the port reference
  (`Set/Latch/Get(SignalPort)` + `Changed(SignalPort)`); latch/level/
  fresh-bus-per-Setup/freeze semantics unchanged. Publishers own **fixed
  semantic ports**: `TriggerVolume`→inside, `ActivationRule`→fired,
  `AmbushEncounter`→fired+cleared, `SectorSpineModule`→five step ports
  (mapped from step strings by an exhaustive switch that errors on unknown;
  `MissionDefinition` stays string-keyed; the `spine:` concat seam dies).
  `publishOnFired`/`publishOnCleared` string arrays are **deleted** —
  publishers latch their own ports; no publisher-side authored surface
  remains (an authorable `SignalPort[]` would recreate the failure in ref
  form). Consumers (`ActivationTerm`, `ActivateOnSignal` — renamed from
  `ActivateOnToken` — and `SectorSpawner`) store only port refs; picker
  restricted to ports under the same `Sector`. *Why:* typo, rename drift,
  consumed-with-no-publisher, and cross-prefab wiring become structurally
  impossible or collapse to a missing ref — the existing loud-inert idiom;
  validation demotes to defense-in-depth.
- **Graph model + validator + inspector causal-tree view ship in this PR;
  node-graph EditorWindow deferred** (issue #305, evidence-gated on sectors
  growing). Pure UnityEditor-free graph/validator beside `SectorManifestSync`
  reading the **baked manifest** (what `Setup` actually runs — hierarchy gaps
  stay the drift badge's job); `SectorEditor` renders the derived causal tree
  (spine backbone, encounters hanging off their terms, locals + unconsumed
  ports annotated) beside the Sync/drift UI. *Why:* the validator must build
  this graph anyway, and hierarchy stays the sole source of truth so the view
  can never drift — the user's mission-sequence-visibility ask lands as a
  rendering, not a second representation.

Blindsider resolutions:

- **Ports are manually authored; zero editor magic.** Add the `SignalPort`
  component and drag it into the publisher's serialized port field — the
  exact recipe as the `waveSpawner`/`extractionZone`/`blocker` refs. No
  ensure-button, no `OnValidate` self-heal (AddComponent is illegal there;
  any regeneration churns fileIDs and orphans every consumer ref). Missing,
  unclaimed, or deleted ports are caught three ways: validator badge,
  all-sector sweep test, Setup loud-inert. **Nothing in the pipeline may ever
  delete-and-recreate a port.** (Ensure-ports convenience = possible later
  card if authoring grows tedious.)
- **Inactive-publisher guard, cheap half only:** Setup-time loud-inert error
  when a publisher that needs Unity messages (`TriggerVolume`, an
  `ActivationRule` with `fireDelaySeconds > 0`) sits on an inactive GO; the
  consumer-side `ActivateOnSignal` on the dormant chaser is exempt by design.
  Runtime `OnDisable` policy deferred — no live deactivation path exists;
  known un-guarded evolution path.

Assumptions (confirmed):

1. Pure graph/validator static class beside `SectorManifestSync`
   (UnityEditor-free, EditMode-testable); picker drawer + causal-tree
   rendering in `Game.Core.Editor` (`Scripts/Editor/Inspectors`), per
   `SceneReferenceDrawer`/`SectorEditor` precedent.
2. Validator input = baked manifest (`Sector.Modules`/`Spawners`), never a
   fresh crawl.
3. `SectorSpawner`: explicit `ActivationMode { Eager, Gated }`; `Gated` +
   null port = loud inert. **Null never means eager.**
4. Ports carry `Kind { Level, Latch }` shown in picker labels; no consumer
   restricts by kind yet (terms legitimately consume both).
5. Blank-token guards become null-ref + same-`Sector`-ownership guards at
   Setup, same message shape.
6. Multi-publisher same-token OR is lost (ports are single-owner); nothing
   uses it — rule-of-three watch.
7. Port components visible in the inspector but excluded from the Add
   Component menu surface authors reach for by accident; created deliberately.
8. Tests: `CombatSectorPrefabEditModeTests` re-pin ref wiring; `Configure`
   seams take ports (`ConfigureEager()`/`ConfigureGated(port)` split on the
   spawner); new EditMode suites for bus keying + graph model/validator; an
   **all-sector-prefabs sweep** (unowned ports, gated-null, cross-sector
   refs, cycles, publisher-field-unassigned) so future sectors are covered by
   construction; `[Category("Sectors")]`; `SectorSpineDemoPlayModeTests`
   proves behavior parity end-to-end.
9. `CombatSector.prefab` rewired in canonical Unity serialization.

## Open questions (decide at build)

Manifest plumbing and bus token type were resolved in PR-1; the spine/local API
landed in PR-2; the migration audit is done (`CombatSector.prefab` is the sole
carrier); the PR-3 forks are locked above; the PR-4 forks are locked in its
decision brief. Dispositions of the former PR-4 questions:

- **Lazy-spawn ownership/teardown** — RESOLVED by token-gated spawner
  production (PR-4 brief): the sector already owns product lifetime through
  the existing spawner Build/Teardown paths; no new handle needed.
- **Locals HUD contract** — DEFERRED to issue #307; PR-4b ships an
  editor-only locals gizmo instead. `MinimapObjectiveMarker` stays spine-only.

Deferred test debt (from PR-1 review): a serialized `ActivationTerm[]`
inspector round-trip test, and compound-collider `TriggerVolume` occupancy
(multi-collider players can double-enter/exit; rides the
PlayerMarker→rigidbody identity cleanup — memory
`project_playermarker_identity_cleanup`).

## Related

- `Obsidian: Design/World/Encounters.md`, `Design/World/Sectors.md`,
  `Design/Gameplay/Rogue-like.md` (design intent)
- `doc/Feature_Plans/Multi_Arena_Substrate.md` (per-arena scoping the bus/objective
  must respect; no new process-wide statics)
- memory `project_objectives_encounters_rethink` (the durable why),
  `feedback_dependency_wiring_philosophy`
- Board: `## BUGS` → "Objectives/encounters/spawn-timing rethink"
