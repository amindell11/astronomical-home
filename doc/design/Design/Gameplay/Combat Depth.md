---
type: consultant-suggestion
status: advisory
authored-by: Claude (design consult, 2026-07-01)
about: "[[Combat]]"
---

# Combat Depth — consultant suggestions

> [!note] These are **advisory suggestions**, not settled design.
> Written as an outside design consult on how to give [[Combat]] enough
> tactical depth that it (a) plays as "satisfying, exciting, unpredictable"
> and (b) becomes a well-posed problem for the planned move to RL (see
> [[Ai plans to move to RL]]). Adopt / reject / edit freely. Where this
> conflicts with settled design in [[Combat]], the settled doc wins — the
> conflicts to resolve are the two open questions at the bottom.

## The lens: depth = coupled tensions, verified by "no dominant strategy"

Combat has depth when every strong option **costs you on a different axis**,
so no single line of play dominates. [[Combat]]'s open question — "how to
prevent attacking always optimal" — is really *"what does attacking cost, and
what punishes over-committing?"* Today the main cost is asteroid collisions,
which isn't enough. The proposal is a small web of **tensions** that are mostly
reframings/sharpenings of existing systems, not new tech.

Why this matters for RL: a shallow environment makes a *learned* policy
converge to one ruthless optimum — the opposite of "unpredictable." Depth in
the mechanics is a prerequisite for interesting emergent/population behavior.
So this work is simultaneously game design and RL problem-definition.

## The tension web

