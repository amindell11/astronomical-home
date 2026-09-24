# Ships

A ship is the player's (or an enemy's) chassis in [[Combat]] and [[Flight]].
Ships differ in feel and role: some trade hull for maneuverability, some carry
more weapons, each has a signature ability kit. Rather than hard-code each ship
as a fixed bundle of stats, a ship is a **prefab chassis with typed slots filled
by Modules**. The prefab *is* the archetype: it carries the chassis stats and
geometry directly and exposes the slots that Modules drop into. This is the same
shape whether we're talking about a base archetype, a hangar loadout, or a
mid-run build assembled from loot.

> Everything that defines a ship — weapons, engine, shield, utility, keystone,
> passive — is a Module sitting in a slot. The ship prefab decides which slots
> exist.

## The model: chassis (the ship prefab) + Modules

**Chassis** (the archetype — the ship prefab itself)
- Base hull points, mass, base roll limit, and the **slot layout** (which slots
  exist) — authored directly on the prefab's `Ship` component.
- **Geometry is the prefab**: the visual rig, colliders and weapon mounts are
  children of the prefab; physical **size is the prefab's root scale** and the
  collision radius is *derived* from the scaled collider bounds (no authored
  scalar to drift).
- Per-slot **config**: a slot can be *open* (player fills it) or *locked* to a
  specific Module (baked into the ship's identity).
- The chassis is the identity layer. "Glass interceptor, light shield" vs
  "brawler, heavy shield, high hull" are two ship prefabs.

**Module** (the swappable part — this is what [[Ship Upgrades|upgrades]] and
[[Loot and Rewards|loot]] are)
- A data asset that occupies one slot and contributes **stats and/or an
  ability**: e.g. a high-thrust engine, a front-biased shield, a laser, a mine.

**Effective ship = chassis ⊕ (equipped Modules), resolved once at spawn.**
A single resolver reads the ship's own chassis + modules and flattens them into
one plain stats block that every system consumes. Nothing downstream knows about
modules — they read the *resolved* ship. Re-resolves whenever the build changes
(loot, hangar swap, or live inspector tuning).

## Resolution rule: disjoint ownership (no stacking, for now)

Every stat is owned by **exactly one** source. There is **no combination
math** — resolution is "read each field from its owner." Upgrading a stat means
**swapping the whole module** (a better engine is a different Engine module),
not stacking a bonus.

> A `+10% thrust` boon layer (additive/multiplicative modifiers on top) is a
> deliberate *later* addition — see [[Ship Upgrades]]. Whole-module swap first;
> boons when we actually build that kind of loot. This keeps the first version
> simple and is a clean base to add stacking onto later.

### Ownership map

| Stat | Owner |
|---|---|
| hull (`maxHealth`), lives, `mass`, `maxBankAngle`, slot layout | **Ship (chassis)** |
| physical size (root scale) → collision radius (derived from colliders) | **Ship prefab geometry** |
| thrust / reverse / strafe forces, top speed, yaw torque + rate, boost impulse + cooldown, linear + angular drag, roll torque + damping | **Engine** |
| max shield, shield regen delay, shield regen rate | **Shield** |

Notes on the judgement calls:
- **`mass` is the chassis's**, not the engine's. Under disjoint ownership there's
  no conflict: the ship sets `Rigidbody.mass`, the Engine supplies raw force,
  and acceleration just *emerges* from physics. A heavy ship with a strong
  engine feels sluggish for free.
- **`maxBankAngle` is the chassis's** — it's a near-visual roll limit, unlikely
  to vary much between ships and not something the player should tune. *How fast*
  it rolls (`bankTorque`/`bankDamping`) is engine handling.
- **Size and collision radius are the prefab's geometry**, not a tunable scalar:
  size is the prefab's authored root scale, and the collision radius is derived
  from the scaled collider bounds at spawn — a single source of truth.

## Slot taxonomy

| Slot | Count | Fills with | Status |
|---|---|---|---|
| **Engine** | 1 | engine module | **built** |
| **Shield** | 1 | shield module | **built** |
| **Weapon** | 2 (fixed for now) | [[Weapons]] | **exists**, frozen at two mounts |
| **Utility** | N | consumables, mines, countermeasures, trinkets | **designed, deferred** |
| **Keystone** | 1 | a keystone ability | **designed, deferred** |
| **Passive** | 1 | a passive | **designed, deferred** |

**Weapon count** is meant to be a big archetype differentiator (some frames
carry more barrels). But *how* N mounts map to the two fire triggers (see
[[Flight]]) can't be designed until more [[Weapons]] exist to inform it, so
weapons stay at **two fixed mounts (primary/secondary)** until then. Variable-N
is the intent; the routing decision is deferred.

## Keystone & passive: locked vs open (deferred to build)

There's a live tension between two earlier ideas: each ship having a *fixed,
unique* keystone + passive (identity), versus [[Keystones]] presenting them as
*player-chosen* either/or pairs. We don't resolve this as a global rule —
it's a "what's fun" question we can't answer from the armchair.

Instead: keystone and passive are **slots**, and each Frame decides per slot
whether it's **locked** (baked ability = fixed identity, easy to balance, reads
like a Hades character) or **open** (player picks, e.g. from the [[Keystones]]
pairs). A locked slot is just an open slot with its choice pinned in data.

- **Starting stance:** ship the first frames with *locked* kits — easier to tune
  and gives each ship a strong identity.
