using Ships.Weapons.Conditions;
using UnityEngine;
using LaserGun = Ships.Weapons.Launcher.LaserGun;

namespace AI.Combat
{
    [RequireComponent(typeof(LaserGun))]
    public class LaserStrategy : MonoBehaviour, IWeaponStrategy
    {
        private LaserGun _laserGun;
        private Heat _heat;
    
        public int Priority => 5;

        private void Awake()
        {
            _laserGun = GetComponent<LaserGun>();
            _heat = GetComponent<Heat>();
        }

        public bool ShouldFire(IWeaponStrategy.TargetingContext context)
        {
            if (!_laserGun || !_heat || !context.HasLineOfSight) return false;

            var isReadyToFire = !_heat.WouldOverheatOnNextShot();
            if (!isReadyToFire) return false;
            
            const float fireDistance = 20f;
            const float fireAngleTolerance = 5f;
            
            var isInRange = context.DistanceToTarget <= fireDistance;
            var isInAngle = context.AngleToTarget <= fireAngleTolerance;

            return isInRange && isInAngle;
        }
    }
}
