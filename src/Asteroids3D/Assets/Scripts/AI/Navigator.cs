using System;
using AI;
using AI.Context;
using Ships.Command;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
namespace Movement.MPC
{
#if UNITY_EDITOR
    public enum CostBreakdownMode
    {
        CurrentState,
        Trajectory,
    }
#endif

    /// <summary>Turns a <see cref="NavObjective"/> into per-frame movement commands: owns the control surface (velocity reference, enemy state) and drives an <see cref="Mpc"/> solver, holding no solver state or MPC math itself.</summary>
    [DefaultExecutionOrder(-60)]
    public class Navigator : MonoBehaviour
    {
        private const uint MpcSamplerStream = 1;

        protected internal Scout scout;
        protected bool facingOverride;
        protected float facingRadOverride;
        protected internal float2 velocityReference;
        protected bool hasVelocityReference;
        protected internal float2 enemyPos;
        protected internal float2 enemyVel;
        protected internal float enemyYaw = float.NaN;
        protected internal float enemyYawRate;
        protected internal float projectileSpeed;
        protected Dynamics enemyDynamics;
        protected internal IntentSentence sentence;
        protected internal ReferentSnapshot referent1;
        protected internal ReferentSnapshot referent2;
        protected internal ReferentSnapshot referent3;

        protected PilotCommand currentCommand;
        public PilotCommand CurrentCommand => currentCommand;
        private double lastSolveFixedTime = double.NaN;

        protected IShipStatus context;

        [Header("Settings")]
        [FormerlySerializedAs("settings")]
        public MpcSettings mpcSettings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        internal Mpc mpc;
        internal Dynamics dynamics;
        private SeedScope navScope;

        /// <summary>Ballistics never cross the decision seam: the host injects our own primary muzzle speed once, and intercept-lead geometry stays the solver's.</summary>
        public void Initialize(IShipStatus shipContext, Dynamics dynamics, Scout scout, SeedScope navScope,
            float primaryProjectileSpeed)
        {
            context = shipContext;
            this.scout = scout;
            this.dynamics = dynamics;
            this.navScope = navScope;
            projectileSpeed = primaryProjectileSpeed;
            if (!mpcSettings)
                mpcSettings = ScriptableObject.CreateInstance<MpcSettings>();
            mpc = new Mpc(mpcSettings, dynamics, navScope.Derive(MpcSamplerStream).ToUint());
        }

        /// <summary>Restores the freshly-initialized navigator: clears every override and rebuilds the solver so its warm-start plan and RNG stream replay from the spawn seed.</summary>
        public void ResetState()
        {
            ResetNavigation();
            currentCommand = default;
            lastSolveFixedTime = double.NaN;
            mpc?.Dispose();
            mpc = new Mpc(mpcSettings, dynamics, navScope.Derive(MpcSamplerStream).ToUint());
        }

        public PilotCommand ComputeCommand()
        {
            using var _ = EditorProfilingScope.Begin("MPC.Navigator.GenerateNavCommands");
            var kin = context.Kinematics;
            if (ShouldIdle())
                return currentCommand = default;

            var scan = scout.ObstacleScan;

            // Idle gaps skip solves, so dt spans whole ticks since the last solve — tick-counted
            // from the double clock so absolute session time never perturbs the float dt — capped
            // at one horizon (beyond that the warm start is fully consumed either way).
            var now = Time.fixedTimeAsDouble;
            var tickSeconds = Time.fixedDeltaTime;
            var ticksSinceSolve = double.IsNaN(lastSolveFixedTime)
                ? 1
                : System.Math.Max(1, (int)System.Math.Round((now - lastSolveFixedTime) / tickSeconds));
            lastSolveFixedTime = now;
            var sinceLastSolve = Mathf.Min(ticksSinceSolve * tickSeconds, mpcSettings.horizonSeconds);

            var inputs = new MpcInputs
            {
                kinematics = kin,
                dt = sinceLastSolve,
                velocityReference = CostVelocityReference,
                facingRad = facingOverride ? facingRadOverride : float.NaN,
                enemyPos = enemyPos,
                enemyVel = enemyVel,
                enemyYaw = enemyYaw,
                enemyYawRate = enemyYawRate,
                projectileSpeed = projectileSpeed,
                enemyDynamics = enemyDynamics,
                sentence = sentence,
                referent1 = referent1,
                referent2 = referent2,
                referent3 = referent3,
                obstacleScan = scan,
                enableObstacleAvoidance = enableObstacleAvoidance,
            };

#if UNITY_EDITOR
            var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
            MpcResult result;
            using (EditorProfilingScope.Begin("MPC.Navigator.Solve"))
                result = mpc.Plan(in inputs);
#if UNITY_EDITOR
            sw.Stop();
            lastSolveTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            lastCostBreakdown = EvaluateBreakdown(mpc.LastInitialState);
            LogSolverPerformanceIfNeeded();
#endif

            ApplyControl(in result);
            return currentCommand;
        }

