using System;
using Movement;
using Ships;
using Ships.Command;
using UnityEngine;
using Utils;

namespace AI.Scanning
{
    /// <summary>
    /// Orchestrates all scanning subsystems.
    /// Performs scans each frame and provides cached results to consumers.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public partial class Scout : MonoBehaviour
    {
        [Header("Ship Scanning")]
        public float nearbyShipRadius = 30f;
        public float asteroidCoverRadius = 15f;

        [Header("Obstacle Detection")]
        public LayerMask asteroidMask;
        public bool useSimpleObstacleScanner = true;
        [Tooltip("Lookahead time for the simple obstacle scanner. Detection radius = maxSpeed * this value.")]
        public float obstacleLookaheadTime = 2f;

        [Header("Legacy (StandardNavigator / DynamicObstacleScanner only)")]
        public float lookAheadDist = 15f;
        public float safeMargin = 1.0f;
        public float degreesBetweenRays = 15f;
        public float maxRayDegrees = 90f;
        public float sphereCastRadius = 0.5f;

        private ShipScanner shipScanner;
        private CoverScanner coverScanner;
        private ObstacleScanner obstacleScanner;
        private ShipId shipId;
        public ShipId ShipId => shipId;
        public IShipRegistry Registry { get; private set; }
        private Dynamics shipDynamics;
        private Func<State> getState;

        // Combined obstacle buffer: static obstacles from obstacleScanner + 360° ship detections from shipScanner.
        // The fan/sphere obstacle scanner can miss ships off to the side; merging the dedicated ship scanner
        // ensures the MPC sees every nearby ship regardless of bearing.
        private DetectedObstacle[] mergedObstacles = new DetectedObstacle[128];
        private int mergedObstacleCount;
        public void Initialize(Transform origin, ShipId shipId, Dynamics shipDynamics, Func<State> stateProvider, IShipRegistry registry)
        {
            this.shipId = shipId;
            this.shipDynamics = shipDynamics;
            Registry = registry;
            getState = stateProvider;
            shipScanner = new ShipScanner(origin, nearbyShipRadius, shipId, registry);
            coverScanner = new CoverScanner(origin, asteroidCoverRadius, asteroidMask);
            // ShipScanner handles ships in a full sphere; obstacle scanner stays focused on static asteroids.
            var avoidanceMask = asteroidMask;
            if (useSimpleObstacleScanner)
                obstacleScanner = new SphereObstacleScanner(origin, avoidanceMask,
                    lookaheadTime: obstacleLookaheadTime);
            else
                obstacleScanner = new DynamicObstacleScanner(origin, avoidanceMask, lookAheadDist, degreesBetweenRays, maxRayDegrees, sphereCastRadius);
        }

        private void Update()
        {
            if (!shipId.IsValid) return;

            shipScanner?.Scan();
            coverScanner?.Scan();
            if (obstacleScanner is SphereObstacleScanner sphere)
            {
                sphere.LookaheadTime = obstacleLookaheadTime;
                var d = shipDynamics;
                sphere.MaxAccel = Mathf.Sqrt(d.forwardAcc * d.forwardAcc + d.maxStrafeAcc * d.maxStrafeAcc) / d.mass;
            }
            obstacleScanner?.Scan(getState().kinematics.vel, shipDynamics.maxSpeed);
            BuildMergedObstacles();
        }

        private void BuildMergedObstacles()
        {
            mergedObstacleCount = 0;

            // Static obstacles (asteroids etc.) from the obstacle scanner.
            if (obstacleScanner != null)
            {
                var src = obstacleScanner.DetectedBuffer;
                var srcCount = obstacleScanner.DetectedCount;
                for (var i = 0; i < srcCount && mergedObstacleCount < mergedObstacles.Length; i++)
                    mergedObstacles[mergedObstacleCount++] = src[i];
            }

            // Other ships from the 360° ship scanner — covers blind spots of the directional/forward
            // obstacle scanner so the MPC actually sees ships approaching from the side.
            if (shipScanner != null && Registry != null)
            {
                var scan = shipScanner.LastResult;
                for (var i = 0; i < scan.count && mergedObstacleCount < mergedObstacles.Length; i++)
                {
                    var id = scan.shipIds[i];
                    if (!Registry.TryGetShip(id, out var ship) || ship == null) continue;
                    var col = (ship.Colliders != null && ship.Colliders.Length > 0) ? ship.Colliders[0] : null;
                    if (!col) continue;
                    var radius = ship.settings ? ship.settings.shipRadius : col.bounds.extents.magnitude * 0.5f;
                    mergedObstacles[mergedObstacleCount++] =
                        new DetectedObstacle(ship.transform.position, radius, col);
                }
            }
        }

        public ObstacleScan ObstacleScan => new(mergedObstacles, mergedObstacleCount);
        public ShipScanResult? ShipScan => shipScanner?.LastResult;
        public bool HasNearbyCover => coverScanner?.HasCover ?? false;

        public void SetObstacleExclusion(Transform root) => obstacleScanner?.SetExcludeRoot(root);
        public void ClearObstacleExclusion() => obstacleScanner?.ClearExcludeRoot();
    }
}
