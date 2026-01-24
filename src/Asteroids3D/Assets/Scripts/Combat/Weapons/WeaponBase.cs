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

        protected WeaponCondition[] conditions;
        
        public event Action OnFire;

        protected virtual void Awake()
        {
            conditions = GetComponents<WeaponCondition>();
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
        protected void InvokeOnFire()
        {
            OnFire?.Invoke();
        }
    }

    public abstract class WeaponBase<TProj> : WeaponComponent where TProj : ProjectileBase
    {
        [Header("Launcher Settings")]
        [SerializeField] internal TProj projectilePrefab;

        protected IShooter shooter;

        protected override void Awake()
        {
            base.Awake();
            shooter = GetComponentInParent<IShooter>();
            if (!firePoint) firePoint = transform;
        }

        public override bool CanFire()
        {
            return projectilePrefab && base.CanFire();
        }

        public override ProjectileBase Fire()
        {
            if (!CanFire()) return null;
            
            foreach (var condition in conditions)
                condition.ProcessFire();
            
            var proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
            proj.Initialize(shooter);
            InvokeOnFire();

            return proj;
        }
    }
}