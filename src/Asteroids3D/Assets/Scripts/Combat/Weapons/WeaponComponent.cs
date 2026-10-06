using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Projectiles;
using Combat.Targeting;
using UnityEngine;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;

namespace Combat.Weapons
{
    /// <summary>Target geometry fed to <see cref="WeaponComponent.InEnvelope"/>/<see cref="WeaponComponent.ShouldFire"/>.</summary>
    public struct TargetingContext
    {
        public Vector2 targetPosition;
        public float distanceToTarget;
        public float angleToTarget;
        public bool hasLineOfSight;
    }

    public abstract class WeaponComponent : MonoBehaviour
    {
        public Transform firePoint;
        [Tooltip("Name shown on this weapon's HUD readout panel. Empty = the prefab name.")]
        [SerializeField] private string displayName;
        protected IShooter shooter;
        protected WeaponCondition[] conditions;
        private List<IWeaponReadout> readouts;
        
        public event Action OnFire;

        protected virtual void Awake()
        {            
            shooter = GetComponentInParent<IShooter>();
            conditions = GetComponents<WeaponCondition>();
            if (!firePoint) firePoint = transform;
            foreach (var condition in conditions)
                condition.Initialize(this);
        }

        /// <summary>Fires one shot at <paramref name="targetPoint"/>. The live-projectile registry is a per-call capability, never stored — a call site without one in hand cannot compile.</summary>
        public abstract ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles);

        /// <summary>Muzzle speed of this weapon's projectile, used for AI intercept lead. 0 if not applicable.</summary>
        public virtual float ProjectileSpeed => 0f;

        /// <summary>Max distance at which an AI gunner engages with this weapon, for diagnostics/telemetry. 0 if not distance-gated.</summary>
        public virtual float FireRange => 0f;

        /// <summary>Full-auto repeats while held; semi-auto fires once per press. Only this weapon's <see cref="HandleTrigger"/> interprets it.</summary>
        public virtual bool AutoFire => true;

        /// <summary>Applies one step of trigger state; the weapon owns its firing semantics (charge weapons override to fire on release/full charge).</summary>
        public virtual void HandleTrigger(in WeaponCommand cmd, IProjectileService projectiles)
        {
            if (AutoFire ? cmd.held : cmd.pressed)
                Fire(cmd.targetPoint, projectiles);
        }

        public virtual bool CanFire()
        {
            return conditions.All(c => c.CanFire());
        }

        /// <summary>Name shown on this weapon's HUD readout panel.</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName)
            ? name.Replace("(Clone)", string.Empty).Trim()
            : displayName;

        /// <summary>Displayable state (readout conditions, lock source, the weapon's own readout), built lazily post-Awake; pre-Awake returns empty WITHOUT caching.</summary>
        public IReadOnlyList<IWeaponReadout> Readouts
        {
            get
            {
                if (readouts != null) return readouts;
                if (conditions == null) return Array.Empty<IWeaponReadout>();

                readouts = new List<IWeaponReadout>(conditions.OfType<IWeaponReadout>());
                if (LockSource != null)
                    readouts.Add(LockSource);
                if (this is IWeaponReadout self)
                    readouts.Add(self);
                return readouts;
            }
        }

        /// <summary>The lock-state source driving this weapon's guidance UI, or null if it has none.</summary>
        public virtual ILockStateSource LockSource => null;

        /// <summary>Geometric firing envelope only (distance/angle/LOS) — readiness (heat/charge/lock/ammo) excluded.</summary>
        public virtual bool InEnvelope(in TargetingContext context) => false;

        /// <summary>AI fire decision: the geometric envelope gated by this weapon's readiness.</summary>
        public virtual bool ShouldFire(TargetingContext context)
        {
            return false;
        }

        public virtual void Reset()
        {
            // Null before Awake (a mount instantiated under an inactive ship): nothing live to reset.
            if (conditions == null) return;
            foreach (var condition in conditions)
                condition.Reset();
        }
        // ReSharper disable Unity.PerformanceAnalysis
        protected void InvokeOnFire()
        {
            OnFire?.Invoke();
        }
    }
}
