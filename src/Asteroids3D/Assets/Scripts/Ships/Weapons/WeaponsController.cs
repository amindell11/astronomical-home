using Balance;
using System;
using System.Collections.Generic;
using AI.Scanning;
using Combat;
using Combat.Targeting;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Loot;
using Movement;
using Ships.Command;
using Ships.Loadout;
using UnityEngine;
using Substrate.Services.Projectiles;

namespace Ships.Weapons
{
    [DefaultExecutionOrder(-95)]
    public class WeaponsController : MonoBehaviour
    {
        [Stat, SerializeField] internal WeaponComponent primaryMount;
        [Stat, SerializeField] internal WeaponComponent secondaryMount;

        /// <summary>A slot's mount point on the hull: the weapon instantiates here and fires from it.</summary>
        [Serializable]
        internal struct Hardpoint
        {
            public WeaponSlot slot;
            public Transform mount;
        }

        [Tooltip("Per-slot mount transforms on the hull. A slot with no entry falls back to this transform.")]
        [SerializeField] private Hardpoint[] hardpoints;

        public WeaponComponent Primary { get; private set; }
        public WeaponComponent Secondary { get; private set; }

        public bool EnginesCut => (Primary && Primary.CutsEngines) || (Secondary && Secondary.CutsEngines);

        /// <summary>The equipped weapon prefabs (the loadout's weapon modules; see ShipLoadout).</summary>
        public WeaponComponent PrimaryMountPrefab => primaryMount;
        public WeaponComponent SecondaryMountPrefab => secondaryMount;

        /// <summary>The lock-on sensor carried by a mounted weapon (null when none); kept current across <see cref="Reequip"/> so consumers never scan the hierarchy.</summary>
        public LockOnSensor Sensor { get; private set; }

        public event Action Fired;

        private WeaponContext context;
        private LullModStack mods;

        private void Awake()
        {
            Primary = ReplaceMount(null, primaryMount, WeaponSlot.Primary);
            Secondary = ReplaceMount(null, secondaryMount, WeaponSlot.Secondary);
            Sensor = FindMountSensor();
        }

        /// <summary>Mount swap (null = empty slot): unchanged slots keep their live instance, changed slots re-instantiate and the context refreshes in place so held references stay valid.</summary>
        public void Reequip(WeaponComponent newPrimary, WeaponComponent newSecondary)
        {
            var changed = false;

            if (newPrimary != primaryMount)
            {
                primaryMount = newPrimary;
                Primary = ReplaceMount(Primary, newPrimary, WeaponSlot.Primary);
                changed = true;
            }
            if (newSecondary != secondaryMount)
            {
                secondaryMount = newSecondary;
                Secondary = ReplaceMount(Secondary, newSecondary, WeaponSlot.Secondary);
                changed = true;
            }

            if (changed)
            {
                context?.Refresh();
                Sensor = FindMountSensor();
                HandMods();
            }
        }

        /// <summary>Puts the run's weapon mods in force on the mounts and keeps them in force across <see cref="Reequip"/>; the ship calls it on bind and on every change of the stack.</summary>
        public void SetMods(LullModStack stack)
        {
            mods = stack;
            HandMods();
        }

        private void HandMods()
        {
            if (Primary) Primary.SetMods(mods?.For(Primary, primaryMount) ?? WeaponMods.None);
            if (Secondary) Secondary.SetMods(mods?.For(Secondary, secondaryMount) ?? WeaponMods.None);
        }

        public bool Fits(WeaponMod mod) => mod.Fits(Primary, primaryMount) || mod.Fits(Secondary, secondaryMount);

        /// <summary>The mounted weapons <paramref name="mod"/> lands on, by display name, for the pick card.</summary>
        public string LandingNames(WeaponMod mod)
        {
            var primary = mod.Fits(Primary, primaryMount) ? Primary.DisplayName : null;
            var secondary = mod.Fits(Secondary, secondaryMount) ? Secondary.DisplayName : null;
            if (primary != null && secondary != null)
                return primary == secondary ? primary : $"{primary} · {secondary}";
            return primary ?? secondary ?? string.Empty;
        }

        /// <summary>The only firing surface: producing an <see cref="IWeapons"/> actuator demands the live-projectile registry, so a fire path without one cannot compile. The actuator reads mounts through this controller, staying current across <see cref="Reequip"/>.</summary>
        public IWeapons Arm(IProjectileService projectiles) => new ArmedWeapons(this, projectiles);

        private sealed class ArmedWeapons : IWeapons
        {
            private readonly WeaponsController owner;
            private readonly IProjectileService projectiles;

            public ArmedWeapons(WeaponsController owner, IProjectileService projectiles)
            {
                this.owner = owner;
                this.projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles),
                    $"{owner.name}: arming weapons requires the live-projectile registry (composition supplies it at spawn).");
            }

