using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "Combat/Combat Tuning")]
    public class CombatTuning : ScriptableObject
    {
        [Header("Targeting")]
        [SerializeField, Min(1)] private int lineOfSightCacheFrames = 5;
        [SerializeField, Range(0f, 180f)] private float angleToleranceBeforeRaycast = 15f;

        [Header("Laser AI Firing")]
        [SerializeField, Min(0f)] private float laserFireDistance = 20f;
        [SerializeField, Range(0f, 180f)] private float laserFireAngleTolerance = 5f;

        [Header("Missile AI Firing (No Lock)")]
        [SerializeField, Min(0f)] private float missileFallbackRange = 10f;
        [SerializeField, Range(0f, 180f)] private float missileFallbackAngleTolerance = 15f;

        public int LineOfSightCacheFrames => lineOfSightCacheFrames;
        public float AngleToleranceBeforeRaycast => angleToleranceBeforeRaycast;
        public float LaserFireDistance => laserFireDistance;
        public float LaserFireAngleTolerance => laserFireAngleTolerance;
        public float MissileFallbackRange => missileFallbackRange;
        public float MissileFallbackAngleTolerance => missileFallbackAngleTolerance;
    }
}
