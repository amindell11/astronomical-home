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
namespace Movement.MPC
{
    /// <summary>
    /// The ship's navigator: turns a <see cref="NavigationIntent"/> into per-frame
    /// movement commands via an MPC solver. This is the only navigator — the former
    /// abstract base was folded in once the Standard nav stack was retired.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public partial class Navigator : MonoBehaviour
    {
        // ── Shared control surface (waypoints, goals, intent) ──

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
        protected Dynamics dynamics;
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
        public MPCSettings mpcSettings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        private Control[] bestSequence;
        private State[] predictedStates;
        private Config config;
        private Control smoothedControl;
#if UNITY_EDITOR
        internal float lastBestCost;
        private State lastInitialState;
#endif
        private Control lastControl;
        private float previousDt;
        private Control[] resampleBuffer;
        private SolverBuffers solver;

        public void Initialize(Func<Ships.Command.State> stateProvider, Dynamics dynamics, Scout scout)
        {
            getState = stateProvider;
            this.dynamics = dynamics;
            this.scout = scout;
            currentWaypoint = new Waypoint { isValid = false };

            config = mpcSettings.ToConfig();
            config.maxBankAngleRad = dynamics.maxBankAngleRad;
            config.maxSpeedSq = dynamics.maxSpeed * dynamics.maxSpeed;
            config.maxYawRateSq = dynamics.maxYawRate * dynamics.maxYawRate;
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
            solver = new SolverBuffers();
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

            var mpcState = ToMpcState(state.kinematics);
            RefreshConfig(mpcState);
#if UNITY_EDITOR
            lastInitialState = mpcState;
#endif
            var scan = scout.ObstacleScan;
            StoreDebugObstacles(scan);
            ShiftWarmStart();

#if UNITY_EDITOR
            var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
            var boostCooldown = state.boostCooldownRemaining;
            // If cooldown exceeds entire horizon, skip boost sampling to save candidate quality
            var boostProb = boostCooldown > mpcSettings.horizonSeconds
                ? 0f : mpcSettings.boostSampleProbability;

            using (EditorProfilingScope.Begin("MPC.Navigator.Solve"))
            {
                // Scale noise inversely with dt so state-space exploration stays constant
                var noiseStd = mpcSettings.noiseStd * mpcSettings.rolloutDt / config.dt;
                lastBestCost = solver.Solve(mpcState, bestSequence,
                    scan, enableObstacleAvoidance,
                    GoalPos(), GoalVel(), enemyYaw, enemyYawRate,
                    projectileSpeed, config, dynamics,
                    mpcSettings.samples, noiseStd, lastControl,
                    enemyDynamics,
                    boostCooldown, boostProb,
                    mpcSettings.eliteFraction,
                    NavigationTargetForSolver());
            }
#if UNITY_EDITOR
            sw.Stop();
            lastSolveTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            lastCostBreakdown = EvaluateBreakdown(mpcState);
            LogSolverPerformanceIfNeeded();
#endif

            UpdatePredictedStates(mpcState);
            RunComparisonRollouts(mpcState, scan);
            lastControl = bestSequence[0];

            var raw = bestSequence[0];
            var a = mpcSettings.controlSmoothing;
            smoothedControl = new Control
            {
                thrust = math.lerp(raw.thrust, smoothedControl.thrust, a),
                strafe = math.lerp(raw.strafe, smoothedControl.strafe, a),
                yawTorque = math.lerp(raw.yawTorque, smoothedControl.yawTorque, a),
            };

            // Scale controls by urgency — below relaxMin = coast, above relaxMax = full authority
            if (mpcSettings.relaxMax > mpcSettings.relaxMin)
            {
                var normalizedCost = lastBestCost / config.horizon;
                var t = math.saturate((normalizedCost - mpcSettings.relaxMin) / (mpcSettings.relaxMax - mpcSettings.relaxMin));
                var urgency = math.pow(t, mpcSettings.relaxCurve);
                smoothedControl.thrust *= urgency;
                smoothedControl.strafe *= urgency;
                smoothedControl.yawTorque *= urgency;
            }

            ApplyControl(ref cmd, smoothedControl, raw.boost);
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

        private static MPC.State ToMpcState(Kinematics kin) => new()
        {
            pos = new float2(kin.pos.x, kin.pos.y),
            vel = new float2(kin.vel.x, kin.vel.y),
            yaw = kin.yaw * Mathf.Deg2Rad,
            yawRate = kin.yawRate * Mathf.Deg2Rad
        };

        private float2 GoalPos() => new(currentWaypoint.position.x, currentWaypoint.position.y);
        private float2 GoalVel() => new(currentWaypoint.velocity.x, currentWaypoint.velocity.y);

        private float2? NavigationTargetForSolver()
        {
            return navigationTarget.HasValue
                ? new float2(navigationTarget.Value.x, navigationTarget.Value.y)
                : (float2?)null;
        }

        private void ShiftWarmStart()
        {
            var newDt = config.dt;
            var dtChanged = previousDt > 0f && math.abs(newDt - previousDt) > 1e-6f;
            previousDt = newDt;

            if (dtChanged)
                ResampleWarmStart(newDt);
            else
                ShiftSequenceForward();
        }

        private void ShiftSequenceForward()
        {
            if (bestSequence.Length > 1)
                System.Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);
        }

        private void ResampleWarmStart(float newDt)
        {
            var horizon = bestSequence.Length;
            if (resampleBuffer == null || resampleBuffer.Length != horizon)
                resampleBuffer = new Control[horizon];

            for (var i = 0; i < horizon; i++)
            {
                var fIdx = i * newDt / previousDt + 1f; // +1 accounts for the one-step shift
                resampleBuffer[i] = SampleSequenceAt(fIdx, horizon);
            }

            System.Array.Copy(resampleBuffer, bestSequence, horizon);
        }

        private Control SampleSequenceAt(float index, int horizon)
        {
            var lo = (int)index;
            if (lo >= horizon - 1) return bestSequence[horizon - 1];

            var a = bestSequence[lo];
            var b = bestSequence[math.min(lo + 1, horizon - 1)];
            var frac = index - lo;
            return new Control
            {
                thrust = math.lerp(a.thrust, b.thrust, frac),
                strafe = math.lerp(a.strafe, b.strafe, frac),
                yawTorque = math.lerp(a.yawTorque, b.yawTorque, frac),
                boost = frac < 0.5f ? a.boost : b.boost,
            };
        }

        private void UpdatePredictedStates(State initial)
        {
            var current = initial;
            for (var i = 0; i < predictedStates.Length; i++)
            {
                current = Model.Step(current, bestSequence[i], config, dynamics);
                predictedStates[i] = current;
            }
        }

        private static void ApplyControl(ref Command cmd, Control u, float boost)
        {
            cmd.thrust = u.thrust;
            cmd.strafe = u.strafe;
            cmd.yawTorque = u.yawTorque;
            cmd.boost = boost;
        }

        private void RefreshConfig(State mpcState)
        {
            var facingRad = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN;
            config = mpcSettings.ToConfig(facingRad, goalMode, goalDesiredRange, goalRangeTolerance);
            config.maxBankAngleRad = dynamics.maxBankAngleRad;
            config.maxSpeedSq = dynamics.maxSpeed * dynamics.maxSpeed;
            config.maxYawRateSq = dynamics.maxYawRate * dynamics.maxYawRate;
            weightOverrides.Apply(ref config);

            if (mpcSettings.adaptiveDtScale > 0f)
            {
                float t;
                if (goalMode == GoalMode.Flee)
                {
                    // Flee: scale by closing speed (how fast the gap is changing)
                    var toGoal = GoalPos() - mpcState.pos;
                    var dist = math.length(toGoal);
                    var dir = dist > 1e-4f ? toGoal / dist : float2.zero;
                    var relVel = mpcState.vel - GoalVel();
                    var closingSpeed = math.abs(math.dot(relVel, dir));
                    t = math.saturate(closingSpeed / dynamics.maxSpeed);
                }
                else
                {
                    // Other modes: scale by distance to goal
                    var dist = math.length(GoalPos() - mpcState.pos);
                    t = math.saturate(dist / mpcSettings.adaptiveDtRefDistance);
                }

                var rawScale = 1f + t * mpcSettings.adaptiveDtScale;
                // Bin to 0.25 increments so dt changes infrequently
                var scale = math.round(rawScale * 4f) / 4f;
                config.dt *= scale;
                config.invDt = config.dt > 0f ? 1f / config.dt : 0f;
            }

            if (bestSequence.Length == config.horizon) return;
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
        }

        partial void RunComparisonRollouts(State mpcState, AI.Scanning.ObstacleScan scan);

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

        // ── Low-level control surface ──
        // Composed by ApplyIntent; also driven directly by play-mode tests.

        public void SetNavigationPoint(Vector2 point, bool avoid = false, Vector2? velocity = null)
        {
            currentWaypoint.position = point;
            currentWaypoint.velocity = velocity ?? Vector2.zero;
            currentWaypoint.isValid = true;
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

        /// <summary>
        /// Set a high-level routing override. When set, the MPC's position + heading costs
        /// pull toward this point instead of the goal (currentWaypoint). Range/Flee/tactical
        /// costs continue to use the goal.
        /// </summary>
        public void SetNavigationTarget(Vector2 plane)
        {
            navigationTarget = plane;
        }

        public void SetNavigationTargetWorld(Vector3 worldPos)
        {
            navigationTarget = GamePlane.WorldPointToPlane(worldPos);
        }

        public void ClearNavigationTarget()
        {
            navigationTarget = null;
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

        public void SetGoalMaintainRange(float desiredRange, float rangeTolerance)
        {
            goalMode = GoalMode.MaintainRange;
            goalDesiredRange = desiredRange;
            goalRangeTolerance = rangeTolerance;
        }

        public void SetGoalFlee()
        {
            goalMode = GoalMode.Flee;
        }

        public void ClearGoalMode()
        {
            goalMode = GoalMode.Waypoint;
            goalDesiredRange = 0f;
            goalRangeTolerance = 0f;
        }

        public void SetEnemyState(float yawDegrees, float yawRateDegrees, float projectileSpeed,
            Dynamics enemyDynamics = default)
        {
            enemyYaw = yawDegrees * Mathf.Deg2Rad;
            enemyYawRate = yawRateDegrees * Mathf.Deg2Rad;
            this.projectileSpeed = projectileSpeed;
            this.enemyDynamics = enemyDynamics;
        }

        public void ClearEnemyState()
        {
            enemyYaw = float.NaN;
            enemyYawRate = 0f;
            projectileSpeed = 0f;
            enemyDynamics = default;
        }

        public void SetWeightOverrides(WeightOverride[] overrides)
        {
            weightOverrides = overrides ?? Array.Empty<WeightOverride>();
        }

        public void ClearWeightOverrides()
        {
            weightOverrides = Array.Empty<WeightOverride>();
        }

        public void SetObstacleExclusion(Transform root)
        {
            scout.SetObstacleExclusion(root);
        }

        public void ClearObstacleExclusion()
        {
            scout.ClearObstacleExclusion();
        }

        private void OnDestroy()
        {
            solver?.Dispose();
        }

        partial void StoreDebugObstacles(ObstacleScan scan);
        partial void LogSolverPerformanceIfNeeded();
    }
}
