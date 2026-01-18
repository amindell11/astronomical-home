using Combat.Conditions;
using Combat.Targeting;
using Combat.Weapons;
using UnityEngine;
using Weapons;

namespace AI.Combat
{
    [RequireComponent(typeof(WeaponMissiles))]
    public class MissileStrategy : MonoBehaviour, IWeaponStrategy
    {
        private WeaponMissiles missiles;
        private Rounds rounds;

        public int Priority => 10;

        private void Awake()
        {
            missiles = GetComponent<WeaponMissiles>();
            rounds = GetComponent<Rounds>();
        }

        public bool ShouldFire(IWeaponStrategy.TargetingContext context)
        {
            if (!missiles || rounds == null) return false;
            
            if (rounds.AmmoCount <= 0) return false;

            switch (missiles.Targeting.State)
            {
                case LockState.Locked:
                    return true;
                case LockState.Idle:
                case LockState.Locking:
                    const float dummyMissileRange = 10f;
                    const float missileAngleTolerance = 15f;
                    return context.DistanceToTarget <= dummyMissileRange && context.AngleToTarget <= missileAngleTolerance;
                default:
                    return false;
            }
        }
    }
}
