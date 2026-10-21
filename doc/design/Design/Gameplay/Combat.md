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
- Missiles have fixed ammo and do not replenish (unless designed otherwise later).

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
- Piloted by a **trained RL policy** (velocity commands + fire/boost) executed through the
  **MPC feasibility tracker** (velocity tracking, intercept aim, obstacle avoidance).
- Tactics (attack/flee/orbit/kite) come from the policy's training, not authored rules.

## Open questions
- What “escape” and “interdiction” tools exist (for player and AI), and how do they interact with Newtonian flight and hazards?
- How do we prevent “attacking is always optimal” without making enemies feel passive?
