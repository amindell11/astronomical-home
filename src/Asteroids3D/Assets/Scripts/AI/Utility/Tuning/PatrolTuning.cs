using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct PatrolTuning
    {
        public float radius;
        public float minDistanceFactor;

        public static PatrolTuning Default => new PatrolTuning
        {
            radius = 50f,
            minDistanceFactor = 0.3f,
        };
    }
}
