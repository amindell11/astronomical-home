using System;
using AI.Scanning;
using AI.Scanning.Sensors;
using Movement;
using Ships;
using Ships.Command;
using Substrate;
using UnityEngine;
using Ships.Registry;

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
        private IWeaponContext weapons;

        // Merged buffer: own keep-outs + static obstacles + 360° ship detections, so the MPC sees ships at any bearing.
        private DetectedObstacle[] mergedObstacles = new DetectedObstacle[128];
        private readonly ShipId[] mergedShipIds = new ShipId[128];
        private readonly DetectedObstacle[] exemptScan = new DetectedObstacle[128];
        private readonly DetectedObstacle.PlaneCircle[] keepOuts = new DetectedObstacle.PlaneCircle[8];
        private int mergedObstacleCount;

        public void Initialize(Transform origin, ShipId shipId, Dynamics shipDynamics, IShipStatus shipContext,
            IShipRegistry registry, IObstacleField field, IWeaponContext weapons)
        {
            this.shipId = shipId;
            this.shipDynamics = shipDynamics;
            this.origin = origin;
            Registry = registry;
            this.shipContext = shipContext;
            this.weapons = weapons;
            shipScanner = new ShipScanner(origin, nearbyShipRadius, shipId, registry);
            coverSensor = new SphereSensor(origin, asteroidCoverRadius, asteroidMask, bufferSize: 8);
            var maxAccel = Mathf.Sqrt(shipDynamics.forwardAcc * shipDynamics.forwardAcc + shipDynamics.maxStrafeAcc * shipDynamics.maxStrafeAcc) / shipDynamics.mass;
            obstacleScanner = new ObstacleScanner(origin, shipDynamics.maxSpeed, maxAccel, obstacleLookaheadTime, field);
        }

        /// <summary>Clears cached scan outputs to their pre-first-scan state; the next Update rebuilds them from the live world.</summary>
        public void ResetState()
        {
            Contacts = ContactSummary.Empty;
            HasNearbyCover = false;
            mergedObstacleCount = 0;
            obstacleScanner?.Clear();
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
            AddKeepOuts();

            if (obstacleScanner != null)
            {
                var src = obstacleScanner.DetectedBuffer;
                var srcCount = obstacleScanner.DetectedCount;
                for (var i = 0; i < srcCount && mergedObstacleCount < mergedObstacles.Length; i++)
                {
                    mergedShipIds[mergedObstacleCount] = ShipId.Invalid;
                    mergedObstacles[mergedObstacleCount++] = src[i];
                }
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
                    mergedShipIds[mergedObstacleCount] = id;
                    mergedObstacles[mergedObstacleCount++] =
                        new DetectedObstacle(ship.transform.position, radius, col, ship.Kinematics.vel);
                }
            }
        }

        // Written first: the solver truncates at its row cap, so rocks and ships drop before keep-outs.
        private void AddKeepOuts()
        {
            if (weapons == null) return;
            var slots = weapons.Slots;
            for (var s = 0; s < slots.Count; s++)
            {
                var count = weapons.KeepOutCircles(slots[s], keepOuts);
                for (var i = 0; i < count; i++)
                {
                    mergedShipIds[mergedObstacleCount] = ShipId.Invalid;
                    mergedObstacles[mergedObstacleCount++] =
                        new DetectedObstacle(GamePlane.PlanePointToWorld(keepOuts[i].center), keepOuts[i].radius, null);
                }
            }
        }

        public ObstacleScan ObstacleScan => new(mergedObstacles, mergedObstacleCount);

        /// <summary>The merged scan without one ship, for a keystone that must reach it.</summary>
        public ObstacleScan ObstacleScanWithout(ShipId ship)
        {
            var count = 0;
            for (var i = 0; i < mergedObstacleCount; i++)
                if (mergedShipIds[i] != ship) exemptScan[count++] = mergedObstacles[i];
            return new ObstacleScan(exemptScan, count);
        }
        /// <summary>Raw asteroid-only scanner view (no ship detections) — the RL obstacle-token source.</summary>
        public ObstacleScan AsteroidScan => new(obstacleScanner.DetectedBuffer, obstacleScanner.DetectedCount);
        public ShipScanResult? ShipScan => shipScanner?.LastResult;
        /// <summary>Cached per-tick contact summary (nearest enemy + force balance).</summary>
        public ContactSummary Contacts { get; private set; } = ContactSummary.Empty;
        public bool HasNearbyCover { get; private set; }
    }
}
