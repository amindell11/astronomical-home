using UnityEngine;

namespace EnemyAI
{
    public interface IWeaponAIStrategy
    {
        public class TargetingContext
        {
            public Vector2 TargetPosition;
            public float DistanceToTarget;
            public float AngleToTarget;
            public bool HasLineOfSight;
            // Future additions can be made here, e.g. public Vector2 TargetVelocity;
        }

        bool ShouldFire(TargetingContext context);
        
        int Priority { get; }
    }
}