### 1) Aggression vs. the shield-regen window (tempo — biggest lever)
[[Combat#Health|Shield]] already regenerates after a gap without taking damage,
and (while up) fully absorbs damage. Make that gap **require breaking contact** —
no regen while trading fire. Continuous aggression then stops being free:
once shield pops, staying in the brawl spends **hull, which never comes back**.
Optimal play becomes *bursts of engagement punctuated by peels to reset
shields* — a push/pull rhythm that reads as exciting rather than a facetank DPS
race.
- **Decision:** commit for the kill vs. peel to preserve hull.
- **Build:** system exists; tune the regen-gap, ensure any damage (incl.
  grazes/splash) resets it, make "shield recovering" legible.
- **RL signal:** a clean tempo variable; good vs. degenerate policies diverge
  here first.

### 2) Heat as a commit meter (laser)
Already have heat + overheat cooldown + the "hotter shots do more damage" perk
(see [[Weapons]]). Sharpen overheating into a **hard fire-lockout window** —
over-commit and you're briefly *unarmed*. With the damage-ramp perk, firing hot
is real risk/reward: more damage now, but flirting with a lockout the enemy
punishes.
- **Decision:** burst discipline vs. dumping heat to close a kill.
- **Build:** mostly exists; the perk turns a chore into a decision.
- **RL signal:** a second resource clock coupled against #1 (can't both brawl
  continuously *and* stay under redline).

### 3) Positioning & cover with real meaning (asteroids + Newtonian geometry)
Missiles already need LoS + angle + hold-time to lock. Extend "cover matters"
to lasers (asteroids block/eat shots) and, crucially, make **asteroids break
missile locks**. The field's geometry becomes a resource: poke from cover, or
juke behind a rock to shed a lock. Pair with a **lead indicator** so the
[[Flight|Newtonian]] aiming (fire-along-facing + carried momentum) is
skill-expressive — a target strafing perpendicular is genuinely hard to hit,
one closing head-on is easy, so *how you move is defense*.
- **Decision:** open brawl (more DPS, more exposure) vs. cover play (safer,
  cedes tempo/aim).
- **Build:** LoS partly exists; add asteroid lock-break + laser occlusion +
  lead reticle.
- **RL signal:** positional reward that isn't just "close distance" — makes
  learned movement look intelligent, not a beeline.

### 4) Missiles as interdiction / zoning (the escape-tool answer)
Reframe missiles from "burst damage" to **tempo control**: spend a scarce
resource (limited ammo) to *force the opponent to spend* — burn boost, break
LoS, or eat hull. A locked missile denies a clean boost-away, so **fleeing is
contested, not binary**: the pursuer fires to interdict; the fleer jukes into an
asteroid to kill the missile and break lock. Flee becomes a skill minigame, and
a disengaging enemy stays *threatening* (this is how you keep enemies from
feeling passive).
- **Decision:** spend scarce ammo to zone/pin vs. hoard for the kill; as target,
  spend boost/position to survive the lock.
- **Build:** exists; work is framing + tuning lock-hold, missile agility, boost
  interaction.
- **RL signal:** resource-vs-tempo trade + the explicit engage/disengage
  decision — exactly what you want a *population* of policies to solve
  differently.

### 5) Ship keystone + passive + slots = the diversity engine (later, design now)
Per [[Ships]], each ship has a keystone ability, a passive, and variable slots
(maneuverable-glass vs. tanky-brawler vs. many-weapon). This makes
"unpredictable" cheap: different archetypes are different reward landscapes → a
population of stylistically different enemies **for free** at RL time. Keystones
on cooldown (blink, shield overcharge, flare/countermeasure, sensor-jam that
delays enemy lock) create burst-decision moments. Not needed to start — but
**decide the slot/keystone contract before freezing the RL observation space**,
because build identity should be an observation.

## Verification tool: dominant-strategy audit

Prove depth with a matrix — for each strategy, what beats it and what it costs.
If every row has a real "punished by," you also have a well-posed RL problem.

| Strategy | Strong because | Costs / denies you | Punished by |
|---|---|---|---|
| **Brawler** (close, sustained laser) | high DPS, forces the fight | denies own shield-regen; heat-lockout risk | kiter who refuses the brawl + missile-zones |
| **Kiter/zoner** (range + missiles + LoS) | chips hull, controls space | spends scarce ammo; cedes tempo | brawler who closes through cover; evasion that shrugs missiles |
| **Evasion/flee** (juke, boost, cover) | resets shields, dodges commit | cedes damage; passive if overdone | interdiction missiles denying clean escape; getting pinned with no cover |

Columns couple (aggression spends the shield window *and* heat; ranged spends
ammo *and* tempo; evasion spends damage), so no row dominates. That coupling is
the depth.

## Proposed answers to [[Combat]]'s open questions

- **"Escape/interdiction tools & how they interact with Newtonian flight /
  hazards"** → **Boost** is the escape impulse; **missiles are the
  interdiction** that contests it; **asteroids are the counter-interdiction**
  (juke to break lock/LoS). The interaction *is* the Newtonian geometry — flee
  by spending boost + trading position; pursuer denies it by spending ammo;
  terrain arbitrates.
- **"Prevent 'attack always optimal' without passive enemies"** → the
  shield-window tempo tax (#1) makes non-stop aggression cost hull;
  interdiction (#4) makes disengagement *active and threatening*, so a peeling
  enemy still pressures you.

## Recommended build order (minimal → depth fast)

Most of this is tuning/reframing existing systems:
1. **Shield-regen-requires-break-contact** (tempo) — biggest depth-per-effort.
2. **Heat lockout + damage-ramp perk** — existing chore → decision.
3. **Asteroid lock-break + laser occlusion + lead indicator** — positioning
   becomes real.
4. **Missile interdiction framing** — tune lock-hold / boost interaction so
   flee is contested.
5. *(later)* keystone/slot contract → build diversity → policy diversity.

Doing 1–4 makes combat meaningfully deep *and* defines the strategy space RL
needs — without training anything yet.

## Related
- [[Combat]] · [[Flight]] · [[Weapons]] · [[Ships]]
- [[Ai plans to move to RL]] · [[Encounters]] (extraction wants flee/extract to
  be meaningful — the tensions above are what make that true)
