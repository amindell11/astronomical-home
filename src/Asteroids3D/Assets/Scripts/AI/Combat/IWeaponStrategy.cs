using UnityEngine;

namespace AI.Combat
{
    public interface IWeaponStrategy
    {
        public class TargetingContext
        {
            public Vector2 TargetPosition;
            public float DistanceToTarget;
            public float AngleToTarget;
            public bool HasLineOfSight;
        }

        bool ShouldFire(TargetingContext context);
        
        int Priority { get; }
    }
}
