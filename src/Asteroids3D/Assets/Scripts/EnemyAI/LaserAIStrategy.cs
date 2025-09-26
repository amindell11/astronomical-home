using UnityEngine;
using Weapons;

namespace EnemyAI
{
    [RequireComponent(typeof(LaserGun))]
    public class LaserAIStrategy : MonoBehaviour, IWeaponAIStrategy
    {
        private LaserGun _laserGun;
    
        public int Priority => 5; // Lower priority than a locked missile

        private void Awake()
        {
            _laserGun = GetComponent<LaserGun>();
        }

        public bool ShouldFire(IWeaponAIStrategy.TargetingContext context)
        {
            if (!_laserGun || !context.HasLineOfSight) return false;

            // These constants are from the original AIGunner
            const float fireDistance = 20f;
            const float fireAngleTolerance = 5f;

            bool isReadyToFire = _laserGun.CurrentHeat < _laserGun.MaxHeat - _laserGun.HeatPerShot;
            if (!isReadyToFire) return false;
            
            bool isInRange = context.DistanceToTarget <= fireDistance;
            bool isInAngle = context.AngleToTarget <= fireAngleTolerance;

            return isInRange && isInAngle;
        }
    }
}