        /// <summary>A zero reference is a valid "stop", so the arm flags — not the values — gate activity: idle iff no world reference and no armed sentence slot.</summary>
        internal bool ShouldIdle() => !hasVelocityReference && !sentence.AnyArmed;

        /// <summary>What the cost layer sees as the world velocity command: NaN when disarmed, so a sentence-only objective never reads as a commanded stop.</summary>
        internal float2 CostVelocityReference =>
            hasVelocityReference ? velocityReference : new float2(float.NaN, float.NaN);

        internal void ApplyControl(in MpcResult r)
        {
            currentCommand.thrust = r.thrust;
            currentCommand.strafe = r.strafe;
            currentCommand.yawTorque = r.yawTorque;
        }

        /// <summary>The single production entry point for driving the navigator, resetting every field each call so the result depends only on the objective, never on prior state or call order. Composes the granular Set*/Clear* seam below (which tests also drive directly). The objective names its referents by identity; <paramref name="resolvedAnchor"/> and the seat snapshots are their kinematics as the host resolved them this tick, so re-applying a held decision tracks live referents and a dead rock rides in as an invalid snapshot (its slots drop to weight 0).</summary>
        public void ApplyObjective(in NavObjective objective, in EnemyTarget resolvedAnchor,
            in ReferentSnapshot rockSeat1 = default, in ReferentSnapshot rockSeat2 = default,
            in ReferentSnapshot rockSeat3 = default)
        {
            if (objective.IsIdle)
            {
                ResetNavigation();
                return;
            }

            if (objective.hasPlanarVelocity)
                SetVelocityReference(objective.planarVelocity);
            else
            {
                hasVelocityReference = false;
                velocityReference = default;
            }
            ClearFacingOverride();
            sentence = objective.sentence;
            referent1 = rockSeat1;
            referent2 = rockSeat2;
            referent3 = rockSeat3;

            // The builder can only arm instance slots through an anchor, so the anchor is live whenever one is.
            if (sentence.aim.armed || sentence.pos.armed || sentence.vel.armed)
                SetEnemyState(resolvedAnchor);
            else
                ClearEnemyState();
        }

        /// <summary>Resets all navigation overrides to idle. Mirrors a fresh, unarmed navigator.</summary>
        public void ResetNavigation()
        {
            hasVelocityReference = false;
            velocityReference = default;
            sentence = default;
            referent1 = default;
            referent2 = default;
            referent3 = default;
            ClearFacingOverride();
            ClearEnemyState();
        }

        /// <summary>Arms the tracker with a commanded world-plane velocity — the reference IS the command, so <see cref="ShouldIdle"/> keys off the arm flag and a zero reference is a valid "stop".</summary>
        public void SetVelocityReference(Vector2 reference)
        {
            velocityReference = new float2(reference.x, reference.y);
            hasVelocityReference = true;
        }

        public void SetFacingOverride(float facingRad)
        {
            facingOverride = true;
            facingRadOverride = facingRad;
        }

        public void ClearFacingOverride()
        {
            facingOverride = false;
        }

