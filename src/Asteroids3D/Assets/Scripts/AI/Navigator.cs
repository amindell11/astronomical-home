using System;
using AI;
using AI.Context;
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
    /// <summary>Turns a <see cref="NavigationIntent"/> into per-frame movement commands: owns the control surface (waypoints, goals, enemy state, weight overrides) and drives an <see cref="Mpc"/> solver, holding no solver state or MPC math itself.</summary>
    [DefaultExecutionOrder(-60)]
    public partial class Navigator : MonoBehaviour
    {
        private const uint MpcSamplerStream = 1;

        public struct Waypoint
        {
            public Vector2 position;
            public Vector2 velocity;
            public bool isValid;
        }

        protected Scout scout;
        protected Waypoint currentWaypoint;
        protected bool facingOverride;
        protected float facingAngle;
        protected GoalMode goalMode;
        protected float goalDesiredRange;
        protected float goalRangeTolerance;
        protected float2 velocityReference;
        protected bool hasVelocityReference;
        protected float2 enemyPos;
        protected float2 enemyVel;
        protected float enemyYaw = float.NaN;
        protected float enemyYawRate;
        protected float projectileSpeed;
        protected Dynamics enemyDynamics;
        protected WeightOverride[] weightOverrides = Array.Empty<WeightOverride>();

        protected PilotCommand currentCommand;
        public PilotCommand CurrentCommand => currentCommand;

        protected IShipStatus context;
        public float arriveRadius = 2f;

        // Terminal cost-to-go field: the chase target whose shared field the solver samples at rollout ends. Set only for enemy-anchored pursuit (MaintainRange).
        private Transform terminalFieldTarget;
        private float selfMaxSpeed;

        // Flee escape field: a per-ship, lazily-created self-anchored grid sampled through the terminal hook, active only while fleeing a live target.
        private Field.FieldBaker fleeFieldBaker;
        private bool hasFleeThreat;
        private float2 fleeThreat;

        // Flee field geometry/policy — constants (mirroring NavFieldService's pursuit defaults) until benchmark evidence says a knob is worth exposing.
        private const int FleeFieldGridSize = 64;
        private const float FleeFieldCellSize = 3f;
        private const float FleeFieldShipRadiusBuffer = 2f;
        private const float FleeFieldRebuildInterval = 0.15f;
        // Fraction of a grid crossing charged to the worst-bearing exit: 1 = fleeing past the threat costs as much as detouring the entire grid.
        private const float FleeThreatBias = 1f;

        public Waypoint CurrentWaypoint => currentWaypoint;

        [Header("Settings")]
        // Prefabs serialize this under its former name "settings".
        [FormerlySerializedAs("settings")]
        public MpcSettings mpcSettings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        private Mpc mpc;
        private Dynamics dynamics; // consumed by the editor/gizmo partial

        public void Initialize(IShipStatus shipContext, Dynamics dynamics, Scout scout, SeedScope navScope)
        {
            context = shipContext;
            this.scout = scout;
            this.dynamics = dynamics;
            selfMaxSpeed = dynamics.maxSpeed;
            currentWaypoint = new Waypoint { isValid = false };
            if (!mpcSettings)
                mpcSettings = ScriptableObject.CreateInstance<MpcSettings>();
            mpc = new Mpc(mpcSettings, dynamics, navScope.Derive(MpcSamplerStream).ToUint());
        }

        public PilotCommand ComputeCommand()
        {
            using var _ = EditorProfilingScope.Begin("MPC.Navigator.GenerateNavCommands");
            var kin = context.Kinematics;
            if (ShouldIdle(kin))
                return currentCommand = default;

            var scan = scout.ObstacleScan;

            // Terminal cost-to-go field: pursuit shares one field per chase target via the service, Flee bakes a per-ship escape field; invalid/absent field = hook off.
            var terminalField = default(Field.TerminalFieldData);
            if (mpcSettings.wTerminal > 0f)
            {
                if (terminalFieldTarget)
                    Field.NavFieldService.Instance.TryGetData(
                        terminalFieldTarget, enemyPos, selfMaxSpeed, ObstacleFields.Active, out terminalField);
                else if (goalMode == GoalMode.Flee && hasFleeThreat)
                    GetFleeField(new float2(kin.pos.x, kin.pos.y), out terminalField);
            }

            var inputs = new MpcInputs
            {
                kinematics = kin,
                boostCooldown = context.BoostCooldownRemaining,
                goalPos = GoalPos(),
                goalVel = GoalVel(),
                velocityReference = velocityReference,
                goalMode = goalMode,
                goalDesiredRange = goalDesiredRange,
                goalRangeTolerance = goalRangeTolerance,
                facingRad = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN,
                enemyPos = enemyPos,
                enemyVel = enemyVel,
                enemyYaw = enemyYaw,
                enemyYawRate = enemyYawRate,
                projectileSpeed = projectileSpeed,
                enemyDynamics = enemyDynamics,
                weightOverrides = weightOverrides,
                obstacleScan = scan,
                enableObstacleAvoidance = enableObstacleAvoidance,
                terminalField = terminalField,
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

            ApplyControl(in result);
            return currentCommand;
        }

        /// <summary>Whether the MPC should sit idle (emit no command) this tick, dispatched per objective: waypoint/patrol idle once stopped at the destination, while the continuous MaintainRange/Flee objectives run whenever their target exists and never "arrive".</summary>
        internal bool ShouldIdle(Kinematics kin)
        {
            // Velocity mode has no waypoint; a zero reference is a valid "stop", so the arm flag — not the reference value — gates activity.
            if (goalMode == GoalMode.VelocityReference) return !hasVelocityReference;
            if (!currentWaypoint.isValid) return true;
            return goalMode switch
            {
                GoalMode.MaintainRange or GoalMode.Flee => false,
                _ => HasArrived(kin),
            };
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

        /// <summary>Pump + rebake + sample the per-ship escape field (timer-only, since the anchor is this ship). False while the first bake is in flight, so the terminal hook contributes 0 like pursuit's warm-up.</summary>
        private bool GetFleeField(float2 selfPos, out Field.TerminalFieldData data)
        {
            fleeFieldBaker ??= new Field.FieldBaker(
                FleeFieldGridSize, FleeFieldCellSize, FleeFieldShipRadiusBuffer,
                new Field.FieldBaker.Policy
                {
                    minRebuildInterval = FleeFieldRebuildInterval,
                    timerOnly = true,
                });
            fleeFieldBaker.Pump();
            fleeFieldBaker.RequestBake(
                Field.SeedSpec.ForBorderEscape(selfPos, fleeThreat, FleeThreatBias),
                ObstacleFields.Active);
            return fleeFieldBaker.TryGetData(selfMaxSpeed, out data);
        }

        private void ApplyControl(in MpcResult r)
        {
            currentCommand.thrust = r.thrust;
            currentCommand.strafe = r.strafe;
            currentCommand.yawTorque = r.yawTorque;
            currentCommand.boost = r.boost;
        }

        /// <summary>The single production entry point for driving the navigator, resetting every field each call so the result depends only on the intent, never on prior state or call order. An invalid intent resets to idle. Composes the granular Set*/Clear* seam below (which tests also drive directly).</summary>
        public void ApplyIntent(in NavigationIntent intent)
        {
            if (!intent.isValid)
            {
                ResetNavigation();
                return;
            }

            // Terminal cost-to-go routing: pursuit shares a per-target field; Flee bakes a per-ship escape field against the live threat.
            terminalFieldTarget = intent.goalMode == GoalMode.MaintainRange && intent.hasTarget
                ? intent.target.source
                : null;
            hasFleeThreat = intent.goalMode == GoalMode.Flee && intent.hasTarget;
            fleeThreat = hasFleeThreat
                ? new float2(intent.target.kinematics.pos.x, intent.target.kinematics.pos.y)
                : default;

            switch (intent.goalMode)
            {
                case GoalMode.VelocityReference:
                    SetVelocityReference(intent.velocityReference);
                    break;
                case GoalMode.MaintainRange:
                    SetGoalMaintainRange(intent.desiredRange, intent.rangeTolerance);
                    break;
                case GoalMode.Flee:
                    SetGoalFlee();
                    break;
                case GoalMode.Waypoint:
                default:
                    ClearGoalMode();
                    break;
            }

            // VelocityReference is driven by a commanded velocity, not a destination, so it sets no waypoint; every other mode is waypoint-anchored.
            if (intent.goalMode != GoalMode.VelocityReference)
                SetNavigationPoint(intent.goalPosition, true, intent.goalVelocity);

            if (intent.applyTacticalCosts && intent.hasTarget)
                SetEnemyState(intent.target, intent.projectileSpeed);
            else
                ClearEnemyState();

            SetWeightOverrides(intent.weightOverrides);

            if (intent.hasTarget)
                SetObstacleExclusion(intent.target.source);
            else
                ClearObstacleExclusion();
        }

        /// <summary>Resets all navigation overrides to idle. Mirrors a fresh, goal-less navigator.</summary>
        public void ResetNavigation()
        {
            terminalFieldTarget = null;
            hasFleeThreat = false;
            fleeThreat = default;
            ClearNavigationPoint();
            ClearGoalMode();
            ClearEnemyState();
            ClearObstacleExclusion();
            ClearWeightOverrides();
        }

        // SetNavigationPoint and SetFacingOverride stay public for direct "go here" commands and play-mode tests; ApplyIntent composes the rest.
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

        /// <summary>Arms velocity-tracker mode with a commanded world-plane velocity. Sets no waypoint — the reference IS the command — so <see cref="ShouldIdle"/> keys off the arm flag and a zero reference is a valid "stop".</summary>
        public void SetVelocityReference(Vector2 reference)
        {
            goalMode = GoalMode.VelocityReference;
            velocityReference = new float2(reference.x, reference.y);
            hasVelocityReference = true;
            ClearNavigationPoint();
        }

        public void SetFacingOverride(float angle)
        {
            facingOverride = true;
            facingAngle = angle;
        }

        internal void SetGoalMaintainRange(float desiredRange, float rangeTolerance)
        {
            goalMode = GoalMode.MaintainRange;
            goalDesiredRange = desiredRange;
            goalRangeTolerance = rangeTolerance;
            hasVelocityReference = false;
        }

        private void SetGoalFlee()
        {
            goalMode = GoalMode.Flee;
            hasVelocityReference = false;
        }

        internal void ClearGoalMode()
        {
            goalMode = GoalMode.Waypoint;
            goalDesiredRange = 0f;
            goalRangeTolerance = 0f;
            hasVelocityReference = false;
        }

        // Converts the enemy snapshot to MPC inputs; the MPC yaw convention (fwd = (-sin, cos)) lives here at the boundary, not in the strategy layer.
        private void SetEnemyState(in EnemyTarget target, float projectileSpeed)
        {
            var k = target.kinematics;
            enemyPos = new float2(k.pos.x, k.pos.y);
            enemyVel = new float2(k.vel.x, k.vel.y);
            var fwd = k.Forward;
            enemyYaw = Mathf.Atan2(-fwd.x, fwd.y);
            enemyYawRate = k.yawRate * Mathf.Deg2Rad;
            this.projectileSpeed = projectileSpeed;
            this.enemyDynamics = target.dynamics;
        }

        private void ClearEnemyState()
        {
            enemyPos = default;
            enemyVel = default;
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
            fleeFieldBaker?.Dispose();
        }

        partial void RunComparisonRollouts(State mpcState, ObstacleScan scan);
        partial void StoreDebugObstacles(ObstacleScan scan);
        partial void LogSolverPerformanceIfNeeded();
    }
}
