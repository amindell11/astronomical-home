using System;
using AI;
using AI.Scanning;
using AI.States;
using Game;
using Ships;
using Ships.Command;
using Movement;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
namespace Movement.MPC
{
    /// <summary>
    /// The ship's navigator: turns a <see cref="NavigationIntent"/> into per-frame movement
    /// commands. It owns the control surface (waypoints, goals, enemy state, weight overrides)
    /// and drives an <see cref="Mpc"/> solver — building the solver's inputs each tick and
    /// applying its result. It holds no solver state or MPC math itself.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public partial class Navigator : MonoBehaviour
    {
        // ── Control surface (waypoints, goals, intent) ──

        public struct Waypoint
        {
            public Vector2 position;
            public Vector2 velocity;
            public bool isValid;
        }

        protected Scout scout;
        protected Waypoint currentWaypoint;
        protected Vector2? navigationTarget;
        protected bool facingOverride;
        protected float facingAngle;
        protected GoalMode goalMode;
        protected float goalDesiredRange;
        protected float goalRangeTolerance;
        protected float enemyYaw = float.NaN;
        protected float enemyYawRate;
        protected float projectileSpeed;
        protected Dynamics enemyDynamics;
        protected WeightOverride[] weightOverrides = Array.Empty<WeightOverride>();

        protected Command currentCommand;
        public Command CurrentCommand => currentCommand;

        protected Func<Ships.Command.State> getState;
        public float arriveRadius = 2f;

        public Waypoint CurrentWaypoint => currentWaypoint;

        [Header("Settings")]
        // Prefabs serialize this under its former name "settings".
        [FormerlySerializedAs("settings")]
        public MPCSettings mpcSettings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        private Mpc mpc;

        public void Initialize(Func<Ships.Command.State> stateProvider, Dynamics dynamics, Scout scout)
        {
            getState = stateProvider;
            this.scout = scout;
            currentWaypoint = new Waypoint { isValid = false };
            mpc = new Mpc(mpcSettings, dynamics);
        }

        private void FixedUpdate()
        {
            if (getState == null) return;
            currentCommand = default;
            GenerateNavCommands(getState(), ref currentCommand);
        }

        public void GenerateNavCommands(Ships.Command.State state, ref Command cmd)
        {
            using var _ = EditorProfilingScope.Begin("MPC.Navigator.GenerateNavCommands");
            if (!currentWaypoint.isValid || HasArrived(state.kinematics)) return;

            var scan = scout.ObstacleScan;
            var inputs = new MpcInputs
            {
                kinematics = state.kinematics,
                boostCooldown = state.boostCooldownRemaining,
                goalPos = GoalPos(),
                goalVel = GoalVel(),
                goalMode = goalMode,
                goalDesiredRange = goalDesiredRange,
                goalRangeTolerance = goalRangeTolerance,
                facingRad = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN,
                enemyYaw = enemyYaw,
                enemyYawRate = enemyYawRate,
                projectileSpeed = projectileSpeed,
                enemyDynamics = enemyDynamics,
                weightOverrides = weightOverrides,
                obstacleScan = scan,
                enableObstacleAvoidance = enableObstacleAvoidance,
                navigationTarget = NavigationTargetForSolver(),
            };

#if UNITY_EDITOR
            StoreDebugObstacles(scan);
            var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
            MpcResult result;
            using (EditorProfilingScope.Begin("MPC.Navigator.Solve"))
                result = mpc.Plan(in inputs);
#if UNITY_EDITOR
            sw.Stop();
            lastSolveTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            lastCostBreakdown = EvaluateBreakdown(mpc.LastInitialState);
            RunComparisonRollouts(mpc.LastInitialState, scan);
            LogSolverPerformanceIfNeeded();
#endif

            ApplyControl(ref cmd, in result);
        }

        private bool HasArrived(Kinematics kin)
        {
            var toGoal = currentWaypoint.position - kin.pos;
            var posArrived = toGoal.sqrMagnitude < arriveRadius * arriveRadius;
            var velStopped = kin.vel.sqrMagnitude < 0.1f;

            if (!posArrived || !velStopped) return false;

            if (!facingOverride) return true;
            var yawErr = Mathf.DeltaAngle(kin.yaw, facingAngle);
            return !(Mathf.Abs(yawErr) > 5f);
        }

        private float2 GoalPos() => new(currentWaypoint.position.x, currentWaypoint.position.y);
        private float2 GoalVel() => new(currentWaypoint.velocity.x, currentWaypoint.velocity.y);

        private float2? NavigationTargetForSolver() => navigationTarget.HasValue
            ? new float2(navigationTarget.Value.x, navigationTarget.Value.y)
            : (float2?)null;

        private static void ApplyControl(ref Command cmd, in MpcResult r)
        {
            cmd.thrust = r.thrust;
            cmd.strafe = r.strafe;
            cmd.yawTorque = r.yawTorque;
            cmd.boost = r.boost;
        }

        /// <summary>
        /// Single production entry point for driving the navigator. Applies the whole
        /// <see cref="NavigationIntent"/> in one place, resetting every field each call so
        /// the result depends only on the intent — never on prior state or call order.
        /// An invalid intent (<see cref="NavigationIntent.None"/>) resets the navigator to idle.
        /// The granular Set*/Clear* methods below are the low-level seam this composes
        /// (also used directly by tests); production code should call ApplyIntent instead.
        /// </summary>
        public void ApplyIntent(in NavigationIntent intent)
        {
            if (!intent.isValid)
            {
                ResetNavigation();
                return;
            }

            switch (intent.goalMode)
            {
                case GoalMode.MaintainRange:
                    SetGoalMaintainRange(intent.desiredRange, intent.rangeTolerance);
                    break;
                case GoalMode.Flee:
                    SetGoalFlee();
                    break;
                default:
                    ClearGoalMode();
                    break;
            }

            SetNavigationPoint(intent.goalPosition, true, intent.goalVelocity);

            if (intent.navigationTarget.HasValue)
                SetNavigationTarget(intent.navigationTarget.Value);
            else
                ClearNavigationTarget();

            if (intent.hasEnemy)
                SetEnemyState(intent.enemyYawDeg, intent.enemyYawRateDeg, intent.projectileSpeed,
                    intent.enemyDynamics);
            else
                ClearEnemyState();

            SetWeightOverrides(intent.weightOverrides);

            if (intent.obstacleExclusion)
                SetObstacleExclusion(intent.obstacleExclusion);
            else
                ClearObstacleExclusion();
        }

        /// <summary>Resets all navigation overrides to idle. Mirrors a fresh, goal-less navigator.</summary>
        public void ResetNavigation()
        {
            ClearNavigationPoint();
            ClearNavigationTarget();
            ClearGoalMode();
            ClearEnemyState();
            ClearObstacleExclusion();
            ClearWeightOverrides();
        }

        // ── Control surface ──
        // ApplyIntent composes the private helpers below. SetNavigationPoint and
        // SetFacingOverride stay public for direct "go here" commands and play-mode tests.

        public void SetNavigationPoint(Vector2 point, bool avoid = false, Vector2? velocity = null)
        {
            currentWaypoint.position = point;
            currentWaypoint.velocity = velocity ?? Vector2.zero;
            currentWaypoint.isValid = true;
        }

        private void ClearNavigationPoint()
        {
            currentWaypoint.isValid = false;
        }

        /// <summary>
        /// Set a high-level routing override. When set, the MPC's position + heading costs
        /// pull toward this point instead of the goal (currentWaypoint). Range/Flee/tactical
        /// costs continue to use the goal.
        /// </summary>
        private void SetNavigationTarget(Vector2 plane)
        {
            navigationTarget = plane;
        }

        private void ClearNavigationTarget()
        {
            navigationTarget = null;
        }

        public void SetFacingOverride(float angle)
        {
            facingOverride = true;
            facingAngle = angle;
        }

        private void SetGoalMaintainRange(float desiredRange, float rangeTolerance)
        {
            goalMode = GoalMode.MaintainRange;
            goalDesiredRange = desiredRange;
            goalRangeTolerance = rangeTolerance;
        }

        private void SetGoalFlee()
        {
            goalMode = GoalMode.Flee;
        }

        private void ClearGoalMode()
        {
            goalMode = GoalMode.Waypoint;
            goalDesiredRange = 0f;
            goalRangeTolerance = 0f;
        }

        private void SetEnemyState(float yawDegrees, float yawRateDegrees, float projectileSpeed,
            Dynamics enemyDynamics = default)
        {
            enemyYaw = yawDegrees * Mathf.Deg2Rad;
            enemyYawRate = yawRateDegrees * Mathf.Deg2Rad;
            this.projectileSpeed = projectileSpeed;
            this.enemyDynamics = enemyDynamics;
        }

        private void ClearEnemyState()
        {
            enemyYaw = float.NaN;
            enemyYawRate = 0f;
            projectileSpeed = 0f;
            enemyDynamics = default;
        }

        private void SetWeightOverrides(WeightOverride[] overrides)
        {
            weightOverrides = overrides ?? Array.Empty<WeightOverride>();
        }

        private void ClearWeightOverrides()
        {
            weightOverrides = Array.Empty<WeightOverride>();
        }

        private void SetObstacleExclusion(Transform root)
        {
            scout.SetObstacleExclusion(root);
        }

        private void ClearObstacleExclusion()
        {
            scout.ClearObstacleExclusion();
        }

        private void OnDestroy()
        {
            mpc?.Dispose();
        }

        partial void RunComparisonRollouts(State mpcState, ObstacleScan scan);
        partial void StoreDebugObstacles(ObstacleScan scan);
        partial void LogSolverPerformanceIfNeeded();
    }
}
