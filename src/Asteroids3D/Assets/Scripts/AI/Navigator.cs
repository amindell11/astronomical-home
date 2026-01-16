using System.IO;
using AI.Computers;
using AI.Context;
using AI.Steering;
using Game;
using Ships;
using Ships.Movement;
using UnityEngine;

namespace AI
{
    public partial class Navigator : MonoBehaviour
    {
        public struct Waypoint
        {
            public Vector2 position;
            public Vector2 velocity;
            public bool isValid;
        }

        [Header("Navigation")]
        public float arriveRadius = 2f;

        [Header("Avoidance")]
        public LayerMask asteroidMask;
        public float lookAheadTime = 1f;
        public float safeMargin = 2f;
        public float avoidRadius = .5f;
        [Tooltip("Toggle obstacle avoidance logic on/off")] public bool enableAvoidance = false;

        [Header("Raycast Avoidance")]
        [Tooltip("Number of rays to cast on each side of the ship")]
        public int raysPerDirection = 5;
        [Tooltip("Max angle in degrees to cast rays")]
        public float maxRayDegrees = 90f;
        [Tooltip("Radius of spherecast for obstacle detection. Set to 0 for raycasts.")]
        public float sphereCastRadius = 0.5f;

        [Header("Steering")]
        [Tooltip("Higher values react faster; 0 disables smoothing. Units: 1/seconds (approx).")]
        [Range(0, 20)] public float proportionalGain = 5f;
        [Tooltip("Use tilted heading when strafing for more natural flight")]
        [SerializeField] private bool useTiltedHeading = true;

        private Ship ship; //TODO remove ship reference
        private Sensors sensors;
        private SteeringTuning tuning;
        private Pilot pilot;
        private Waypoint currentWaypoint;
        private bool facingOverride;
        private float facingAngle;

        public Waypoint CurrentWaypoint => currentWaypoint;

        public void Initialize(Ship ship, Sensors sensors)
        {
            this.ship = ship;
            this.sensors = sensors;
            currentWaypoint = new Waypoint { isValid = false };
            var mass = ship.Movement.Mass;
            var settings = ship.settings;
            tuning = settings
                ? new SteeringTuning(settings.forwardAccel / mass,
                    settings.reverseAccel / mass,
                    settings.maxStrafeForce / mass,
                    SteeringTuning.Default.DeadZone)
                : SteeringTuning.Default;
            
            pilot = new Pilot(tuning, proportionalGain);
        }

        public void GenerateNavCommands(State state, ref Command cmd)
        {
            if (!ship || !currentWaypoint.isValid) {
                cmd.TargetAngle = state.Kinematics.Yaw; //TODO can remove?
                return;
            }

            var kin = state.Kinematics;
            
            var obstacleScan = ScanObstacles(kin);
            var pathOutput = PlanPath(kin, obstacleScan);
            var pilotOutput = GetPilotOutput(kin, pathOutput);
            
            cmd.Thrust = pilotOutput.thrust;
            cmd.Strafe = pilotOutput.strafe;
            cmd.TargetAngle = pilotOutput.rotTargetDeg;
            cmd.RotateToTarget = true;

            StoreDebugState(obstacleScan);
            StoreDebugState(currentWaypoint.position, pathOutput.dbg, pilotOutput);
            StoreDebugState(pilotOutput.thrust, pilotOutput.strafe);
        }

        private ObstacleScanner.ScanResult ScanObstacles(Kinematics kin)
        {
            var scanConfig = new ObstacleScanner.Config
            {
                enabled = enableAvoidance,
                asteroidMask = asteroidMask,
                lookAheadTime = lookAheadTime,
                safeMargin = safeMargin,
                maxSpeed = ship.settings.maxSpeed,
                raysPerDirection = raysPerDirection,
                maxRayDegrees = maxRayDegrees,
                sphereCastRadius = sphereCastRadius
            };
            
            return sensors.Obstacles.Scan(scanConfig, kin);
        }

        private PathPlanner.Output PlanPath(Kinematics kin, ObstacleScanner.ScanResult obstacleScan)
        {
            var pathInput = new PathPlanner.Input(
                kin, 
                currentWaypoint.position, 
                currentWaypoint.velocity, 
                avoidRadius, 
                arriveRadius,
                ship.settings.maxSpeed,
                lookAheadTime, 
                safeMargin, 
                obstacleScan.Obstacles, 
                tuning);

            return PathPlanner.Compute(pathInput);
        }

        private Pilot.Output GetPilotOutput(Kinematics kin, PathPlanner.Output pathOutput)
        {
            float? facingTarget = facingOverride ? facingAngle : null;
            var pilotInput = new Pilot.Input(kin, pathOutput.desiredVelocity, pathOutput.desiredAccel, 
                ship.settings.maxSpeed, facingTarget, useTiltedHeading); 
            return pilot.Compute(pilotInput);
        }
        
        public void SetNavigationPoint(Vector2 point, bool avoid = false, Vector2? velocity = null)
        {
            currentWaypoint.position = point;
            currentWaypoint.velocity = velocity ?? Vector2.zero;
            currentWaypoint.isValid = true;
            enableAvoidance = avoid;
        }

        public void SetNavigationPointWorld(Vector3 worldPos, bool avoid = true, Vector3? velocity = null)
        {
            var planePos = GamePlane.WorldPointToPlane(worldPos);
            var planeVel = velocity.HasValue ? GamePlane.WorldDirToPlane(velocity.Value) : (Vector2?)null;
            SetNavigationPoint(planePos, avoid, planeVel);
        }

        public void ClearNavigationPoint()
        {
            currentWaypoint.isValid = false;
        }

        public void SetFacingOverride(float angle)
        {
            facingOverride = true;
            facingAngle = angle;
        }

        public void SetFacingTarget(Vector2 direction)
        {
            if (!(direction.sqrMagnitude > 0.01f)) return;
            var angle = Vector2.SignedAngle(Vector2.up, direction);
            if (angle < 0f) angle += 360f;
            SetFacingOverride(angle);
        }

        public void ClearFacingOverride()
        {
            facingOverride = false;
        }
        partial void StoreDebugState(ObstacleScanner.ScanResult scan);
        partial void StoreDebugState(Vector2 goal, PathPlanner.DebugInfo path, Pilot.Output pilot);
        partial void StoreDebugState(float thrust, float strafe);
    }
} 
