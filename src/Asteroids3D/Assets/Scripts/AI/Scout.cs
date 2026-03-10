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
        public float lookAheadDist = 15f;
        public float safeMargin = 1.0f;

        [Header("Raycast Avoidance")]
        public float degreesBetweenRays = 15f;
        public float maxRayDegrees = 90f;
        public float sphereCastRadius = 0.5f;

        private ShipScanner shipScanner;
        private CoverScanner coverScanner;
        private DynamicObstacleScanner obstacleScanner;
        private ShipId shipId;
        public ShipId ShipId => shipId;
        public IShipRegistry Registry { get; private set; }
        private Dynamics shipDynamics;
        private Func<State> getState;
        public void Initialize(Transform origin, ShipId shipId, Dynamics shipDynamics, Func<State> stateProvider, IShipRegistry registry)
        {
            this.shipId = shipId;
            this.shipDynamics = shipDynamics;
            Registry = registry;
            getState = stateProvider;
            shipScanner = new ShipScanner(origin, nearbyShipRadius, shipId, registry);
            coverScanner = new CoverScanner(origin, asteroidCoverRadius, asteroidMask);
            var avoidanceMask = asteroidMask | LayerIds.Mask(LayerIds.Ship);
            obstacleScanner = new DynamicObstacleScanner(origin, avoidanceMask, lookAheadDist, degreesBetweenRays, maxRayDegrees, sphereCastRadius);
        }

        private void Update()
        {
            if (!shipId.IsValid) return;

            shipScanner?.Scan();
            coverScanner?.Scan();
            obstacleScanner?.Scan(getState().kinematics.vel, shipDynamics.maxSpeed);
        }

        public ObstacleScan ObstacleScan => new(obstacleScanner?.DetectedBuffer, obstacleScanner?.DetectedCount ?? 0);
        public ShipScanResult? ShipScan => shipScanner?.LastResult;
        public bool HasNearbyCover => coverScanner?.HasCover ?? false;

        public void SetObstacleExclusion(Transform root) => obstacleScanner?.SetExcludeRoot(root);
        public void ClearObstacleExclusion() => obstacleScanner?.ClearExcludeRoot();
    }
}
