using Balance;
using Damage;
using Ships.Damage;
using Substrate;
using UnityEngine;

namespace Combat.Projectiles
{
    public class Laser : Projectile<Laser>
    {
        [Header("Laser Properties")]
        [Stat, SerializeField] private float laserSpeed = 20f;

        private bool banks;
        private Collider bankedOff;
        private bool passesOrdnance;
        private SuppressingFire suppressing;

        protected override DamageKind Kind => DamageKind.Laser;

        public float LaserSpeed => laserSpeed;

        public override void Launch(Vector3 direction, Vector3 targetPoint)
        {
            var shooterVelocity = Shooter?.Velocity ?? Vector3.zero;
            var inheritAlong = Vector3.Project(shooterVelocity, direction);
            rb.linearVelocity = direction * laserSpeed + inheritAlong;
            base.Launch(direction, targetPoint);
        }

        /// <summary>The next asteroid this bolt meets turns it, unharmed, instead of stopping it.</summary>
        public void BankOnce() => banks = true;

        /// <summary>The bolt flies through other shooters' missiles and charges instead of setting them off.</summary>
        public void PassOrdnance() => passesOrdnance = true;

        /// <summary>Counts this bolt's hit on a ship toward <paramref name="fire"/>'s steady fire.</summary>
        public void Suppress(SuppressingFire fire) => suppressing = fire;

        protected override void OnHit(IDamageable other, Collider collider)
        {
            // The rock it banked off still touches it on the step it turns.
            if (collider == bankedOff) return;
            if (banks && collider.gameObject.layer == LayerIds.Asteroid)
            {
                Bank(collider);
                return;
            }
            if (passesOrdnance && other is ProjectileBase ordnance && ordnance.Shooter != Shooter) return;
            if (suppressing != null && other is DamageController hull && hull.Ship)
                suppressing.Strike(hull.Ship.Status, Time.fixedTime);
            base.OnHit(other, collider);
        }

        private void Bank(Collider rock)
        {
            banks = false;
            bankedOff = rock;
            var traveled = DistanceTraveled;
            var normal = GamePlane.WorldDirOnPlane(transform.position - rock.bounds.center).normalized;
            var velocity = rb.linearVelocity;
            if (Vector3.Dot(velocity, normal) >= 0f) return;

            var turned = Vector3.Reflect(velocity, normal);
            rb.linearVelocity = turned;
            transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(velocity, turned, GamePlane.Normal), GamePlane.Normal)
                                 * transform.rotation;
            // The range cap measures from the start point, so move it to keep the path length.
            startPosition = transform.position - turned.normalized * traveled;
        }

        protected override void OnReturnToPool()
        {
            banks = false;
            bankedOff = null;
            passesOrdnance = false;
            suppressing = null;
        }
    }
}
