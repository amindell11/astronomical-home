using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct PatrolTuning
    {
        public float radius;
        public float minDistanceFactor;
        [Tooltip("Distance at which the patrol waypoint is considered reached. " +
                 "Should be <= MPC arrivalDistance to avoid the ship hovering outside the decel zone.")]
        public float arriveRadius;

        [Tooltip("Seconds without meaningful progress before the waypoint is abandoned.")]
        public float stuckTimeout;

        [Tooltip("Ship must close at least this much distance to reset the stuck timer.")]
        public float stuckProgressThreshold;

        public static PatrolTuning Default => new PatrolTuning
        {
            radius = 50f,
            minDistanceFactor = 0.3f,
            arriveRadius = 3f,
            stuckTimeout = 3f,
            stuckProgressThreshold = 1f,
        };
    }
}
