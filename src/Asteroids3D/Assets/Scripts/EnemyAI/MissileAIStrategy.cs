using UnityEngine;
using Weapons;

namespace EnemyAI
{
    [RequireComponent(typeof(MissileLauncher))]
    public class MissileAIStrategy : MonoBehaviour, IWeaponAIStrategy
    {
        private MissileLauncher _missileLauncher;

        public int Priority => 10; // High priority for locked missiles

        private void Awake()
        {
            _missileLauncher = GetComponent<MissileLauncher>();
        }

        public bool ShouldFire(IWeaponAIStrategy.TargetingContext context)
        {
            if (!_missileLauncher) return false;
            
            if (_missileLauncher.AmmoCount <= 0) return false;

            switch (_missileLauncher.State)
            {
                case MissileLauncher.LockState.Locked:
                    return true;
                case MissileLauncher.LockState.Idle:
                case MissileLauncher.LockState.Locking:
                    // Dumb-fire at very close targets, mimicking original AIGunner logic
                    const float dummyMissileRange = 10f;
                    const float missileAngleTolerance = 15f;
                    return context.DistanceToTarget <= dummyMissileRange && context.AngleToTarget <= missileAngleTolerance;
                default:
                    return false;
            }
        }
    }
}
