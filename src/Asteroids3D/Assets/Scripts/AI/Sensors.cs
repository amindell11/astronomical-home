using AI.Computers;
using AI.Context;
using Ships;
using Ships.Movement;
using UnityEngine;
using ObstacleScanner = AI.Scanning.ObstacleScanner;

namespace AI
{
    public partial class Sensors : MonoBehaviour
    {
        [Header("Ship Scanning")]
        [Tooltip("Maximum distance to consider nearby ships")]
        public float nearbyShipRadius = 30f;
        [Tooltip("Radius to scan for asteroid cover")]
        public float asteroidCoverRadius = 15f;

        [Header("Obstacle Detection")]
        public LayerMask asteroidMask;
        public float lookAheadTime = 1f;
        public float safeMargin = 2f;

        [Header("Raycast Avoidance")]
        [Tooltip("Number of rays to cast on each side of the ship")]
        public int raysPerDirection = 5;
        [Tooltip("Max angle in degrees to cast rays")]
        public float maxRayDegrees = 90f;
        [Tooltip("Radius of spherecast for obstacle detection. Set to 0 for raycasts.")]
        public float sphereCastRadius = 0.5f;

        private ShipScanner ShipScanner;

        public ObstacleScanner ObstacleScanner { get; private set; }

        public void Initialize(Ship ship, ShipInfo shipInfo)
        {
            ShipScanner = new ShipScanner(ship, shipInfo, nearbyShipRadius);
            ObstacleScanner = new ObstacleScanner(ship.transform);
        }

        public ObstacleScanner.ScanResult ScanObstacles(Kinematics kin, float maxSpeed, bool enabled)
        {
            var scanConfig = new ObstacleScanner.Config
            {
                enabled = enabled,
                asteroidMask = asteroidMask,
                lookAheadTime = lookAheadTime,
                safeMargin = safeMargin,
                maxSpeed = maxSpeed,
                raysPerDirection = raysPerDirection,
                maxRayDegrees = maxRayDegrees,
                sphereCastRadius = sphereCastRadius
            };

            return ObstacleScanner.Scan(scanConfig, kin);
        }

        // Delegate ship scanning to ShipScanner
        public ShipScanner.ScanResult LastScan => ShipScanner?.LastScan ?? default;
        public ShipScanner.ScanResult ScanNearby(Ship excludeFromThreat = null) => ShipScanner?.ScanNearby(excludeFromThreat) ?? default;
        public Ship FindNearestEnemy() => ShipScanner?.FindNearestEnemy();
        
        // Delegate cover detection to ObstacleScanner
        public bool HasNearbyCover(Vector3 position) => ObstacleScanner?.HasNearbyCover(position, asteroidCoverRadius) ?? false;
    }
}
