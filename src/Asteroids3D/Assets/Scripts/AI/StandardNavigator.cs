using AI.Computers;
using AI.Context;
using AI.Steering;
using Game;
using Ships;
using Ships.Movement;
using UnityEngine;

namespace AI
{
    public partial class StandardNavigator : Navigator
    {
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

        private SteeringTuning tuning;
        private Pilot pilot;

        public override void Initialize(Ship ship, Sensors sensors)
        {
            base.Initialize(ship, sensors);
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

        public override void GenerateNavCommands(State state, ref Command cmd)
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

        protected override void OnSetNavigationPoint(bool avoid)
        {
            enableAvoidance = avoid;
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

        partial void StoreDebugState(ObstacleScanner.ScanResult scan);
        partial void StoreDebugState(Vector2 goal, PathPlanner.DebugInfo path, Pilot.Output pilot);
        partial void StoreDebugState(float thrust, float strafe);
    }
}