using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Conditions;
using Combat.Projectile;
using Combat.Targeting;
using Game.Services;
using UnityEngine;
using Utils;

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

    /// <summary>Marker for a weapon's displayable state contracts (heat, ammo, lock…) surfaced via <see cref="WeaponComponent.Readouts"/>.</summary>
    public interface IWeaponReadout
    {
    }

    public abstract class WeaponComponent : MonoBehaviour
    {
        [SerializeField] public Transform firePoint;
        [Tooltip("Name shown on this weapon's HUD readout panel. Empty = the prefab name.")]
        [SerializeField] private string displayName;
        protected IShooter shooter;
        protected IProjectileService projectiles;
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

        public abstract ProjectileBase Fire();

        /// <summary>Injected registry for fired projectiles; null-tolerant — an unwired weapon fires fine, just unregistered.</summary>
        public void SetProjectiles(IProjectileService service)
        {
            projectiles = service;
        }

        /// <summary>Muzzle speed of this weapon's projectile, used for AI intercept lead. 0 if not applicable.</summary>
        public virtual float ProjectileSpeed => 0f;

        /// <summary>Max distance at which an AI gunner engages with this weapon, for diagnostics/telemetry. 0 if not distance-gated.</summary>
        public virtual float FireRange => 0f;

        /// <summary>Full-auto repeats while held; semi-auto fires once per press. Only this weapon's <see cref="HandleTrigger"/> interprets it.</summary>
        public virtual bool AutoFire => true;

        /// <summary>Applies one step of trigger state; the weapon owns its firing semantics (charge weapons override to fire on release/full charge).</summary>
        public virtual void HandleTrigger(bool pressed, bool held)
        {
            if (AutoFire ? held : pressed)
                Fire();
        }

        public virtual bool CanFire()
        {
            return conditions.All(c => c.CanFire());
        }

        /// <summary>Name shown on this weapon's HUD readout panel.</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName)
            ? name.Replace("(Clone)", string.Empty).Trim()
            : displayName;

        /// <summary>Hangar hover stat line; read off the prefab asset where Awake never runs, so overrides must use only serialized state.</summary>
        public virtual string HangarStats => DisplayName;

        /// <summary>Displayable state (readout conditions + lock source), built lazily post-Awake; pre-Awake returns empty WITHOUT caching.</summary>
        public IReadOnlyList<IWeaponReadout> Readouts
        {
            get
            {
                if (readouts != null) return readouts;
                if (conditions == null) return Array.Empty<IWeaponReadout>();

                readouts = new List<IWeaponReadout>();
                foreach (var condition in conditions)
                    if (condition is IWeaponReadout readout)
                        readouts.Add(readout);
                if (LockSource != null)
                    readouts.Add(LockSource);
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

    public abstract class WeaponBase<TProj> : WeaponComponent where TProj : ProjectileBase
    {
        [Header("Launcher Settings")]
        [SerializeField] internal TProj projectilePrefab;

        protected override void Awake()
        {
            base.Awake();
            if (projectilePrefab)
                SimplePool<TProj>.Warm(projectilePrefab);
        }
        
        public override bool CanFire() => projectilePrefab && base.CanFire();
        
        public override ProjectileBase Fire()
        {
            if (!CanFire()) return null;

            foreach (var condition in conditions)
                condition.ProcessFire();

            var proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
            proj.Initialize(shooter);
            projectiles?.Register(proj, proj.ReturnToPoolImmediate);
            proj.Launch(firePoint.up);
            InvokeOnFire();

            return proj;
        }
    }
}
