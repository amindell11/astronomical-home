using Ships;
using UnityEngine;

namespace AI.Scanning
{
    /// <summary>
    /// Orchestrates all scanning subsystems.
    /// Performs scans each frame and provides cached results to consumers.
    /// </summary>
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
        private Ship ship;
        
        public void Initialize(Transform origin)
        {
            
            shipScanner = new ShipScanner(origin, nearbyShipRadius);
            coverScanner = new CoverScanner(origin, asteroidCoverRadius, asteroidMask);
            obstacleScanner = new DynamicObstacleScanner(origin, asteroidMask, lookAheadDist, degreesBetweenRays, maxRayDegrees, sphereCastRadius);
        }

        private void Update()
        {
            if (!ship) return;
            
            shipScanner?.Scan();
            coverScanner?.Scan();
            obstacleScanner?.Scan(ship.Movement.Kinematics.Vel, ship.settings.maxSpeed);
        }

        public ObstacleScan ObstacleScan => new(obstacleScanner?.DetectedBuffer, obstacleScanner?.DetectedCount ?? 0);
        public ShipScanResult? ShipScan => shipScanner?.LastResult;
        public bool HasNearbyCover => coverScanner?.HasCover ?? false;
    }
}
