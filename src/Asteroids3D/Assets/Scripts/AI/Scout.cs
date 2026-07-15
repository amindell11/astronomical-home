using System;
using AI.Scanning;
using AI.Scanning.Sensors;
using Game.Services;
using Movement;
using Ships;
using Ships.Command;
using UnityEngine;
using Utils;

namespace AI
{
    /// <summary>Orchestrates all scanning subsystems: scans each frame and provides cached results to consumers.</summary>
    [DefaultExecutionOrder(-80)]
    public class Scout : MonoBehaviour
    {
        [Header("Ship Scanning")]
        public float nearbyShipRadius = 30f;
        public float asteroidCoverRadius = 15f;

        [Header("Obstacle Detection")]
        public LayerMask asteroidMask;
        [Tooltip("Lookahead time used to size the obstacle query box. Sizes a FIXED worst-case " +
                 "AABB (from max speed, not current speed), so the box no longer grows as the ship accelerates.")]
        public float obstacleLookaheadTime = 2f;

        private ShipScanner shipScanner;
        private SphereSensor coverSensor;
        internal ObstacleScanner obstacleScanner;
        private Transform origin;
        private ShipId shipId;
        public ShipId ShipId => shipId;
        public IShipRegistry Registry { get; private set; }
        private Dynamics shipDynamics;
        private IShipStatus shipContext;

        // Merged buffer: static obstacles + 360° ship detections, so the MPC sees nearby ships regardless of bearing.
        private DetectedObstacle[] mergedObstacles = new DetectedObstacle[128];
        private int mergedObstacleCount;

        public void Initialize(Transform origin, ShipId shipId, Dynamics shipDynamics, IShipStatus shipContext, ArenaContext arena)
        {
            this.shipId = shipId;
            this.shipDynamics = shipDynamics;
            this.origin = origin;
            Registry = arena.Registry;
            this.shipContext = shipContext;
            shipScanner = new ShipScanner(origin, nearbyShipRadius, shipId, arena.Registry);
            coverSensor = new SphereSensor(origin, asteroidCoverRadius, asteroidMask, bufferSize: 8);
            var maxAccel = Mathf.Sqrt(shipDynamics.forwardAcc * shipDynamics.forwardAcc + shipDynamics.maxStrafeAcc * shipDynamics.maxStrafeAcc) / shipDynamics.mass;
            obstacleScanner = new ObstacleScanner(origin, shipDynamics.maxSpeed, maxAccel, obstacleLookaheadTime, arena);
        }

        /// <summary>Clears cached scan outputs to their pre-first-scan state; the next Update rebuilds them from the live world.</summary>
        public void ResetState()
        {
            Contacts = ContactSummary.Empty;
            HasNearbyCover = false;
            mergedObstacleCount = 0;
        }

        private void Update()
        {
            if (!shipId.IsValid) return;

            shipScanner?.Scan();
            Contacts = shipScanner != null
                ? ContactSummary.Build(shipScanner.LastResult, shipId, origin.position, Registry)
                : ContactSummary.Empty;
            HasNearbyCover = coverSensor != null && coverSensor.Detect() > 0;
            obstacleScanner?.Scan();
            BuildMergedObstacles();
        }

        private void BuildMergedObstacles()
        {
            mergedObstacleCount = 0;

            if (obstacleScanner != null)
            {
                var src = obstacleScanner.DetectedBuffer;
                var srcCount = obstacleScanner.DetectedCount;
                for (var i = 0; i < srcCount && mergedObstacleCount < mergedObstacles.Length; i++)
                    mergedObstacles[mergedObstacleCount++] = src[i];
            }

            if (shipScanner == null || Registry == null) return;
            {
                var scan = shipScanner.LastResult;
                for (var i = 0; i < scan.count && mergedObstacleCount < mergedObstacles.Length; i++)
                {
                    var id = scan.shipIds[i];
                    if (!Registry.TryGetShip(id, out var ship) || ship == null) continue;
                    var col = (ship.Colliders != null && ship.Colliders.Length > 0) ? ship.Colliders[0] : null;
                    if (!col) continue;
                    var radius = ship.Stats != null ? ship.ShipRadius : col.bounds.extents.magnitude * 0.5f;
                    mergedObstacles[mergedObstacleCount++] =
                        new DetectedObstacle(ship.transform.position, radius, col);
                }
            }
        }

        public ObstacleScan ObstacleScan => new(mergedObstacles, mergedObstacleCount);
        public ShipScanResult? ShipScan => shipScanner?.LastResult;
        /// <summary>Cached per-tick contact summary (nearest enemy + force balance).</summary>
        public ContactSummary Contacts { get; private set; } = ContactSummary.Empty;
        public bool HasNearbyCover { get; private set; }

        // No-op: obstacles come from the deterministic field query, which never includes ships.
        public void SetObstacleExclusion(Transform root) { }
        public void ClearObstacleExclusion() { }
    }
}
