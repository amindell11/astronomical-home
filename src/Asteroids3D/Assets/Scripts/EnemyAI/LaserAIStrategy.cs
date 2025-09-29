using UnityEngine;
using Weapons;
using Ships.Weapons.Conditions;

namespace EnemyAI
{
    [RequireComponent(typeof(LaserGun))]
    public class LaserAIStrategy : MonoBehaviour, IWeaponAIStrategy
    {
        private LaserGun _laserGun;
        private Heat _heat;
    
        public int Priority => 5; // Lower priority than a locked missile

        private void Awake()
        {
            _laserGun = GetComponent<LaserGun>();
            _heat = GetComponent<Heat>();
        }

        public bool ShouldFire(IWeaponAIStrategy.TargetingContext context)
        {
            if (!_laserGun || !_heat || !context.HasLineOfSight) return false;

            // Avoid firing if the next shot would cause overheat
            bool isReadyToFire = !_heat.WouldOverheatOnNextShot();
            if (!isReadyToFire) return false;
            
            // These constants are from the original AIGunner
            const float fireDistance = 20f;
            const float fireAngleTolerance = 5f;
            
            bool isInRange = context.DistanceToTarget <= fireDistance;
            bool isInAngle = context.AngleToTarget <= fireAngleTolerance;

            return isInRange && isInAngle;
        }
    }
}
