using Editor;
using UnityEngine;
using System;
using System.Linq;
using Ships.Weapons.Conditions;

namespace Weapons
{
    // One abstract, non-generic root that Unity can serialize
    public abstract class WeaponComponent : MonoBehaviour
    {
        [SerializeField] public Transform firePoint;

        protected WeaponCondition[] conditions;
        
        public event Action OnFire;

        protected virtual void Awake()
        {
            conditions = GetComponents<WeaponCondition>();
            foreach (var condition in conditions)
            {
                condition.Initialize(this);
            }
        }

        public abstract ProjectileBase Fire();

        public virtual bool CanFire()
        {
            return conditions.All(c => c.CanFire());
        }

        public virtual void Reset()
        {
            foreach (var condition in conditions)
            {
                condition.Reset();
            }
        }
        protected void InvokeOnFire()
        {
            OnFire?.Invoke();
        }
    }
    /// <summary>
    /// Generic weapon/launcher base – spawns pooled projectiles of type <typeparamref name="TProj"/>.
    /// </summary>
    /// <typeparam name="TProj">Projectile component the launcher fires.</typeparam>
    public abstract class LauncherBase<TProj> : WeaponComponent where TProj : ProjectileBase
    {
        [Header("Launcher Settings")]
        [SerializeField] internal TProj projectilePrefab;

        protected IShooter Shooter;

        protected override void Awake()
        {
            base.Awake();
            Shooter = GetComponentInParent<IShooter>();
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
            {
                condition.ProcessFire();
            }

            var proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
            proj.Initialize(Shooter);
            InvokeOnFire();

            return proj;
        }
    }
}