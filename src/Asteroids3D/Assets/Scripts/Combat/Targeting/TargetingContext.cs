using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Contextual data about a target used for weapon firing decisions.
    /// </summary>
    public struct TargetingContext
    {
        public Vector2 targetPosition;
        public float distanceToTarget;
        public float angleToTarget;
        public bool hasLineOfSight;
    }
}
