# Concussion Grenade

Slave I-style concussion charge (AotC seismic charge): a drop-behind fused
charge whose detonation is a **true expanding wavefront** — a damage frontier
that sweeps outward and hits each target once as it reaches them. Not a mine
(no persistence, no proximity trap); real mines remain a possible future
weapon.

Design settled by grill 2026-07-09. Roadmap memory:
`project_weapon_types_roadmap.md`.

## Decisions (grill outcomes)

| Axis | Decision |
|---|---|
| Slot | Ordinary weapon in the existing Primary/Secondary pool — no utility slot |
| Deploy | Drops from the mount with shooter velocity minus a backward push; heavy linear damping parks it near the drop point in ~1s |
| Detonation | Timed fuse, OR contact with any damageable after an arming delay, OR being shot (`IDamageable`, like Missile) |
| Blast | Expanding wavefront: radius grows at `expandSpeed` up to `maxRadius`; each `IDamageable` is damaged once, when first overlapped |
| Falloff & push | Damage scales linearly with frontier radius at sweep time (full at center → 0 at `maxRadius`); swept Rigidbodies get an outward impulse scaled the same way |
| Who it hits | Everything — **full self-damage** (no `IsFriendly` exemption; the dropper outruns the wave or eats it), all IDamageables including missiles and other grenades (chain detonations), no occlusion |
| Asteroid bonus | **Deliberately absent in v1.** Deferred to the DamageInfo typing framework (board card "Damage typing (DamageInfo) + asteroid damage multiplier"); no local multiplier hack |
| AI | `ShouldFire`: target behind (`angleToTarget >= minDropAngle`) and within `dropRange`. LOS ignored — `LosCache` short-circuits to false beyond 15°, so a behind-target never has "LOS" |

## Pieces

- `Combat/Projectiles/Grenade.cs` — `Projectile<Grenade>, IDamageable`; drop
  velocity, fuse/arming timers, detonation spawns the wave. `Configure(fuse,
  arming)` is the deterministic test seam (Missile.Configure precedent).
- `Combat/Projectiles/ConcussionWave.cs` — pooled, **not** a projectile.
  `Begin(attacker)` resets state; `FixedUpdate` grows the radius and sweeps
  `OverlapSphereNonAlloc`, deduping via a per-instance hit set; releases
  itself at `maxRadius`. `Falloff(radius, maxRadius)` is a pure static for
  EditMode tests.
- `Combat/Projectiles/Visual/ConcussionWaveVisual.cs` — LineRenderer ring
  (unit circle in the game plane, transform scale = frontier radius). The
  ring **is** the readable damage boundary, so it ships with the mechanics,
  not as later polish. Center burst reuses the missile explosion `PooledVFX`.
- `Combat/Weapons/Grenades.cs` — `WeaponBase<Grenade>`, semi-auto,
  `ProjectileSpeed` 0 (AI aims at present position; irrelevant for a drop),
  behind-and-close `ShouldFire`.
- Prefabs: `Grenades` (weapon: Rounds 3 + 12s auto-reload, Cooldown 1/s),
  `GrenadeCharge` (projectile, Missile layer — shootable/chainable like a
  missile), `ConcussionWave`. Grenade added to `PlayerLoadout.asset` weapons.

## Deferred / polish backlog

- Asteroid bonus damage → DamageInfo typing framework (board card).
- Launch/detonation audio (the iconic delayed *BWAAAH*), fuse-blink telegraph
  on the charge, ring material/gradient styling.
- True mines (persistent proximity traps), remote detonation variant.
