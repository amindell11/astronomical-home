using Combat.Conditions;
using Combat.Projectile;

namespace Combat.Weapons
{
    public partial class WeaponLaser : WeaponBase<LaserProjectile>
    {
        public float ProjectileSpeed => projectilePrefab.LaserSpeed;

        private Heat heat;

        protected override void Awake()
        {
            base.Awake();
            heat = GetComponent<Heat>();
        }
    }
} 