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
        protected bool facingOverride;
        protected float facingAngle;
        protected GoalMode goalMode;
        protected float goalDesiredRange;
        protected float goalRangeTolerance;
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

        // Terminal cost-to-go field (Track B3): the chase target whose shared field the solver
        // samples at rollout ends. Set only for enemy-anchored pursuit (MaintainRange); Flee is
        // out of scope (trade study §7.4).
        private Transform terminalFieldTarget;
        private float selfMaxSpeed;

        public Waypoint CurrentWaypoint => currentWaypoint;

        [Header("Settings")]
        // Prefabs serialize this under its former name "settings".
        [FormerlySerializedAs("settings")]
        public MpcSettings mpcSettings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        private Mpc mpc;
        private Dynamics dynamics;

        // ── Gap layer (detector + injected bank-through primitives) ──
        private GapDetector gapDetector;
        private DetectedGap[] gapBuffer = new DetectedGap[16];
        private Control[] injectedBuffer;      // flattened horizon-length sequences
        private Control[] cachedGapElite;      // last winning injected sequence, re-seeded next solve
        private bool hasCachedGapElite;

        public void Initialize(IShipStatus shipContext, Dynamics dynamics, Scout scout)
        {
            context = shipContext;
            this.scout = scout;
            this.dynamics = dynamics;
            selfMaxSpeed = dynamics.maxSpeed;
            currentWaypoint = new Waypoint { isValid = false };
            if (!mpcSettings)
                mpcSettings = ScriptableObject.CreateInstance<MpcSettings>();
            mpc = new Mpc(mpcSettings, dynamics);
            gapDetector = new GapDetector();
        }

        public PilotCommand ComputeCommand()
        {
            using var _ = EditorProfilingScope.Begin("MPC.Navigator.GenerateNavCommands");
            var kin = context.Kinematics;
            if (!currentWaypoint.isValid || HasArrived(kin))
                return currentCommand = default;

            var scan = scout.ObstacleScan;
            var injectedCount = BuildGapPrimitives(kin, scan);

            // Terminal cost-to-go field: one shared field per chase target, sampled by the
            // solver at each rollout's terminal state. Invalid/absent field = hook off.
            var terminalField = default(Field.TerminalFieldData);
            if (terminalFieldTarget && mpcSettings.wTerminal > 0f)
                Field.NavFieldService.Instance.TryGetData(
                    terminalFieldTarget, enemyPos, selfMaxSpeed, out terminalField);


            var inputs = new MpcInputs
            {
                kinematics = kin,
                boostCooldown = context.BoostCooldownRemaining,
                goalPos = GoalPos(),
                goalVel = GoalVel(),
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
                injectedControls = injectedBuffer,
                injectedCount = injectedCount,
                terminalField = terminalField,
            };

#if UNITY_EDITOR
            StoreDebugObstacles(scan);
            var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
            MpcResult result;
            using (EditorProfilingScope.Begin("MPC.Navigator.Solve"))
                result = mpc.Plan(in inputs);
            CacheWinningInjection(injectedCount);
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

        /// <summary>
        /// Runs the gap detector over the obstacle scan and synthesizes bank-through-gap
        /// primitive sequences into <see cref="injectedBuffer"/> for CEM injection: top-k
        /// gaps by score with switch hysteresis, plus the previous tick's winning injected
        /// sequence (if any) as an extra seed. Returns the injected sequence count.
        /// </summary>
        private int BuildGapPrimitives(in Kinematics kin, in ObstacleScan scan)
        {
            if (!enableObstacleAvoidance || mpcSettings.gapTopK <= 0 || scan.count == 0)
                return 0;

            var horizon = mpcSettings.Horizon;
            var maxSequences = mpcSettings.gapTopK * mpcSettings.primitivesPerGap + 1;
            if (injectedBuffer == null || injectedBuffer.Length != maxSequences * horizon)
            {
                injectedBuffer = new Control[maxSequences * horizon];
                cachedGapElite = new Control[horizon];
                hasCachedGapElite = false;
            }

            var state = new State
            {
                pos = new float2(kin.pos.x, kin.pos.y),
                vel = new float2(kin.vel.x, kin.vel.y),
                yaw = kin.yaw * Mathf.Deg2Rad,
                yawRate = kin.yawRate * Mathf.Deg2Rad,
            };
            var hullFull = dynamics.shipRadius + mpcSettings.collisionSafetyMargin;
            var hullBank = dynamics.shipRadius * Mathf.Cos(dynamics.maxBankAngleRad)
                           + mpcSettings.collisionSafetyMargin;

            var gapCount = gapDetector.Detect(state.pos, scan, hullFull, hullBank,
                mpcSettings.gapMaxRange, gapBuffer);

            var goalBearing = GapDetector.Bearing(state.pos, GoalPos());
            var speed = math.length(state.vel);
            for (var i = 0; i < gapCount; i++)
                gapBuffer[i].score = GapDetector.Score(in gapBuffer[i], state.yaw, speed,
                    goalBearing, dynamics.maxYawRate);

            var chosen = gapDetector.ChooseGap(gapBuffer, gapCount, mpcSettings.gapSwitchMargin);
            StoreDebugGaps(gapBuffer, gapCount, chosen);
            if (chosen < 0) return 0;

            // Inject the chosen gap first, then the remaining gaps in score order up to top-k.
            var injectedCount = GapPrimitives.Synthesize(in gapBuffer[chosen], in state, in dynamics,
                mpcSettings.rolloutDt, horizon, mpcSettings.primitivesPerGap, injectedBuffer, 0);
            for (var k = 1; k < mpcSettings.gapTopK; k++)
            {
                // Next-best unconsumed gap (consumed ones have score = -inf).
                var next = -1;
                for (var i = 0; i < gapCount; i++)
                {
                    if (i == chosen || float.IsNegativeInfinity(gapBuffer[i].score)) continue;
                    if (next < 0 || gapBuffer[i].score > gapBuffer[next].score) next = i;
                }
                if (next < 0) break;
                injectedCount += GapPrimitives.Synthesize(in gapBuffer[next], in state, in dynamics,
                    mpcSettings.rolloutDt, horizon, mpcSettings.primitivesPerGap,
                    injectedBuffer, injectedCount * horizon);
                gapBuffer[next].score = float.NegativeInfinity; // consume so the next pass skips it
            }

            // Re-seed last tick's winning injected sequence so a successful thread persists
            // across solves even when this tick's synthesis shifts its timing.
            if (hasCachedGapElite && injectedCount < maxSequences)
            {
                System.Array.Copy(cachedGapElite, 0, injectedBuffer, injectedCount * horizon, horizon);
                injectedCount++;
            }

            return injectedCount;
        }

        /// <summary>If an injected sequence won the last solve outright, cache it for re-injection.</summary>
        private void CacheWinningInjection(int injectedCount)
        {
            if (injectedCount <= 0) return;
            var bestIndex = mpc.Solver.LastBestIndex;
            if (bestIndex < 1 || bestIndex > injectedCount) return;

            var horizon = mpcSettings.Horizon;
            System.Array.Copy(injectedBuffer, (bestIndex - 1) * horizon, cachedGapElite, 0, horizon);
            hasCachedGapElite = true;
        }

        private void ApplyControl(in MpcResult r)
        {
            currentCommand.thrust = r.thrust;
            currentCommand.strafe = r.strafe;
            currentCommand.yawTorque = r.yawTorque;
            currentCommand.boost = r.boost;
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

            // Terminal cost-to-go routing applies to enemy-anchored pursuit only.
            terminalFieldTarget = intent.goalMode == GoalMode.MaintainRange && intent.hasTarget
                ? intent.target.source
                : null;

            switch (intent.goalMode)
            {
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
            ClearNavigationPoint();
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

        // Converts the enemy snapshot to the MPC's enemy inputs. The MPC yaw convention
        // (fwd = (-sin, cos)) lives here, at the MPC boundary — not in the strategy layer.
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
        }

        partial void RunComparisonRollouts(State mpcState, ObstacleScan scan);
        partial void StoreDebugObstacles(ObstacleScan scan);
        partial void StoreDebugGaps(DetectedGap[] gaps, int count, int chosen);
        partial void LogSolverPerformanceIfNeeded();
    }
}
