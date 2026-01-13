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
        public float arriveRadius = 10f;

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

        [Header("Steering Smoothing")]
        [Tooltip("Higher values react faster; 0 disables smoothing. Units: 1/seconds (approx).")]
        [Range(0, 20)] public float proportionalGain = 5f;

        private Ship ship;
        private Sensors sensors;
        private SteeringTuning tuning;
        private Waypoint currentWaypoint;
        private bool facingOverride;
        private float facingAngle;
        private float smoothThrust, smoothStrafe;

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
            var planeVel = velocity.HasValue ? GamePlane.WorldPointToPlane(velocity.Value) : (Vector2?)null;
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

        public void GenerateNavCommands(State state, ref Command cmd)
        {
            if (!ship || !currentWaypoint.isValid) {
                cmd.TargetAngle = state.Kinematics.Yaw;
                return;
            }

            var kin = state.Kinematics;
            var maxSpeed = ship.settings.maxSpeed;

            var scanConfig = new ObstacleScanner.Config
            {
                enabled = enableAvoidance,
                asteroidMask = asteroidMask,
                lookAheadTime = lookAheadTime,
                safeMargin = safeMargin,
                maxSpeed = maxSpeed,
                raysPerDirection = raysPerDirection,
                maxRayDegrees = maxRayDegrees,
                sphereCastRadius = sphereCastRadius
            };

            var obstacleScan = sensors.Obstacles.Scan(scanConfig, kin);
            var vpOut = ComputeNavigation(kin, maxSpeed, obstacleScan);

            ApplyControls(vpOut, ref cmd);
            StoreDebugState(obstacleScan);

            if (facingOverride)
            {
                cmd.TargetAngle = facingAngle;
            }
        }

        private Pilot.Output ComputeNavigation(Kinematics kin, float maxSpeed, ObstacleScanner.ScanResult obstacleScan)
        {
            var goal2D = currentWaypoint.position;
            var wpVel = currentWaypoint.velocity;

            var ppIn = new PathPlanner.Input(kin, goal2D, wpVel, avoidRadius, arriveRadius, maxSpeed,
                lookAheadTime, safeMargin, obstacleScan.Obstacles, tuning);

            var ppOut = PathPlanner.Compute(ppIn);
            var vpIn = new Pilot.Input(kin, ppOut.desiredVelocity, ppOut.desiredAccel, maxSpeed, tuning, facingOverride, true);
            var vpOut = Pilot.Compute(vpIn);

            StoreDebugState(goal2D, ppOut.dbg, vpOut);
            return vpOut;
        }

        private void ApplyControls(Pilot.Output vpOut, ref Command cmd)
        {
            var k = proportionalGain;
            var dt = Time.fixedDeltaTime;
            if (k > 0f)
            {
                smoothThrust += (vpOut.thrust - smoothThrust) * k * dt;
                smoothStrafe += (vpOut.strafe - smoothStrafe) * k * dt;
            }
            else
            {
                smoothThrust = vpOut.thrust;
                smoothStrafe = vpOut.strafe;
            }

            cmd.Thrust = smoothThrust;
            cmd.Strafe = smoothStrafe;
            cmd.RotateToTarget = true;
            cmd.TargetAngle = vpOut.rotTargetDeg;

            StoreDebugState(smoothThrust, smoothStrafe);
        }

        // Partial methods for editor debug - removed entirely in production
        partial void StoreDebugState(ObstacleScanner.ScanResult scan);
        partial void StoreDebugState(Vector2 goal, PathPlanner.DebugInfo path, Pilot.Output pilot);
        partial void StoreDebugState(float thrust, float strafe);
    }
} 
