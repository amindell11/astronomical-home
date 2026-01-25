using UnityEngine;

namespace Combat.Projectile
{
    public class LaserProjectile : Projectile<LaserProjectile>
    {
        [Header("Laser Properties")]
        [SerializeField] private float laserSpeed = 20f;

        public float LaserSpeed => laserSpeed;

        public override void Initialize(IShooter shooter)
        {
            base.Initialize(shooter);

            if (!rb) return;
            var shooterVelocity = Shooter?.Velocity ?? Vector3.zero;
            var forward = transform.up;
            var inheritAlong = Vector3.Project(shooterVelocity, forward);
            rb.linearVelocity = forward * laserSpeed + inheritAlong;
        }
    }
}