- **When playtesting says a ship wants choice**, flip that one slot to open — no
  code change, just data. Can go per-ship, even per-slot (locked passive + open
  keystone).

The *architecture* (slots) is decided because it's expensive to change later;
the *fun knob* (fixed vs chosen) stays in data because we'll retune it every
playtest. There is **no ability system in code yet** — keystone/passive are
designed as slots but unbuilt. See [[Combat Depth]] §5: pin the slot/keystone
contract before the [[Ai plans to move to RL|RL]] observation space freezes, so
build identity is an observable.

## Current roster (design intent vs what's built)

Five ship identities are the design roster — their intended feel:

- **Interceptor** — low hull, high maneuverability. Glass ship that lives on
  dodging (see [[Flight]] skill expression).
- **Tank / Brawler** — high hull, heavy shield, sluggish. Wants to close and
  trade — the "commit for the kill" side of the [[Combat Depth|tempo]] tension.
- **Fast** — speed/handling-tuned; differs from Default mainly in its Engine.
- **Default** — the baseline balanced chassis.
- **Cargo** — utility/peaceful chassis (may carry no weapons; ships can be
  unarmed).

**What's built today:** three ship *prefabs* — the baseline `Ship_1` (Default),
its variant hulls `Ship_2`/`Ship_3`, and the `Junker` cargo hull — each carrying
its chassis + `Default`/`Cargo` engine & shield modules. The `Fast`,
`Interceptor` and `Tank` seeds were **removed** (they were orphan stat-sets with
no prefab); they come back as real ship prefabs when we build those archetypes.

Keystones/passives per frame (e.g. Interceptor→*Ghost* blink, Brawler→
*Headstrong* front-shield) are illustrative until the ability system exists.

## Enemies use the same model

Per [[Combat]], AI enemies fly the *same* frames and modules the player does
(piloted by the RL policy through the MPC feasibility tracker). Because a ship is just Frame + Modules, a
**population of stylistically different enemies is nearly free** — different
frames and locked kits are different reward landscapes, exactly the diversity
[[Combat Depth]] and [[Ai plans to move to RL]] want.

## How this maps to the build

- The monolithic `ShipSettings` dissolved into the **ship prefab itself** plus
  two composable SO modules. Chassis stats (`mass` / `maxHealth` /
  `startingLives` / `maxBankAngle`) are serialized fields on the **`Ship`**
  MonoBehaviour; **`EngineModule`** and **`ShieldModule`** are the swappable
  slots. A resolver flattens `Ship` + modules into a plain **`ResolvedShipStats`**
  that feeds the existing movement/damage systems and the MPC `Dynamics`
  unchanged.
- There is **no `FrameSettings` asset** — the prefab *is* the frame. `Ship` holds
  the chassis fields directly plus typed module pointers (`engine` / `shield`);
  physical size is the prefab's root scale and the collision radius is derived
  from its colliders. Runtime "equip" = swap a module pointer + re-resolve.
- **Build history:** an intermediate step split `ShipSettings` into a
  `FrameSettings` SO + modules; that SO was then folded into the ship prefab
  (this doc's model) so a ship is one normal Unity prefab. Values carried across
  **verbatim**, so behaviour — and the test suite — stayed unchanged.
- Persistent player builds (a saved `ShipLoadout`, hangar-editable) are a later
  meta-progression concern layered on top of the same runtime model.

## Orientation convention (formalized 2026-07-08)

Every plane-dweller (ships, plane-locked objects) uses the same transform
convention, whose source of truth is `GamePlane.PlanePose` /
`GamePlane.Rotation` in code:

- **`transform.forward` = the game-plane normal**, which points **away from
  the top-down camera** (for the production Y plane, `GamePlane`'s normal is
  `Vector3.down`). The hull face you see in game is therefore the ship's
  **-forward** side.
- **`transform.up` = the in-plane heading** — the *nose*. Hull meshes are
  authored nose-along-local-+Y to match.
- **Heading changes are rotations about the plane normal** (i.e. about local
  forward) — see `UnitService.RespawnShip`.

This is exactly Unity's standard 2D-sprite convention (camera looks along
+forward; you see the sprite's back face) generalized to a configurable plane.
New code that needs "nose direction" or "lay the ship flat" math (e.g. the
hangar preview's showroom pose) must express it via **`GamePlane.PlanePose(normal,
heading)`** — never raw Euler magic numbers, and never sign-guessing the
normal (that mistake costs three debugging iterations; ask the hangar preview).

## Open questions

- **Boon/modifier layer:** when we add stacking `+%` upgrades, what's the
  per-field policy (additive vs multiplicative)? Tracked in [[Ship Upgrades]].
- **Utility slot contents & count** — undesigned; blocks building the slot.
- **N-weapon → fire-trigger routing** — deferred until more [[Weapons]] exist.
- **Run vs meta scope:** which slots lock at the hangar for a run vs swap freely
  from [[Loot and Rewards|loot]] mid-run (ties to [[Rogue-like]] permanence)?
  Tracked in [[Ship Upgrades]].
- **Enemy module pool:** do enemies draw from the same modules as player loot,
  or a separate tuning set? Affects balance and RL curriculum.

## Related
- [[Ship Upgrades]] · [[Weapons]] · [[Keystones]] · [[Flight]] · [[Combat]] ·
  [[Combat Depth]]
- [[Home Base]] (hangar loadout) · [[Loot and Rewards]] · [[Rogue-like]]
