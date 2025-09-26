using Editor;
using UnityEngine;
using System;

namespace Weapons
{
    // One abstract, non-generic root that Unity can serialize
    public abstract class WeaponComponent : MonoBehaviour
    {
        [SerializeField] public    Transform firePoint;
        [SerializeField] protected float     fireRate = 0.2f;

        public event Action OnFire;
        public abstract ProjectileBase Fire();
        public abstract bool CanFire();

        public abstract void Reset();
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
        [SerializeField] internal TProj     projectilePrefab;

        protected float NextFireTime;
        protected IShooter Shooter;

        protected virtual void Awake()
        {
            Shooter = GetComponentInParent<IShooter>();
            if (!firePoint) firePoint = transform;
        }

        public override bool CanFire()
        {
            return projectilePrefab && Time.time >= NextFireTime;
        }

        public override ProjectileBase Fire()
        {
            if (!CanFire()) return null;

            NextFireTime = Time.time + fireRate;
            var proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
            proj.Initialize(Shooter);
            InvokeOnFire();

            return proj;
        }
    }
}