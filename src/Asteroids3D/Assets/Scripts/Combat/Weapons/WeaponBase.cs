using Balance;
using System;
using Combat.Projectiles;
using UnityEngine;
using Utils;
using Substrate.Services.Projectiles;

namespace Combat.Weapons
{
    public abstract class WeaponBase<TProj> : WeaponComponent where TProj : ProjectileBase
    {
        [Header("Launcher Settings")]
        [Stat, SerializeField] internal TProj projectilePrefab;

        protected override void Awake()
        {
            base.Awake();
            SimplePool<TProj>.Warm(projectilePrefab);
        }

        public override ProjectileBase Fire(IProjectileService projectiles)
        {
            // An untracked projectile could outlive its context; a deliberate null is refused before conditions consume charge/ammo.
            if (projectiles == null) throw new ArgumentNullException(nameof(projectiles));
            if (!CanFire()) return null;

            foreach (var condition in conditions)
                condition.ProcessFire();

            var proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
            proj.Initialize(shooter);
            projectiles.Register(proj, proj.ReturnToPoolImmediate);
            proj.Launch(firePoint.up);
            InvokeOnFire();

            return proj;
        }
    }
}
