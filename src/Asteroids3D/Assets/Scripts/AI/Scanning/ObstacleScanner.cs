using Asteroids.Spawning;
using Game;
using UnityEngine;

namespace AI.Scanning
{
    public readonly struct DetectedObstacle
    {
        public readonly Vector2 position;
        public readonly float radius;
        public readonly Collider collider;

        public DetectedObstacle(Collider collider)
        {
            this.collider = collider;
            position = GamePlane.WorldPointToPlane(collider.transform.position);
            radius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
        }

        // Used when the authoritative position/radius come from the source object rather than
        // collider bounds (ships from ship settings; asteroids from the spawn registry).
        public DetectedObstacle(Vector3 worldPos, float radius, Collider collider)
        {
            this.collider = collider;
            position = GamePlane.WorldPointToPlane(worldPos);
            this.radius = radius;
        }
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
    /// Produces the static-obstacle scan for the MPC (output contract: <see cref="ObstacleScan"/>,
    /// unchanged). Primary producer: a direct query of the live asteroid spawn registry within a
    /// FIXED-size AABB around the ship — the extent is the worst-case travel envelope at
    /// <c>maxSpeed</c> over the lookahead horizon, a per-ship constant, so the obstacle set no
    /// longer breathes with current speed and there is no physics round-trip. The registry is
    /// live state: destroyed asteroids leave it immediately and fragments enter it, so the query
    /// reflects the world, not the deterministic spawn layout. Radius per asteroid is the
    /// authoritative in-plane radius from the controller, not collider bounds (and each asteroid
    /// appears exactly once — the old OverlapSphere could report both the cheap sphere and the
    /// LOD mesh collider of the same rock).
    /// Fallback: when no asteroid spawner is live (primitive-obstacle test scenes, legacy
    /// setups), the legacy speed-adaptive OverlapSphere path runs unchanged.
    /// </summary>
    public class ObstacleScanner
    {
        private static readonly Collider[] ScratchBuffer = new Collider[128];

        private readonly Transform origin;
        private readonly LayerMask obstacleMask;
        private readonly GameObject selfRoot;
        private Transform excludeRoot;

        public DetectedObstacle[] DetectedBuffer { get; }
        public int DetectedCount { get; private set; }

        /// <summary>Effective radius / AABB half-extent used in the last scan.</summary>
        public float Radius { get; private set; }

        /// <summary>Lookahead time in seconds (sizes the fixed envelope at maxSpeed).</summary>
        public float LookaheadTime { get; set; }

        /// <summary>Max acceleration magnitude (m/s²). Extends the envelope.</summary>
        public float MaxAccel { get; set; }

        /// <summary>True when the last scan used the deterministic registry query.</summary>
        public bool UsedRegistryQuery { get; private set; }

        public ObstacleScanner(Transform origin, LayerMask obstacleMask, float lookaheadTime = 2f, int bufferSize = 64)
        {
            this.origin = origin;
            this.obstacleMask = obstacleMask;
            selfRoot = origin.gameObject;
            LookaheadTime = lookaheadTime;
            DetectedBuffer = new DetectedObstacle[bufferSize];
            DetectedCount = 0;
        }

        public void SetExcludeRoot(Transform root) => excludeRoot = root;
        public void ClearExcludeRoot() => excludeRoot = null;

        /// <summary>
        /// The fixed AABB half-extent: distance coverable within the lookahead time at max
        /// speed plus the max-acceleration contribution. Constant per ship — no speed churn.
        /// </summary>
        public float QueryHalfExtent(float maxSpeed)
        {
            var t = LookaheadTime;
            return maxSpeed * t + 0.5f * MaxAccel * t * t;
        }

        /// <summary>
        /// Scan for static obstacles. Registry query when a spawner is live; legacy
        /// speed-adaptive OverlapSphere otherwise.
        /// </summary>
        public void Scan(Vector2 vel, float maxSpeed)
        {
            if (AsteroidSpawner.ActiveSpawners.Count > 0)
                ScanRegistry(maxSpeed);
            else
                ScanPhysics(vel);
        }

        private void ScanRegistry(float maxSpeed)
        {
            UsedRegistryQuery = true;
            var halfExtent = QueryHalfExtent(maxSpeed);
            Radius = halfExtent;
            var center = GamePlane.WorldPointToPlane(origin.position);

            DetectedCount = 0;
            var spawners = AsteroidSpawner.ActiveSpawners;
            for (var s = 0; s < spawners.Count; s++)
            {
                var live = spawners[s].LiveAsteroids;
                if (live == null) continue;
                foreach (var ast in live)
                {
                    if (!ast) continue;
                    var pos = GamePlane.WorldPointToPlane(ast.transform.position);
                    if (Mathf.Abs(pos.x - center.x) > halfExtent ||
                        Mathf.Abs(pos.y - center.y) > halfExtent) continue;
                    if (excludeRoot && ast.transform.root == excludeRoot) continue;
                    if (DetectedCount >= DetectedBuffer.Length) return;
                    DetectedBuffer[DetectedCount++] =
                        new DetectedObstacle(ast.transform.position, ast.Radius, ast.SimpleCollider);
                }
            }
        }

        private void ScanPhysics(Vector2 vel)
        {
            UsedRegistryQuery = false;
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