            public void Fire(WeaponSlot slot, in WeaponCommand cmd)
            {
                if (!owner || !owner.enabled) return;
                var mount = owner.Mount(slot);
                if (!mount) return;
                if (cmd.reload) mount.Reload();
                mount.HandleTrigger(in cmd, projectiles);
            }
        }

        private void RaiseFired() => Fired?.Invoke();

        private WeaponComponent ReplaceMount(WeaponComponent current, WeaponComponent prefab, WeaponSlot slot)
        {
            if (current)
            {
                // Detach before the deferred Destroy so same-frame hierarchy queries can't find the outgoing mount.
                current.OnFire -= RaiseFired;
                current.transform.SetParent(null);
                Destroy(current.gameObject);
            }

            if (!prefab) return null;
            var mounted = Instantiate(prefab, ResolveMount(slot));
            mounted.OnFire += RaiseFired;
            return mounted;
        }

        /// <summary>The first mounted weapon's lock sensor (mount-scoped; sensors ride the mounts).</summary>
        private LockOnSensor FindMountSensor()
        {
            if (Primary && Primary.TryGetComponent(out LockOnSensor primarySensor)) return primarySensor;
            if (Secondary && Secondary.TryGetComponent(out LockOnSensor secondarySensor)) return secondarySensor;
            return null;
        }

        /// <summary>The authored mount for a slot, or this transform (ship origin) when none is set.</summary>
        internal Transform ResolveMount(WeaponSlot slot) => ResolveMount(hardpoints, slot, transform);

        /// <summary>Pure slot→mount lookup: the matching hardpoint's mount, else <paramref name="fallback"/>.</summary>
        internal static Transform ResolveMount(Hardpoint[] hardpoints, WeaponSlot slot, Transform fallback)
        {
            if (hardpoints != null)
                foreach (var hp in hardpoints)
                    if (hp.slot == slot && hp.mount)
                        return hp.mount;
            return fallback;
        }

        /// <summary>Supplies the ship pose used to build each slot's firing solution.</summary>
        public void Initialize(Func<Kinematics> pose)
        {
            context = new WeaponContext(this, pose);
        }

        /// <summary>The slot-keyed read view handed to armed commanders.</summary>
        public IWeaponContext Context => context;

        /// <summary>The slot-keyed display view handed to the HUD (same object, UI-facing surface).</summary>
        public IWeaponReadouts ReadoutContext => context;

        private WeaponComponent Mount(WeaponSlot slot) => slot switch
        {
            WeaponSlot.Primary => Primary,
            WeaponSlot.Secondary => Secondary,
            _ => null,
        };

        public void ResetSystem()
        {
            Primary?.Reset();
            Secondary?.Reset();
            Sensor?.ResetLock();
        }

        public void OnShipDeath()
        {
            ResetSystem();
        }

        /// <summary>Slot-keyed read view over the mounts; one ship-lifetime instance, refreshed in place on reequip so consumers hold the object, never per-mount state.</summary>
        private sealed class WeaponContext : IWeaponContext, IWeaponReadouts
        {
            private readonly WeaponsController owner;
            private readonly Func<Kinematics> pose;
            private Gunsight primarySight;
            private Gunsight secondarySight;
            private readonly List<WeaponSlot> slots = new(2);

            public WeaponContext(WeaponsController owner, Func<Kinematics> pose)
            {
                this.owner = owner;
                this.pose = pose;
                Refresh();
            }

            /// <summary>Rebuild the per-slot state from the currently mounted weapons.</summary>
            public void Refresh()
            {
                slots.Clear();
                primarySight = null;
                secondarySight = null;

                if (owner.Primary)
                {
                    primarySight = new Gunsight(owner.Primary, pose);
                    slots.Add(WeaponSlot.Primary);
                }
                if (owner.Secondary)
                {
                    secondarySight = new Gunsight(owner.Secondary, pose);
                    slots.Add(WeaponSlot.Secondary);
                }
            }

            public IReadOnlyList<WeaponSlot> Slots => slots;

            public bool IsReady(WeaponSlot slot) => owner.Mount(slot)?.CanFire() ?? false;

            public float ProjectileSpeed(WeaponSlot slot) => owner.Mount(slot)?.ProjectileSpeed ?? 0f;

            public Vector2 AimPoint(WeaponSlot slot, in Kinematics shooter, Vector2 targetPos, Vector2 targetVel)
            {
                var mount = owner.Mount(slot);
                return mount ? mount.AimPoint(in shooter, targetPos, targetVel) : targetPos;
            }

            public float? HoldRange(WeaponSlot slot)
            {
                var mount = owner.Mount(slot);
                return mount ? mount.HoldRange : null;
            }

            public int KeepOutCircles(WeaponSlot slot, Span<DetectedObstacle.PlaneCircle> circles)
            {
                var mount = owner.Mount(slot);
                return mount ? mount.KeepOutCircles(circles) : 0;
            }

            public Gunsight Sight(WeaponSlot slot) => slot switch
            {
                WeaponSlot.Primary => primarySight,
                WeaponSlot.Secondary => secondarySight,
                _ => null,
            };

            public string DisplayName(WeaponSlot slot) => owner.Mount(slot)?.DisplayName ?? string.Empty;

            public IReadOnlyList<IWeaponReadout> Readouts(WeaponSlot slot) =>
                owner.Mount(slot)?.Readouts ?? System.Array.Empty<IWeaponReadout>();
        }
    }
}
