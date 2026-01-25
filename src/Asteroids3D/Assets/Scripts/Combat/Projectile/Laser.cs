using UnityEngine;

namespace Combat.Projectile
{
    public class LaserProjectile : Projectile<LaserProjectile>
    {
        [Header("Laser Properties")]
        [SerializeField] private float laserSpeed = 20f;

        public float LaserSpeed => laserSpeed;

        public override void Launch(Vector3 direction)
        {
            var shooterVelocity = Shooter?.Velocity ?? Vector3.zero;
            var inheritAlong = Vector3.Project(shooterVelocity, direction);
            rb.linearVelocity = direction * laserSpeed + inheritAlong;
            base.Launch(direction);
        }
    }
}
