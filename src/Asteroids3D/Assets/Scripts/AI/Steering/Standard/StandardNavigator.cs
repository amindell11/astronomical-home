using System;
using AI.Scanning;
using Ships;
using Ships.Command;
using Ships.Movement;
using UnityEngine;

namespace AI.Steering.Standard
{
    [DefaultExecutionOrder(-60)]
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

        private Pilot pilot;

        public override void Initialize(Func<State> stateProvider, Dynamics dynamics, Scanning.Scout scout)
        {
            base.Initialize(stateProvider, dynamics, scout);
            
            pilot = new Pilot(dynamics, proportionalGain);
        }

        public override void GenerateNavCommands(State state, ref Command cmd)
        {
            if (!currentWaypoint.isValid) return;
            

            var kin = state.kinematics;
            
            var obstacleScan = ScanObstacles(kin);
            var pathOutput = PlanPath(kin, obstacleScan);
            var pilotOutput = GetPilotOutput(kin, pathOutput);
            
            cmd.thrust = pilotOutput.thrust;
            cmd.strafe = pilotOutput.strafe;
            cmd.yawTorque = ControlUtils.RotationPd(pilotOutput.rotTargetDeg, kin.yaw, kin.yawRate, dynamics.maxYawRate, 2f);

            StoreDebugState(currentWaypoint.position, pathOutput.dbg, pilotOutput);
            StoreDebugState(pilotOutput.thrust, pilotOutput.strafe);
        }

        protected override void OnSetNavigationPoint(bool avoid)
        {
            enableAvoidance = avoid;
        }

        private ObstacleScan ScanObstacles(Kinematics kin)
        {
            return scout.ObstacleScan;
        }

        private PathPlanner.Output PlanPath(Kinematics kin, ObstacleScan obstacles)
        {
            var pathInput = new PathPlanner.Input(
                kin, 
                currentWaypoint.position, 
                currentWaypoint.velocity, 
                avoidRadius, 
                arriveRadius,
                dynamics.maxSpeed,
                scout.lookAheadDist/dynamics.maxSpeed, 
                scout.safeMargin, 
                obstacles,
                dynamics);

            return PathPlanner.Compute(pathInput);
        }

        private Pilot.Output GetPilotOutput(Kinematics kin, PathPlanner.Output pathOutput)
        {
            float? facingTarget = facingOverride ? facingAngle : null;
            var pilotInput = new Pilot.Input(kin, pathOutput.desiredVelocity, pathOutput.desiredAccel, 
                dynamics.maxSpeed, facingTarget, useTiltedHeading); 
            return pilot.Compute(pilotInput);
        }

        partial void StoreDebugState(Vector2 goal, PathPlanner.DebugInfo path, Pilot.Output pilot);
        partial void StoreDebugState(float thrust, float strafe);
    }
}