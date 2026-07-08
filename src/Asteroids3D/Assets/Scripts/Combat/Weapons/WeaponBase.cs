using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Conditions;
using Combat.Projectile;
using Combat.Targeting;
using UnityEngine;
using Utils;

namespace Combat.Weapons
{
    /// <summary>
    /// Contextual data about a target used for weapon firing decisions
    /// (the input to <see cref="WeaponComponent.ShouldFire"/>).
    /// </summary>
    public struct TargetingContext
    {
        public Vector2 targetPosition;
        public float distanceToTarget;
        public float angleToTarget;
        public bool hasLineOfSight;
    }

    public abstract class WeaponComponent : MonoBehaviour
    {
        [SerializeField] public Transform firePoint;
        protected IShooter shooter;
        protected WeaponCondition[] conditions;
        
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

        /// <summary>Muzzle speed of this weapon's projectile, used for AI intercept lead. 0 if not applicable.</summary>
        public virtual float ProjectileSpeed => 0f;

        /// <summary>
        /// Whether the weapon repeats while the trigger is held (full-auto, paced by its cooldown),
        /// or fires once per trigger press (semi-auto). Cadence/rate is owned by the weapon's
        /// conditions (e.g. <c>Cooldown</c>) either way; this only governs how a held human trigger
        /// is interpreted. AI fire is unaffected — it is paced by the cooldown regardless.
        /// </summary>
        public virtual bool AutoFire => true;

        public virtual bool CanFire()
        {
            return conditions.All(c => c.CanFire());
        }

        /// <summary>
        /// This weapon's conditions (cached at Awake). Lets consumers (e.g. HUD generation)
        /// discover a weapon's gauges from what it actually carries instead of casting to
        /// concrete weapon classes.
        /// </summary>
        public IReadOnlyList<WeaponCondition> Conditions =>
            conditions ?? Array.Empty<WeaponCondition>();

        /// <summary>The lock-state source driving this weapon's guidance UI, or null if it has none.</summary>
        public virtual ILockStateSource LockSource => null;

        /// <summary>
        /// Determines if this weapon should fire based on the given targeting context.
        /// Override in derived classes to provide weapon-specific AI firing logic.
        /// </summary>
        public virtual bool ShouldFire(TargetingContext context)
        {
            return false; // Default: don't fire
        }

        public virtual void Reset()
        {
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
            proj.Launch(firePoint.up);
            InvokeOnFire();

            return proj;
        }
    }
}
