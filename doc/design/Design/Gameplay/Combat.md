# Combat

Enemy AI is intended to be intelligent and difficult to kill, and piloting has a steep but rewarding learning curve.

High-level intent
- Enemies are fewer in number since they are more skilled than typical roguelike mobs.
- Sometimes the best option is to **flee** rather than fight, or use stealth, since enemy difficulty is high and objectives may not require defeating them.

See also: [[Flight]]

## Weapons

### Laser
- Fires a fast, fixed-velocity projectile in a straight line along current facing direction.
- Heats while firing and cools while not firing.
- Overheating triggers a cooldown penalty.

### Missile
- Slower, acceleration-driven projectile.
- Modes:
  - “Dummy fire” (unguided)
  - “Lock-on” (guided)
- Lock-on requirements:
  - target in range
  - line of sight
  - angle tolerance held for a few seconds
- Once locked, missile pursues the target subject to dynamic limits.
- Missiles can be destroyed by lasers, other missiles, or asteroids.
- Explosion deals high damage in a small AoE around impact.
- Missiles should support a stored burst while making a reserve worthwhile. Spending the reserve should create a recovery window.

## Health
- Health is split into **shield** and **hull**.
- Shield regenerates slowly after a short period without taking damage.
- Hull does not regenerate.
- Incoming damage hits shield first; the remainder on a shield-breaking hit bleeds through into hull (council §C3, shipped PR #235).
- At zero hull, the ship explodes.

## Damage
- Ships take damage from enemy projectiles and from collisions.
- Asteroid collision damage is proportional to collision energy and the normal component of relative velocity:
  - glancing blows do less damage than head-on impacts
  - smaller asteroids do less damage than larger ones

## AI
- Enemy uses the same ship and controls as the player.
- Flown by a **scripted pilot** whose temper picks its tactics (velocity commands + fire/boost), executed through the
  **MPC feasibility tracker** (velocity tracking, intercept aim, obstacle avoidance).
- Tactics (attack run, flank, stand-off, fall-back, break-off) come from authored rules over the ship's situation.

## The fight as it plays today (owner evidence, 2026-11-25)

The owner reports one autopilot loop that answers most fights. Circle the
enemy to stay out of its targeting, fire the main gun on cooldown, spend mines
or missiles as a finisher while the gun recovers, and save the keystone as a
panic dodge. Enemies change little in response to what the player does from
second to second, so the same circle keeps working and fights repeat.

The loop breaks in two reported ways, and neither is an enemy countering the
circle. The gun overheats while no missile and no keystone are ready, and
incoming hits burst the ship down in the gap. Or the player stops moving to
finish a kill and takes steady chip damage. Standing still can still pay,
because a kill restores player hull (the `minionKillHeal` and `rivalKillHeal`
fields on `GameHost`). The owner usually picks a weakened rival for its larger
heal, and reports that no single close save stands out among many. The heal
also rewards finishing a target from the front, which pulls against spending
time on a flank, a push or terrain.

Faction reactions after a rival falls are ruled on #1589. In play the owner
reports that factions mostly run, and a Bastion last stand changes little the
player can feel, because the charge tactic is point blank fire and the minions
cannot ram.

## Draft acceptance maneuvers, unruled

Four maneuvers state what the candidate direction in [[OVERVIEW]] would have
to make reliable. One already happened. The player found that a concussion
mine can shove an enemy into ramming range, a use the systems allowed before
anyone named it. Three are still fantasies the current fight blocks:

- Reach the end of a Bulwark line and fire a railgun down the line. Ships that
  fly straight at the player leave no line to flank.
- Blink past an enemy in Crimson and fire Rippers into it while it turns.
  Today this works only on ships that turn slower than Crimson. Whether that
  limit is the skill or a defect is open.
- Shed a missile by flying through a tight gap in the asteroids.

A draft test for a future slice follows. The direction is working when a
player can do each maneuver on purpose, and when circling at one radius while
firing on cooldown stops being enough.

## Enemy phrases, a research direction, unruled

An enemy phrase is a timed enemy behavior pattern the player can read and
answer. It needs a tell the player can spot at its start, teeth that make it
dangerous in a new way while it runs, and a window where the enemy has
committed and the player can exploit the commitment. A tactic is one ship's
pick. A phrase can span several ships and several seconds. How a phrase would
sit above tactic picks, tempers and the MPC is the open research question, and
[[AI Pilots]] records why enemy behavior is the place to look for something
new. Nothing here is a spec, and examples in discussion (a Bulwark line that
holds long enough to flank, a ship that cuts across the player's circle, a
ram capable leader screened by its minions) are prompts for that research,
not picks.

## Open questions
- What “escape” and “interdiction” tools exist (for player and AI), and how do they interact with Newtonian flight and hazards?
- How do we prevent “attacking is always optimal” without making enemies feel passive?
- How would an enemy phrase compose with tactic picks, tempers and the MPC, and who owns coordination between ships?
