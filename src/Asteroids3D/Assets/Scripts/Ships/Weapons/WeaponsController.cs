using System;
using System.Collections.Generic;
using Combat;
using Combat.Targeting;
using Combat.Weapons;
using Movement;
using Ships.Command;
using UnityEngine;

namespace Ships.Weapons
{
    [DefaultExecutionOrder(-95)]
    public class WeaponsController : MonoBehaviour, IWeapons
    {
        [SerializeField] internal WeaponComponent primaryMount;
        [SerializeField] internal WeaponComponent secondaryMount;

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

        /// <summary>The equipped weapon prefabs (the loadout's weapon modules; see ShipLoadout).</summary>
        public WeaponComponent PrimaryMountPrefab => primaryMount;
        public WeaponComponent SecondaryMountPrefab => secondaryMount;

        /// <summary>The lock-on sensor carried by a mounted weapon (null when none); kept current across <see cref="Reequip"/> so consumers never scan the hierarchy.</summary>
        public LockOnSensor Sensor { get; private set; }

        private WeaponContext context;

        private void Awake()
        {
            if (primaryMount) Primary = Instantiate(primaryMount, ResolveMount(WeaponSlot.Primary));
            if (secondaryMount) Secondary = Instantiate(secondaryMount, ResolveMount(WeaponSlot.Secondary));
            Sensor = FindMountSensor();
        }

        /// <summary>Between-run mount swap (null = empty slot): unchanged slots keep their live instance, changed slots re-instantiate and the context refreshes in place so held references stay valid.</summary>
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
            }
        }

        private WeaponComponent ReplaceMount(WeaponComponent current, WeaponComponent prefab, WeaponSlot slot)
        {
            if (current)
            {
                // Detach before the deferred Destroy so same-frame hierarchy queries
                // can't find the outgoing mount.
                current.transform.SetParent(null);
                Destroy(current.gameObject);
            }

            return prefab ? Instantiate(prefab, ResolveMount(slot)) : null;
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

        // ── IWeapons ──
        public void Fire(WeaponSlot slot, in WeaponCommand cmd)
        {
            if (!enabled) return;
            Mount(slot)?.HandleTrigger(cmd.pressed, cmd.held);
        }

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

            public Gunsight Sight(WeaponSlot slot) => slot switch
            {
                WeaponSlot.Primary => primarySight,
                WeaponSlot.Secondary => secondarySight,
                _ => null,
            };

            // ── IWeaponReadouts ──
            public string DisplayName(WeaponSlot slot) => owner.Mount(slot)?.DisplayName ?? string.Empty;

            public IReadOnlyList<IWeaponReadout> Readouts(WeaponSlot slot) =>
                owner.Mount(slot)?.Readouts ?? System.Array.Empty<IWeaponReadout>();
        }
    }
}
