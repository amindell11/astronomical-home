using Game;
using UnityEngine;

namespace AI.Scanning
{
    public readonly struct DetectedObstacle
    {
        /// <summary>One covering circle already projected to plane space.</summary>
        public readonly struct PlaneCircle
        {
            public readonly Vector2 center;
            public readonly float radius;
            public PlaneCircle(Vector2 center, float radius)
            {
                this.center = center;
                this.radius = radius;
            }
        }

        // Primary single circle: the K=1 / fallback / current behaviour. Selection ranks
        // whole obstacles by THIS position, and multi-sphere-off consumers use it exclusively.
        public readonly Vector2 position;
        public readonly float radius;
        public readonly Collider collider;

        /// <summary>The asteroid behind this entry, when the producer is the asteroid field; null for
        /// ships and plain colliders. Carries the interim referent identity (component-ref + spawn
        /// epoch, #423) so slot consumers never re-derive it through the collider.</summary>
        public readonly Asteroids.AsteroidController source;

        // Plane-projected linear velocity (units/s); zero for static sources. MPC solver inputs never read it.
        public readonly Vector2 velocity;

        // Remaining health fraction (0,1]; 1 for undamageable sources. RL obs consume it; solver inputs never read it.
        public readonly float healthPct;

        // Up to three tighter covering circles for an elongated obstacle. lobeCount == 0 means
        // "use the primary circle only" (every non-lobe ctor), so ships and legacy callers are
        // unchanged. When lobeCount > 1 a multi-sphere-aware consumer may prefer these.
        public readonly PlaneCircle lobe0;
        public readonly PlaneCircle lobe1;
        public readonly PlaneCircle lobe2;
        public readonly int lobeCount;

        public DetectedObstacle(Collider collider)
        {
            this.collider = collider;
            position = GamePlane.WorldPointToPlane(collider.transform.position);
            radius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
            velocity = Vector2.zero;
            healthPct = 1f;
            source = null;
            lobe0 = default;
            lobe1 = default;
            lobe2 = default;
            lobeCount = 0;
        }

        // Used for non-static obstacles (e.g. other ships) where the relevant world
        // position is the ship's transform, not necessarily a child collider's local origin,
        // and radius comes from ship settings rather than collider bounds.
        public DetectedObstacle(Vector3 worldPos, float radius, Collider collider, Vector2 velocity = default, float healthPct = 1f,
            Asteroids.AsteroidController source = null)
        {
            this.collider = collider;
            position = GamePlane.WorldPointToPlane(worldPos);
            this.radius = radius;
            this.velocity = velocity;
            this.healthPct = healthPct;
            this.source = source;
            lobe0 = default;
            lobe1 = default;
            lobe2 = default;
            lobeCount = 0;
        }

        // Multi-lobe overload: primary circle (fallback / selection key) plus up to three
        // pre-projected plane lobes. lobeCount clamps to [0,3].
        public DetectedObstacle(Vector3 worldPos, float radius, Collider collider,
            PlaneCircle lobe0, PlaneCircle lobe1, PlaneCircle lobe2, int lobeCount, Vector2 velocity = default, float healthPct = 1f,
            Asteroids.AsteroidController source = null)
        {
            this.collider = collider;
            position = GamePlane.WorldPointToPlane(worldPos);
            this.radius = radius;
            this.velocity = velocity;
            this.healthPct = healthPct;
            this.source = source;
            this.lobe0 = lobe0;
            this.lobe1 = lobe1;
            this.lobe2 = lobe2;
            this.lobeCount = Mathf.Clamp(lobeCount, 0, 3);
        }

        /// <summary>Lobe by index (0..lobeCount-1). Caller guarantees the range.</summary>
        public PlaneCircle Lobe(int i) => i == 0 ? lobe0 : i == 1 ? lobe1 : lobe2;
    }

    public readonly struct ObstacleScan
    {
        public readonly DetectedObstacle[] buffer;
        public readonly int count;

        public ObstacleScan(DetectedObstacle[] buffer, int count)
        {
            this.buffer = buffer;
            this.count = count;
        }

        public static implicit operator (DetectedObstacle[] buffer, int count)(ObstacleScan scan) => (scan.buffer, scan.count);
        public static implicit operator ObstacleScan((DetectedObstacle[] buffer, int count) tuple) => new ObstacleScan(tuple.buffer, tuple.count);
    }

    /// <summary>
    /// Fills a buffer with live asteroids inside a fixed-size AABB around the ship by
    /// querying the deterministic asteroid field directly (no physics overlap). Destroyed
    /// asteroids are never reported. The MPC handles relevance through its cost function.
    /// </summary>
    public class ObstacleScanner
    {
        private readonly Transform origin;
        private readonly IObstacleField field;

        public DetectedObstacle[] DetectedBuffer { get; }
        public int DetectedCount { get; private set; }

        /// <summary>
        /// Half-extent (per axis) of the fixed query box, in plane units: the worst-case
        /// travel envelope over the lookahead horizon at max speed — a per-ship constant
        /// computed once at construction, so the obstacle set never breathes with speed.
        /// </summary>
        public float HalfExtent { get; }

        /// <param name="maxSpeed">Ship max speed (plane units/s).</param>
        /// <param name="maxAccel">Max acceleration magnitude (units/s²); extends the envelope.</param>
        /// <param name="lookaheadTime">Planning horizon the envelope must cover, seconds.</param>
        /// <param name="field">The world's obstacle source; null senses zero static obstacles.</param>
        public ObstacleScanner(Transform origin, float maxSpeed, float maxAccel,
            float lookaheadTime, IObstacleField field, int bufferSize = 64)
        {
            this.origin = origin;
            this.field = field;
            HalfExtent = maxSpeed * lookaheadTime + 0.5f * maxAccel * lookaheadTime * lookaheadTime;
            DetectedBuffer = new DetectedObstacle[bufferSize];
            DetectedCount = 0;
        }

        /// <summary>Query the world's obstacle field for live asteroids inside the fixed box around the ship; a null field (a world without asteroids) clears the buffer.</summary>
        public void Scan()
        {
            DetectedCount = 0;
            if (field == null) return;
            var centerPlane = GamePlane.WorldPointToPlane(origin.position);
            DetectedCount = field.QueryObstacles(centerPlane, HalfExtent, DetectedBuffer);
        }

        /// <summary>Back to the pre-first-scan state; stale results must not outlive a reset (episode boundaries rebuild the field).</summary>
        public void Clear() => DetectedCount = 0;
    }
}
