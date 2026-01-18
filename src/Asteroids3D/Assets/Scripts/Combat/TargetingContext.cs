using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Contextual data about a target used for weapon firing decisions.
    /// </summary>
    public struct TargetingContext
    {
        public Vector2 TargetPosition;
        public float DistanceToTarget;
        public float AngleToTarget;
        public bool HasLineOfSight;
    }
}
