using System;
using System.Linq;
using Combat.Conditions;
using Combat.Projectile;
using UnityEngine;
using Utils;

namespace Combat.Weapons
{
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

        public virtual bool CanFire()
        {
            return conditions.All(c => c.CanFire());
        }

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
