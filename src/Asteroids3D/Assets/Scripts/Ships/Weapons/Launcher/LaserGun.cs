using Ships.Weapons.Conditions;
using Weapons;

namespace Ships.Weapons.Launcher
{
    /// <summary>
    /// Concrete weapon that fires pooled <see cref="LaserProjectile"/> instances.
    /// All common launcher logic lives in <see cref="LauncherBase{TProj}"/>.
    /// This weapon uses a heat system for ammo.
    /// </summary>
    public partial class LaserGun : LauncherBase<LaserProjectile>
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