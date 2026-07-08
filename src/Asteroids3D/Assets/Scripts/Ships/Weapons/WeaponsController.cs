using System;
using System.Collections.Generic;
using Combat;
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

        private WeaponContext context;

        private void Awake()
        {
            if (primaryMount) Primary = Instantiate(primaryMount, ResolveMount(WeaponSlot.Primary));
            if (secondaryMount) Secondary = Instantiate(secondaryMount, ResolveMount(WeaponSlot.Secondary));
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

        // ── IWeapons ──
        public void Fire(WeaponSlot slot, in WeaponCommand cmd)
        {
            if (!enabled || !cmd.fire) return;
            Mount(slot)?.Fire();
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
        }

        public void OnShipDeath()
        {
            ResetSystem();
        }

        /// <summary>Slot-keyed read view over the controller's mounts.</summary>
        private sealed class WeaponContext : IWeaponContext
        {
            private readonly WeaponsController owner;
            private readonly Gunsight primarySight;
            private readonly Gunsight secondarySight;
            private readonly List<WeaponSlot> slots = new(2);

            public WeaponContext(WeaponsController owner, Func<Kinematics> pose)
            {
                this.owner = owner;
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

            public bool IsAutoFire(WeaponSlot slot) => owner.Mount(slot)?.AutoFire ?? true;

            public float ProjectileSpeed(WeaponSlot slot) => owner.Mount(slot)?.ProjectileSpeed ?? 0f;

            public Gunsight Sight(WeaponSlot slot) => slot switch
            {
                WeaponSlot.Primary => primarySight,
                WeaponSlot.Secondary => secondarySight,
                _ => null,
            };
        }
    }
}
