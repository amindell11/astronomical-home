using System;
using Asteroids;
using Balance;
using Combat.Tethers;
using Damage;
using Substrate;
using UnityEngine;

namespace Combat.Projectiles
{
    public class Harpoon : Projectile<Harpoon>
    {
        [Header("Harpoon")]
        [Stat, SerializeField, Min(0.01f)] private float launchSpeed = 50f;

        private int hookableLayers;

        public event Action<HookTarget, Vector3> Hooked;

        protected override DamageKind Kind =>
            throw new InvalidOperationException("A harpoon deals no damage, so it has no damage kind.");

        protected override void Awake()
        {
            base.Awake();
            hookableLayers = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid);
        }

        public override void Launch(Vector3 direction, Vector3 targetPoint)
        {
            var inheritAlong = Vector3.Project(Shooter.Velocity, direction);
            rb.linearVelocity = direction * launchSpeed + inheritAlong;
            base.Launch(direction, targetPoint);
        }

        protected override void OnHit(IDamageable other, Collider collider)
        {
            if ((hookableLayers & (1 << collider.gameObject.layer)) == 0 || !collider.attachedRigidbody)
                return;

            Hooked?.Invoke(new HookTarget(collider.attachedRigidbody, other as AsteroidDamage), transform.position);
            Dispose();
        }

        protected override void OnHit(IDamageable other) { }
    }
}