        // Converts the enemy snapshot to MPC inputs; the MPC yaw convention (fwd = (-sin, cos)) lives here at the boundary, not in the policy layer.
        private void SetEnemyState(in EnemyTarget anchor)
        {
            var k = anchor.kinematics;
            enemyPos = new float2(k.pos.x, k.pos.y);
            enemyVel = new float2(k.vel.x, k.vel.y);
            var fwd = k.Forward;
            enemyYaw = Mathf.Atan2(-fwd.x, fwd.y);
            enemyYawRate = k.yawRate * Mathf.Deg2Rad;
            enemyDynamics = anchor.dynamics;
        }

        // Leaves projectileSpeed alone: it is our own ballistics, injected once, not part of the enemy snapshot.
        private void ClearEnemyState()
        {
            enemyPos = default;
            enemyVel = default;
            enemyYaw = float.NaN;
            enemyYawRate = 0f;
            enemyDynamics = default;
        }

        private void OnDestroy()
        {
            mpc?.Dispose();
        }

#if UNITY_EDITOR
        internal SolverBuffers solver => mpc?.Solver;
        internal Config config => mpc != null ? mpc.Config : default;
        internal Control[] bestSequence => mpc?.BestSequence;
        internal State[] predictedStates => mpc?.PredictedStates;
        internal State lastInitialState => mpc != null ? mpc.LastInitialState : default;
        internal Control lastControl => mpc != null ? mpc.LastControl : default;

        [NonSerialized] public int selectedCandidateIndex = -1;
        // Scene-view scratch shared with the candidate-selection handles; sorted by cost ascending.
        internal int[] visibleCandidateIndices;
        internal int visibleCount;

        [Header("Debug Visualization")]
        [Tooltip("Show cost breakdown in Inspector")]
        public bool showCostBreakdown = true;
        [Tooltip("Which cost to display: current ship state or full trajectory")]
        public CostBreakdownMode costBreakdownMode = CostBreakdownMode.CurrentState;
        [Tooltip("Log solver performance once per second")]
        public bool logSolverPerformance = false;

        [Tooltip("Draw the sampled candidate trajectory fan, and admit candidate-selection handles in the scene view")]
        public bool showCandidateTrajectories = true;
        [Tooltip("Color the predicted path by cost and label it periodically")]
        public bool showTrajectoryCosts = true;
        [Tooltip("Draw the collision hulls and turn-away bite ranges the solver tests")]
        public bool showObstacleCosts = true;
        [Tooltip("Draw the THR/STR/YAW applied-control bars")]
        public bool showControlInputs = true;
        [Min(1)]
        [Tooltip("How many of the sampled candidates to draw, best-cost first")]
        public int candidateSampleCount = 32;
        [Tooltip("How fast candidate opacity falls off with cost rank")]
        public float candidateAlphaFalloff = 2f;
        [Min(1)]
        [Tooltip("Label every Nth predicted node")]
        public int labelStep = 5;
        [Tooltip("Where the control-bar panel sits relative to the ship")]
        public Vector3 controlPanelOffset = new(0f, 2.5f, 0f);

        private float nextLogTime;
        public CostBreakdown lastCostBreakdown;
        public float lastSolveTimeMs;

        internal float lastBestCost => mpc != null ? mpc.LastBestCost : 0f;

        private CostBreakdown EvaluateBreakdown(State mpcState)
        {
            var input = solver.BuildCostInput(CostVelocityReference, enemyPos, enemyVel, enemyYaw, enemyYawRate, projectileSpeed, mpcState.vel, sentence, referent1, referent2, referent3);
            if (costBreakdownMode == CostBreakdownMode.CurrentState)
                return Cost.EvaluateBreakdown(mpcState, bestSequence[0], lastControl, input, config);
            return Cost.EvaluateTrajectoryBreakdown(mpcState, bestSequence, input, config, dynamics, lastControl);
        }

        private void LogSolverPerformanceIfNeeded()
        {
            if (!logSolverPerformance || !(Time.time > nextLogTime)) return;
            UnityEngine.Debug.Log($"[MPC] {gameObject.name} | Solve: {lastSolveTimeMs:F2}ms | Cost: {lastBestCost:F1}");
            nextLogTime = Time.time + 1f;
        }
#endif
    }
}
