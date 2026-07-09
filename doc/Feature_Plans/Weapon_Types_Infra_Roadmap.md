# Weapon Types — Infra-First Roadmap

Goal: implement the unbuilt weapon types from the design docs
(`Design/Gameplay/Weapons.md`, `Combat.md`, Proposal F1) by landing the enabling
infrastructure first, with one or two new weapons riding along in each PR as
proof that the seam works.

Built today: `Lasers` (heat/overheat) and `Missiles` (lock-on, PN homing, ammo,
splash). The weapon architecture's clean seams — `WeaponBase<TProj>`, the
`WeaponCondition` composition (`Cooldown`/`Heat`/`Rounds`), and the per-weapon
AI virtuals (`ShouldFire(TargetingContext)`, `ProjectileSpeed`, `AutoFire`) —
mean most new "spawn a projectile" weapons are composition. What blocks new
types is a small set of cross-cutting hardcodings, each owned by one PR below.

---

## PR 1 — Modular weapon HUD + Ripper

**Infra: the HUD binds to what's equipped, not to Laser-in-Primary /
Missile-in-Secondary.**

`UI/Overlay.cs:49-61` hardcodes `player.Weapons?.Primary as Lasers` and
`Secondary as Missiles`. Equip anything else and its UI silently vanishes —
this blocks *every* new weapon, so it goes first.

Approach — **generate the HUD from the loadout, through narrow read contracts**
(design settled by grill 2026-07-08):

- **Read contracts on the sim**: each displayable condition exposes a nested,
  read-only, event-driven interface co-located with its sole implementer —
  `IHeatReadout` (Heat), `IAmmoReadout` (Rounds), plus the existing
  `ILockStateSource` — all tagged `IWeaponReadout`. `WeaponComponent` exposes
  `Readouts` (its readout-bearing conditions + `LockSource`) and `DisplayName`.
- **UI-facing surface**: `IWeaponReadouts` (slots, display name, per-slot
  readout list) is the HUD sibling of the commander-facing `IWeaponContext`,
  implemented by the same `WeaponsController` context object.
- **`HudBinding`**: `Overlay.Initialize` takes a readonly bundle
  (`IShipStatus`, `IDamageEvents`, `IWeaponReadouts`) assembled by `PlayerRig`
  — the UI's counterpart of `ShipControl`; `Ship` never enters the UI layer.
- **Per-weapon panels**: `WeaponReadoutBuilder` (on `HUDPanel`) instantiates a
  `WeaponReadoutPanel` per equipped slot (weapon display name + widget stack)
  and one widget per readout: `IHeatReadout` → `HeatGaugeUI`, `IAmmoReadout` →
  `AmmoCounterUI` (count + reload fill; missiles included — icon pips retired),
  `ILockStateSource` → `LockReadoutUI`. One widget type per readout type.
- Widgets are standalone prefabs under `Prefabs/UI/Readouts/`; the old
  `HeatBar`/`MissileAmmoContainer` overlay nodes are deleted.
- Audio stays two single overlay-level channels bound to the first matching
  readout in slot order (overheat → first `IHeatReadout`, lock → first
  `ILockStateSource`).
- **Deferred** (board card, Mid Dev Pool): `WeaponHudLibrary` SO — designer-
  chosen widget variants per weapon (e.g. pips vs counter) via a UI-side map,
  keyed by weapon prefab; no UI config on sim objects.
- Slot-count stays two; this PR does not touch `WeaponSlot`.

**Weapon: Ripper** — "regular gun that uses ammo, no overheat, fixed reload
time when you run out" (`Weapons.md`).

- **Reload**: extend `Rounds` with an optional auto-reload (`reloadTime`; 0 =
  never, which keeps missile behaviour identical). When ammo hits 0, a timer
  runs and refills the magazine; `CanFire()` false while reloading. Expose
  reload progress for the ammo widget. One condition class, no `Magazine`
  proliferation, and the PR-1 HUD shows it automatically.
- **Projectile**: reuse the `Laser` class with a slug-tuned prefab — the class
  is just "straight-line constant-velocity projectile," and pools are keyed by
  prefab, so no new class is needed. (Renaming `Laser` to a neutral name is a
  follow-up, foldable into the "Refactor Weapons Projectiles folder" board
  card; a distinct slug class is earned only if slug behaviour ever diverges.)
- **Weapon**: `Rippers : WeaponBase<Laser>` with `Cooldown` (fire rate) +
  `Rounds` (magazine + reload). `AutoFire => true`.
  `ShouldFire`: LoS + range + angle gate (clone of `Lasers`' policy, minus
  heat) — don't dump the magazine at nothing.
- **Assets**: Ripper weapon prefab + slug prefab + audio/visual (reuse laser
  VFX initially). Equip on a testbench ship for verification; **default ship
  loadouts unchanged** in this PR.

