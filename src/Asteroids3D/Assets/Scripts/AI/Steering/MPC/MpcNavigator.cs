using AI.Scanning;
using AI.Steering.MPC;
using Ships;
using Ships.Movement;
using UnityEngine;

namespace AI.Steering.MPC
{
    public partial class MpcNavigator : Navigator
    {
        [Header("Settings")]
        public Steering.MPC.Settings settings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        private Control[] bestSequence;
        private State[] predictedStates;
        private Config config;
        private float lastBestCost;

        public override void Initialize(Ship ship, Scout scout)
        {
            base.Initialize(ship, scout);
            
            var horizon = settings.Horizon;
            bestSequence = new Control[horizon];
            predictedStates = new State[horizon];
            
            config = BuildConfig(ship);
        }

        private Config BuildConfig(Ship ship)
        {
            var mass = ship.Movement.Mass;
            var shipSettings = ship.settings;
            
            return new Config
            {
                dt = settings.rolloutDt,
                horizon = settings.Horizon,
                maxSpeed = shipSettings.maxSpeed,
                maxYawRate = shipSettings.maxYawRate * Mathf.Deg2Rad,
                forwardAcc = shipSettings.forwardAccel / mass,
                reverseAcc = shipSettings.reverseAccel / mass,
                strafeAcc = shipSettings.maxStrafeForce / mass,
                alphaMax = shipSettings.rotationThrust * Mathf.Deg2Rad,
                damping = shipSettings.rotationDrag,
                
                wPos = settings.wPos,
                wVel = settings.wVel,
                wYaw = settings.wYaw,
                wYawRate = settings.wYawRate,
                wEffort = settings.wEffort,
                wSmoothness = settings.wSmoothness,
                wObstacle = settings.wObstacle,
                terminalMultiplier = settings.terminalMultiplier,
                obstacleThreshold = settings.obstacleThreshold
            };
        }

        public override void GenerateNavCommands(Ships.State state, ref Ships.Command cmd)
        {
            if (!ship || !currentWaypoint.isValid)
            {
                cmd.TargetAngle = state.Kinematics.Yaw;
                return;
            }

            RefreshWeights();

            if (HasArrived(state.Kinematics))
            {
                cmd.Thrust = 0;
                cmd.Strafe = 0;
                cmd.YawTorque = 0;
                cmd.RotateToTarget = false;
                return;
            }

            var mpcState = ToMpcState(state.Kinematics);
            var (obstacles, count) = GetObstacles();
            StoreDebugObstacles(obstacles, count);

            ShiftWarmStart();
            lastBestCost = Sampler.Solve(mpcState, bestSequence, currentWaypoint.position, 
                obstacles, count, config, settings.samples, settings.noiseStd, bestSequence);

            UpdatePredictedStates(mpcState);
            ApplyControl(ref cmd, bestSequence[0]);
        }

        private void RefreshWeights()
        {
            config.wPos = settings.wPos;
            config.wVel = settings.wVel;
            config.wYaw = settings.wYaw;
            config.wYawRate = settings.wYawRate;
            config.wEffort = settings.wEffort;
            config.wSmoothness = settings.wSmoothness;
            config.wObstacle = settings.wObstacle;
            config.terminalMultiplier = settings.terminalMultiplier;
            config.obstacleThreshold = settings.obstacleThreshold;
        }

        private bool HasArrived(Kinematics kin)
        {
            var toGoal = currentWaypoint.position - kin.Pos;
            return toGoal.sqrMagnitude < arriveRadius * arriveRadius && kin.Vel.sqrMagnitude < 0.1f;
        }

        private static MPC.State ToMpcState(Kinematics kin) => new()
        {
            pos = kin.Pos,
            vel = kin.Vel,
            yaw = kin.Yaw * Mathf.Deg2Rad,
            yawRate = kin.YawRate * Mathf.Deg2Rad
        };

        private (DetectedObstacle[] obstacles, int count) GetObstacles()
        {
            var obstacles = scout ? scout.Obstacles.DetectedBuffer : null;
            var count = enableObstacleAvoidance && scout ? scout.Obstacles.DetectedCount : 0;
            return (obstacles, count);
        }

        private void ShiftWarmStart() =>
            System.Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);

        private void UpdatePredictedStates(State initial)
        {
            var current = initial;
            for (var i = 0; i < predictedStates.Length; i++)
            {
                current = Model.Step(current, bestSequence[i], config);
                predictedStates[i] = current;
            }
        }

        private static void ApplyControl(ref Ships.Command cmd, Control u)
        {
            cmd.Thrust = u.thrust;
            cmd.Strafe = u.strafe;
            cmd.YawTorque = u.yawTorque;
            cmd.RotateToTarget = false;
        }

        protected override void OnSetNavigationPoint(bool avoid) { }

        partial void StoreDebugObstacles(DetectedObstacle[] obstacles, int count);
    }
}
