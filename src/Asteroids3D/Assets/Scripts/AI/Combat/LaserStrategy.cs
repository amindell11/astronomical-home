using Combat.Conditions;
using Combat.Weapons;
using UnityEngine;

namespace AI.Combat
{
    [RequireComponent(typeof(WeaponLaser))]
    public class LaserStrategy : MonoBehaviour, IWeaponStrategy
    {
        private WeaponLaser laserGun;
        private Heat _heat;
    
        public int Priority => 5;

        private void Awake()
        {
            laserGun = GetComponent<WeaponLaser>();
            _heat = GetComponent<Heat>();
        }

        public bool ShouldFire(IWeaponStrategy.TargetingContext context)
        {
            if (!laserGun || !_heat || !context.HasLineOfSight) return false;

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
