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
        private float bankedDamageScale = 1f;
        private Collider bankedOff;
        private bool intercepts;
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

        /// <summary>The next asteroid this bolt meets turns it, unharmed, instead of stopping it; the turned bolt's damage scales by <paramref name="damageScale"/>.</summary>
        public void BankOnce(float damageScale)
        {
            banks = true;
            bankedDamageScale = damageScale;
        }

        public void Intercept() => intercepts = true;

        protected override bool Intercepts => intercepts;

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
            SetDamageScale(DamageScale * bankedDamageScale);
        }

        protected override void OnReturnToPool()
        {
            banks = false;
            bankedDamageScale = 1f;
            bankedOff = null;
            intercepts = false;
            suppressing = null;
        }
    }
}
