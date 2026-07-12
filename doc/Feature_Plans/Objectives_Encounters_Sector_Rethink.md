# Objectives / Encounters / Sector Rethink

*Draft • 2026-07-12 • status: design agreed (design session), build not started*

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
- **Activation rule / encounter** — a placed child reconciled into a new manifest
  slice (peer of `spawners[]`/`modules[]`), carrying a small serialized **term
  list** (flavor + key/target + params) and an **effect** (content spawn-spec +
  local-objective builder + on-complete event tokens). Terms reference fixtures /
  volumes / spine-stage / event tokens by serialized reference or token.
- **Sector event bus** — a per-`Sector` instance (owned like the build context),
  tokens as a small typed enum or interned string keys for inspector authoring.
- **Two-tier objective service** — spine API (`SetSectorObjective` / stage) +
  local-slot API (`OpenLocal` / `CloseLocal`) replacing the single
  `SetObjective` / `SetTarget`; HUD reads spine prominently + active locals
  contextually.

## PR sequence

**Rethink-proof — behavior-identical until the payoff PR, land first:**

- **PR-1 · Event bus + activation-rule engine + `TriggerVolume` (dormant).**
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
- **PR-4 · Author a real overlapping/timed combat encounter (the original ask).**
  Now trivial: a combat encounter whose rule uses time/spatial terms, spawns waves
  that **overlap**, completion = cleared. Proves the model delivers what started
  this — several things alive at once, each on its own schedule.

**Scope claim:** PR-1/PR-2 are additive/behavior-preserving and land immediately;
PR-3 is the single behavior-flipping change (well-covered by the demo as an
end-to-end test); PR-4 is pure authoring on the finished mechanism.

## Open questions (decide at build)

- **Manifest plumbing** — do encounters/rules get their own `SectorManifestSync`
  slice, or ride `modules[]`? `TriggerVolume`s and fixtures as placed children
  referenced by rules — by serialized ref or by token?
- **Bus token type** — typed enum (safe) vs interned strings (designer-authorable).
  Scope is per-sector = per-arena; confirm no static leak vs `Multi_Arena_Substrate`.
- **Lazy-spawn ownership/teardown** — a rule's spawned content must despawn on
  sector teardown / episode reset (RL); who owns the handle (the rule, via the
  sector's teardown pass)?
- **Two-tier objective API surface** — exact spine/local methods + HUD contract
  (`CurrentTarget` becomes spine-target; locals need their own contextual markers).
- **Migration of `EncounterSequenceModule` authored content** — audit which sectors
  reference it (Combat/Arena/Testbench prefabs) before deleting.

## Related

- `Obsidian: Design/World/Encounters.md`, `Design/World/Sectors.md`,
  `Design/Gameplay/Rogue-like.md` (design intent)
- `doc/Feature_Plans/Multi_Arena_Substrate.md` (per-arena scoping the bus/objective
  must respect; no new process-wide statics)
- memory `project_objectives_encounters_rethink` (the durable why),
  `feedback_dependency_wiring_philosophy`
- Board: `## BUGS` → "Objectives/encounters/spawn-timing rethink"
