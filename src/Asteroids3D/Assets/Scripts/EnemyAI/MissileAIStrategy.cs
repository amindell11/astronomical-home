using UnityEngine;
using Weapons;
using Ships.Weapons.Conditions;

namespace EnemyAI
{
    [RequireComponent(typeof(Missiles))]
    public class MissileAIStrategy : MonoBehaviour, IWeaponAIStrategy
    {
        private Missiles missiles;
        private Rounds rounds;

        public int Priority => 10; // High priority for locked missiles

        private void Awake()
        {
            missiles = GetComponent<Missiles>();
            rounds = GetComponent<Rounds>();
        }

        public bool ShouldFire(IWeaponAIStrategy.TargetingContext context)
        {
            if (!missiles || rounds == null) return false;
            
            if (rounds.AmmoCount <= 0) return false;

            switch (missiles.Targeting.State)
            {
                case LockState.Locked:
                    return true;
                case LockState.Idle:
                case LockState.Locking:
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
