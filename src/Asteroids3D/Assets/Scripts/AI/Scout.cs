using AI.Scanning;
using Ships;
using UnityEngine;

namespace AI
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
        public int raysPerDirection = 5;
        public float maxRayDegrees = 90f;
        public float sphereCastRadius = 0.5f;

        private ShipScanner shipScanner;
        private CoverScanner coverScanner;
        private ObstacleScanner obstacleScanner;
        private Ship ship;
        
        public void Initialize(Ship ship)
        {
            this.ship = ship;
            var origin = ship.transform;
            
            shipScanner = new ShipScanner(ship, nearbyShipRadius);
            coverScanner = new CoverScanner(origin, asteroidCoverRadius, asteroidMask);
            obstacleScanner = new ObstacleScanner(origin, lookAheadDist, asteroidMask, raysPerDirection, maxRayDegrees, sphereCastRadius);
        }

        private void Update()
        {
            if (!ship) return;
            
            shipScanner?.Scan();
            coverScanner?.Scan();
            
            var vel = ship.Movement.Kinematics.Vel;
            var scanDir = vel.sqrMagnitude > 0.001f ? vel.normalized : ship.Movement.Kinematics.Forward;
            obstacleScanner?.Scan(scanDir);
        }

        public ShipScanner Ships => shipScanner;
        public ObstacleScanner Obstacles => obstacleScanner;
        public ObstacleScanResult ScanObstacles() => obstacleScanner?.LastResult ?? default;
        public bool HasNearbyCover => coverScanner?.HasCover ?? false;
    }
}