**Tests**: reload cycle (fires N, blocks, refills after `reloadTime`);
missile `Rounds` regression (reloadTime=0 never refills); HUD binder binds
heat/ammo by condition presence and survives a Ripper-in-Primary loadout;
existing Heat/Rounds/hardpoint tests stay green.

---

## PR 2 — Trigger release semantics + per-slot AI aim → Charge Laser + Railgun

**As built (2026-07-08):** `WeaponCommand` carries raw input facts — `held` (trigger down)
and `pressed` (press edge) — forwarded every step; `WeaponComponent.HandleTrigger` owns all
firing semantics (auto = fire on held, semi = fire on pressed, charge weapons override).
The AI truthfully "mashes" (`pressed = held = ShouldFire` each step), which preserves
semi-auto cooldown pacing under sustained AI intent while a human's semi stays
one-shot-per-click. `IsAutoFire` left `IWeaponContext`. `ChargeTime` implements the shared
charge interpreter (charge on held; fire on release ≥ min charge or auto at full — the AI
path) plus `IChargeReadout` → `ChargeGaugeUI` on the PR1 builder. `Gunner` aims per slot
with that slot's `ProjectileSpeed` (`<= 0` ⇒ hitscan, aim at present position). ChargeLasers
scale shot damage by charge via `ProjectileBase.SetDamageScale` (reset on pool return);
Railguns raycast, skipping the shooter's own hull (nearest non-own-root hit).

**Deferred — trigger-source injection (pull model):** the more principled endpoint is
injecting each weapon a trigger read-surface at Initialize (`ITrigger`-style, matching the
`Func<Kinematics> pose` idiom) and letting weapons poll — retiring the per-tick
`IWeapons.Fire` push entirely. Deliberately deferred: it breaks the push symmetry with the
pilot channel (RL action shape) and rewrites the dispatch tests for the same observable
behavior. Revisit if a third trigger-consuming feature (burst?) lands.

**Original sketch (superseded by the above):**

**Infra A: weapons see the trigger, not just fire ticks.**

Today `WeaponsController.Fire()` early-outs on `!cmd.fire`
(`WeaponsController.cs:64`), so a weapon can never observe *release* — charge
mechanics are impossible. Meanwhile `PlayerCommander.FireSlot` does rising-edge
detection *for* semi-auto weapons (`PlayerCommander.cs:110-115`), i.e. trigger
interpretation currently lives in the commander.

Change of ownership — **the weapon interprets the trigger**:

- `WeaponCommand.fire` becomes "trigger held this tick" (rename to `held` for
  honesty). `WeaponsController.Fire` forwards every command to the mount.
- `WeaponComponent` gets a small trigger front-end (e.g.
  `HandleTrigger(bool held)`) that detects edges internally and implements:
  auto → fire while held; semi-auto → fire on rising edge; charge → accumulate
  while held, fire on release/full (below). Existing `Fire()` stays the
  "actually shoot now" primitive.
- `PlayerCommander.FireSlot` collapses to pushing raw held state
  (`IsAutoFire` leaves the player path; it can stay on `IWeaponContext` for
  AI/UI if still useful).
- `Gunner` semantics unchanged: its per-slot decision becomes the held state.

`ChargeTime` (currently a dead stub, `Conditions/ChargeTime.cs`) becomes a real
`WeaponCondition`: charge level accumulates while the trigger is held,
`CanFire()` requires ≥ min charge, `ProcessFire()` consumes it. Proposed
release semantics: **player** fires on trigger release (partial charge allowed
above a minimum) or auto-fires at full charge; **AI** holds while
`ShouldFire` and auto-fires at full charge — no release-timing burden on the
AI.

**Infra B: AI aims each slot with that slot's ballistics.**

`Gunner.ApplyIntent` computes one intercept from `PrimaryProjectileSpeed` and
feeds it to every slot's `Gunsight` (`Gunner.cs:66`, board card "Smarter AI
aim-point selection per weapon"). Generalize:

- Gunner stores the raw enemy kinematics from the intent; `Fire()` computes a
  per-slot aim point with `weapons.ProjectileSpeed(slot)`.
- Convention: `ProjectileSpeed <= 0` ⇒ hitscan/no-lead ⇒ aim at the target's
  current position (matches the existing "0 if not applicable" doc on
  `WeaponComponent.ProjectileSpeed`).
- `Navigator`'s use of `PrimaryProjectileSpeed` for approach lead is untouched.

**Weapon: Charged Railgun** — hitscan (`Weapons.md`).

- First non-projectile weapon: subclasses `WeaponComponent` directly (not
  `WeaponBase<TProj>`), `Fire()` raycasts from `firePoint` along `firePoint.up`
  on the ship/asteroid mask, applies damage via `IDamageable`, returns null
  (callers ignore the return; verify tests). Beam flash visual + hit VFX.
