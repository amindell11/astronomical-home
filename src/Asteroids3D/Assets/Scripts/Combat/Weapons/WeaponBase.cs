using Balance;
using System;
using Combat.Projectiles;
using Combat.Weapons.Mods;
using Substrate;
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

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            // An untracked projectile could outlive its context; a deliberate null is refused before conditions consume charge/ammo.
            if (projectiles == null) throw new ArgumentNullException(nameof(projectiles));
            if (!TrySpendFireConditions()) return null;

            var first = Launch(Muzzles[0], targetPoint, projectiles);
            for (var i = 1; i < Muzzles.Count; i++)
                Launch(Muzzles[i], targetPoint, projectiles);
            InvokeOnFire();

            return first;
        }

        private TProj Launch(Transform muzzle, Vector3 targetPoint, IProjectileService projectiles)
        {
            var proj = SimplePool<TProj>.Get(projectilePrefab, GamePlane.WorldPointOnPlane(muzzle.position), muzzle.rotation);
            proj.Initialize(shooter);
            proj.SetDamageScale(Mods.Factor(WeaponModField.Damage));
            projectiles.Register(proj, proj.ReturnToPoolImmediate);
            proj.Launch(GamePlane.WorldDirOnPlane(muzzle.up), targetPoint);
            return proj;
        }
    }
}
