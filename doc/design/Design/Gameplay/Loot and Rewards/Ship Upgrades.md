# Ship Upgrades

An upgrade is a **Module** — a swappable part that fills a slot on a ship (see
[[Ships]] for the chassis + Modules model). "Upgrading" a ship means changing
what sits in its slots. Upgrades arrive as [[Loot and Rewards|loot]] on a run and are
equipped at the [[Home Base]] hangar or (where allowed) mid-run.

> There is no separate "upgrade system." An upgrade *is* a module, and equipping
> one is the same act whether it came from a shop, a drop, or the hangar.

## Upgrade categories = slot types

The four categories from the original note map directly onto [[Ships|slot
types]]:

| Category | Slot | What it changes | Status |
|---|---|---|---|
| **Engines** | Engine | thrust, top speed, boost, handling | **built** |
| **Shield** | Shield | max shield, regen delay/rate, (later) shape | **built** |
| **[[Weapons]]** | Weapon ×2 | what each mount fires | **exists** (two mounts) |
| **Ship Passives** | Passive / Keystone / Utility | always-on effects, signature ability, situational tools | **designed, deferred** |

## How an upgrade changes the ship: whole-module swap

Per [[Ships]]' **disjoint-ownership** rule, a module *owns* its stats outright —
so an upgrade is a **whole-module replacement**, not a stacked bonus. A better
engine is a different Engine module that fully defines thrust/speed/boost;
slotting it *replaces* the old one. No arithmetic, no modifier stack (for now).

This means early upgrades are **sidegrades and identity choices**, not "+numbers"
— a nimble racing engine vs a heavy long-boost engine, a fast-regen light shield
vs a big slow-regen shield. That fits the [[Combat Depth]] philosophy (every
strong option costs you on another axis) better than a pure power ladder.

### Later: the boon/modifier layer

A second kind of upgrade — small stacking modifiers (`+10% thrust`, `+15
shield`), i.e. [[Hades]]-style boons — is a **deliberate later addition**. It
sits *on top* of resolved module stats as a modifier pass and doesn't change the
module model. Deferred until we build that kind of loot; the per-field policy
(additive vs multiplicative) is an [[Ships#Open questions|open question]].

## Run-scoped vs permanent

[[Rogue-like]]: most power gained on a run is **lost on death**, but some rewards
**persist**. Upgrades inherit this — a module carries a **scope**:

- **Run-scoped** — found as [[Loot and Rewards|loot]] mid-run, equipped
  immediately, lost when the run ends. The bulk of moment-to-moment build
  variety.
- **Permanent** — crafted from [[Crafting parts|parts]] at [[Home Base]], or
  earned via contracts; carries across runs and forms your starting hangar
  options.

Scope is a flag on the module, **not** a separate system — the runtime treats
run-scoped and permanent modules identically; only *persistence between runs*
differs. This keeps loot, crafting, and the hangar unified over one model.

## Where upgrades are chosen

- **[[Home Base]] hangar** — assemble your starting build for a run from
  permanent modules ("choose your engine, etc.").
- **Mid-run** — swap in run-scoped modules found as loot (subject to which slots
  a run allows swapping; see open question).
- **Simulated drill field** ([[Home Base]]) — test a build against the AI
  companion with no death penalty.

## Open questions

- **Which slots swap mid-run vs lock at the hangar?** (The chassis/hull likely
  locks for a run; engine/shield/weapons maybe swappable at POIs.) Ties to
  [[Rogue-like]] pacing.
- **Do enemies drop the exact module they were using**, or from a loot table?
  (Enemies use the same chassis + Modules — see [[Ships]].)
- **Rarity / progression curve** — how do run-scoped modules scale over a run so
  power grows without a flat "+numbers" ladder?
- Boon-layer per-field policy (see [[Ships#Open questions]]).

## Related
- [[Ships]] · [[Weapons]] · [[Keystones]] · [[Combat Depth]]
- [[Loot and Rewards]] · [[Crafting parts]] · [[Home Base]] · [[Rogue-like]]