- Conditions: `ChargeTime` + `Cooldown`. `ProjectileSpeed => 0` (hitscan lead
  convention). Full charge required to fire.
- `ShouldFire`: LoS + long range + tight angle.

**Weapon: Charge Laser** — "charge laser does more damage" (`Weapons.md`).

- `WeaponBase<Laser>` variant with `ChargeTime` (+ optionally `Heat`); scales
  the shot's damage by charge level at fire time. Needs a per-shot damage
  override on the spawned projectile (internal setter / `Configure` on
  `ProjectileBase`) — the same seam the later heat-damage perk uses.
- Player: release fires at current charge above minimum. HUD charge gauge =
  one new condition-bound widget on the PR-1 binder.

**Tests**: trigger semantics (auto held-repeat, semi rising-edge, charge
release/full-charge fire) via `IWeapons.Fire` streams; per-slot lead (two slots
w/ different speeds get different aim points; hitscan slot aims at present
position); railgun raycast damage + friendly-skip; charge-scaled damage;
existing `WeaponCommandDispatchPlayModeTests` updated for the forwarding
change.

---

## PR 3 — Weapons as first-class loadout slots (hangar prep)

**As built (2026-07-09, grill-settled):** weapons join the hangar loadout model with
**prefab-as-module** (the weapon prefab IS the module, matching the chassis slot's
`Ship[]` precedent; no `WeaponModule` SO — full stat/visual unification stays parked):

- `ShipLoadout` carries `PrimaryWeapon`/`SecondaryWeapon` prefab refs; `LoadoutConfig`
  gains **one shared** `weapons[]` pool (the two mounts are identical hardware).
- `Ship.Reequip(engine, shield, primaryWeapon, secondaryWeapon)` — one atomic
  whole-build apply. Weapons delegate to `WeaponsController.Reequip` (destroy mount
  instances → instantiate new at hardpoints → **refresh the `WeaponContext` in place**
  so commander/HUD-held references stay valid); the ship refreshes its `Targeting`
  cache (old mount detached before deferred destroy so same-frame queries can't find it).
- World wiring stays with the service: `IUnitService.WireShipDependencies(Ship)` is the
  spawn wiring made public + idempotent; `PlayerRig.ApplyLoadout` re-runs it (and
  re-binds the HUD) after applying — no per-ship holder of world state, no new seam.
- Loadout seeds from the controller's authored mount prefabs; null = unarmed (valid);
  weapon selection persists across chassis rebuilds like engine/shield.
- **Not in this PR:** hangar UI weapon rows (next PR — `HangarScreen` rows + prefab edit).

**Deferred:** relocate `LockOnSensor` to the hull ("sensor is hull equipment") — would
dissolve the re-wiring need entirely, but sensor↔missile coupling (`CanLock` gates on
`weapon.CanFire()`) makes it its own design exercise.

---

## Later (deliberately deferred — bigger systems)

In rough order of likely need; each gets its own plan when picked up:

- **Missile prefab variants** (speed/homing tuning) — pure data, can ride any
  PR. Cheap win whenever wanted.
- **Heat-damage perk** — trivial after PR 2's per-shot damage seam; blocked
  conceptually on a perk system existing at all.
- **Concussion mine** — deployed persistent object (new projectile lifecycle:
  arm delay, proximity trigger, lifetime), shared explosion utility extracted
  from `Missile.ApplySplashDamage`, damage typing for the asteroid bonus, AI
  "deterrence while fleeing" policy, and the weapon-slot vs Utility-slot
  design question (`Ships.md` puts mines in the unbuilt Utility slot).
- **Damage typing** (`DamageInfo` struct through `IDamageable.TakeDamage`) —
  needed by mine asteroid-bonus, hull-biased missiles, disabling missiles.
- **Flares + missile seeker rework** — missiles currently home on a `Transform`
  set once at launch and never re-evaluate; flares need a seeker/decoy model,
  `TargetLock` interplay, AI incoming-missile awareness, Utility slot. Do
  alongside Combat Depth §3/§4 (asteroid lock-break, interdiction tuning).
- **Sparrows** (missile swarm) — salvo spawn + lock distribution; its defining
  flare-resistance is meaningless until flares exist. After flares.
- **System-disabling missiles** — requires a status-effect framework on Ship;
  defer until more than one consumer motivates it.
- **Weapons Settings SO / WeaponModule** (board card) — data layer to make
  weapons loot/upgrade modules per `Ship Upgrades.md`; orthogonal, land when
  loot work starts.
- **N weapon slots** — `WeaponSlot{Primary,Secondary}` + two serialized fields
  stays frozen per `Ships.md` until more weapons exist to inform trigger
  routing.
