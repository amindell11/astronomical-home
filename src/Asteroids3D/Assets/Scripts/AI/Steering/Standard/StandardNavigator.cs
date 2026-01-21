using AI.Computers;
using AI.Context;
using AI.Scanning;
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
        public float avoidRadius = .5f;
        [Tooltip("Toggle obstacle avoidance logic on/off")] public bool enableAvoidance = false;

        [Header("Steering")]
        [Tooltip("Higher values react faster; 0 disables smoothing. Units: 1/seconds (approx).")]
        [Range(0, 20)] public float proportionalGain = 5f;
        [Tooltip("Use tilted heading when strafing for more natural flight")]
        [SerializeField] private bool useTiltedHeading = true;

        private SteeringTuning tuning;
        private Pilot pilot;

        public override void Initialize(Ship ship, Scout scout)
        {
            base.Initialize(ship, scout);
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

            StoreDebugState(currentWaypoint.position, pathOutput.dbg, pilotOutput);
            StoreDebugState(pilotOutput.thrust, pilotOutput.strafe);
        }

        protected override void OnSetNavigationPoint(bool avoid)
        {
            enableAvoidance = avoid;
        }

        private ObstacleScanResult ScanObstacles(Kinematics kin)
        {
            return scout.ScanObstacles();
        }

        private PathPlanner.Output PlanPath(Kinematics kin, ObstacleScanResult obstacleScan)
        {
            var pathInput = new PathPlanner.Input(
                kin, 
                currentWaypoint.position, 
                currentWaypoint.velocity, 
                avoidRadius, 
                arriveRadius,
                ship.settings.maxSpeed,
                scout.lookAheadDist/ship.settings.maxSpeed, 
                scout.safeMargin, 
                obstacleScan.Obstacles, 
                obstacleScan.hitCount,
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

        partial void StoreDebugState(Vector2 goal, PathPlanner.DebugInfo path, Pilot.Output pilot);
        partial void StoreDebugState(float thrust, float strafe);
    }
}