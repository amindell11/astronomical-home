using Ships.Weapons.Conditions;
using UnityEngine;
using Weapons;

namespace AI.Combat
{
    [RequireComponent(typeof(Missiles))]
    public class MissileStrategy : MonoBehaviour, IWeaponStrategy
    {
        private Missiles missiles;
        private Rounds rounds;

        public int Priority => 10;

        private void Awake()
        {
            missiles = GetComponent<Missiles>();
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
