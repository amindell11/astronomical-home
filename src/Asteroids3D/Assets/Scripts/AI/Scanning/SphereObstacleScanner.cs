using UnityEngine;

namespace AI.Scanning
{
    /// <summary>
    /// Simple obstacle scanner using a single OverlapSphere.
    /// Detects all obstacles within a radius that expands with speed,
    /// letting the MPC handle relevance through its cost function.
    /// Drop-in replacement for DynamicObstacleScanner.
    /// </summary>
    public class SphereObstacleScanner : ObstacleScanner
    {
        private static readonly Collider[] ScratchBuffer = new Collider[128];

        /// <summary>Effective radius used in the last scan.</summary>
        public float Radius { get; private set; }

        /// <summary>Lookahead time in seconds.</summary>
        public float LookaheadTime { get; set; }

        /// <summary>Max acceleration magnitude (m/s²). Used to extend detection range.</summary>
        public float MaxAccel { get; set; }

        public SphereObstacleScanner(
            Transform origin,
            LayerMask obstacleMask,
            float lookaheadTime = 2f,
            int bufferSize = 64)
            : base(origin, obstacleMask, bufferSize)
        {
            LookaheadTime = lookaheadTime;
        }

        /// <summary>
        /// Scan for obstacles. Radius covers the distance reachable within
        /// the lookahead time from max speed plus current speed contribution.
        /// </summary>
        public override void Scan(Vector2 vel, float maxSpeed)
        {
            var t = LookaheadTime;
            Radius = vel.magnitude * t + 0.5f * MaxAccel * t * t;
            var count = Physics.OverlapSphereNonAlloc(
                origin.position, Radius, ScratchBuffer, obstacleMask,
                QueryTriggerInteraction.Ignore);

            DetectedCount = 0;
            for (var i = 0; i < count && DetectedCount < DetectedBuffer.Length; i++)
            {
                var col = ScratchBuffer[i];
                if (col && col.gameObject != selfRoot && col.transform.root != origin.root
                    && (!excludeRoot || col.transform.root != excludeRoot))
                    DetectedBuffer[DetectedCount++] = new DetectedObstacle(col);
            }
        }
    }
}
