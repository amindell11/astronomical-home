using System;
using AI;
using AI.Context;
using AI.Scanning;
using AI.States;
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

    /// <summary>Turns a <see cref="NavigationIntent"/> into per-frame movement commands: owns the control surface (velocity reference, enemy state, weight overrides) and drives an <see cref="Mpc"/> solver, holding no solver state or MPC math itself.</summary>
    [DefaultExecutionOrder(-60)]
    public class Navigator : MonoBehaviour
    {
        private const uint MpcSamplerStream = 1;

        protected Scout scout;
        protected bool facingOverride;
        protected float facingRadOverride;
        protected internal float2 velocityReference;
        protected bool hasVelocityReference;
        private bool boostCommanded;
        protected internal float2 enemyPos;
        protected internal float2 enemyVel;
        protected internal float enemyYaw = float.NaN;
        protected internal float enemyYawRate;
        protected internal float projectileSpeed;
        protected Dynamics enemyDynamics;
        protected internal AnchoredIntent anchored;
        protected WeightOverride[] weightOverrides = Array.Empty<WeightOverride>();

        protected PilotCommand currentCommand;
        public PilotCommand CurrentCommand => currentCommand;

        protected IShipStatus context;

        [Header("Settings")]
        [FormerlySerializedAs("settings")]
        public MpcSettings mpcSettings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        internal Mpc mpc;
        internal Dynamics dynamics;
        private SeedScope navScope;

        public void Initialize(IShipStatus shipContext, Dynamics dynamics, Scout scout, SeedScope navScope)
        {
            context = shipContext;
            this.scout = scout;
            this.dynamics = dynamics;
            this.navScope = navScope;
            if (!mpcSettings)
                mpcSettings = ScriptableObject.CreateInstance<MpcSettings>();
            mpc = new Mpc(mpcSettings, dynamics, navScope.Derive(MpcSamplerStream).ToUint());
        }

        /// <summary>Restores the freshly-initialized navigator: clears every override and rebuilds the solver so its warm-start plan and RNG stream replay from the spawn seed.</summary>
        public void ResetState()
        {
            ResetNavigation();
            currentCommand = default;
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

            var inputs = new MpcInputs
            {
                kinematics = kin,
                boostCooldown = context.BoostCooldownRemaining,
                velocityReference = velocityReference,
                facingRad = facingOverride ? facingRadOverride : float.NaN,
                enemyPos = enemyPos,
                enemyVel = enemyVel,
                enemyYaw = enemyYaw,
                enemyYawRate = enemyYawRate,
                projectileSpeed = projectileSpeed,
                enemyDynamics = enemyDynamics,
                anchored = anchored,
                weightOverrides = weightOverrides,
                obstacleScan = scan,
                enableObstacleAvoidance = enableObstacleAvoidance,
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
            LogSolverPerformanceIfNeeded();
#endif

            ApplyControl(in result);
            return currentCommand;
        }

        /// <summary>A zero reference is a valid "stop", so the arm flag — not the reference value — gates activity.</summary>
        internal bool ShouldIdle() => !hasVelocityReference;

        internal void ApplyControl(in MpcResult r)
        {
            currentCommand.thrust = r.thrust;
            currentCommand.strafe = r.strafe;
            currentCommand.yawTorque = r.yawTorque;
            currentCommand.boost = boostCommanded ? Mathf.Max(r.boost, 1f) : r.boost;
        }

        /// <summary>The single production entry point for driving the navigator, resetting every field each call so the result depends only on the intent, never on prior state or call order. An invalid intent resets to idle. Composes the granular Set*/Clear* seam below (which tests also drive directly).</summary>
        public void ApplyIntent(in NavigationIntent intent)
        {
            if (!intent.isValid)
            {
                ResetNavigation();
                return;
            }

            // Two facing sources in one intent is a contradiction, not a precedence question.
            var facingSources = (intent.hasFacing ? 1 : 0) + (intent.anchored.hasFacing ? 1 : 0) + (intent.aimAtTarget ? 1 : 0);
            if (facingSources > 1)
                throw new InvalidOperationException(
                    "NavigationIntent carries more than one facing source (facingRad / anchored facing / aimAtTarget) — a chooser must pick exactly one.");

            boostCommanded = intent.boost;
            SetVelocityReference(intent.velocityReference);

            if (intent.hasFacing)
                SetFacingOverride(intent.facingRad);
            else
                ClearFacingOverride();

            anchored = intent.anchored;
            // Legacy intercept aim IS the enemy anchor at offset 0, full authority — the one facing-target rule lives in the cost model.
            if (intent.aimAtTarget && intent.hasTarget)
            {
                anchored.hasFacing = true;
                anchored.facingOffsetRad = 0f;
                anchored.facingWeight = 1f;
            }

            if ((anchored.hasFacing || anchored.hasVelocity) && intent.hasTarget)
                SetEnemyState(intent.target, intent.projectileSpeed);
            else
                ClearEnemyState();   // targetless anchored channels collapse to the delegation priors

            SetWeightOverrides(intent.weightOverrides);

            if (intent.hasTarget)
                SetObstacleExclusion(intent.target.source);
            else
                ClearObstacleExclusion();
        }

        /// <summary>Resets all navigation overrides to idle. Mirrors a fresh, unarmed navigator.</summary>
        public void ResetNavigation()
        {
            boostCommanded = false;
            hasVelocityReference = false;
            velocityReference = default;
            anchored = default;
            ClearFacingOverride();
            ClearEnemyState();
            ClearObstacleExclusion();
            ClearWeightOverrides();
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

#if UNITY_EDITOR
        [Header("Debug Visualization")]
        [Tooltip("Horizontal prediction step for labels")]
        public int labelStep = 5;
        [Tooltip("Show cost breakdown in Inspector")]
        public bool showCostBreakdown = true;
        [Tooltip("Which cost to display: current ship state or full trajectory")]
        public CostBreakdownMode costBreakdownMode = CostBreakdownMode.CurrentState;
        [Tooltip("Log solver performance once per second")]
        public bool logSolverPerformance = false;

        [Header("Scene Gizmo Sub-Toggles")]
        public bool showObstacleCosts = true;
        public bool showTrajectoryCosts = true;
        public bool showControlInputs = true;
        [Tooltip("Render a random sampling of MPC candidate trajectories with rank-based alpha. " +
                 "Click a terminal-point handle in the scene view to inspect that candidate's breakdown.")]
        public bool showCandidateTrajectories = false;
        [Range(1, 256)]
        [Tooltip("How many of the (up to) 256 candidates to render. Subsample is reseeded each frame.")]
        public int candidateSampleCount = 32;
        [Range(0f, 5f)]
        [Tooltip("Visibility falloff with cost rank. 0 = all rendered candidates equally bright, " +
                 "higher = sharper focus on top-ranked. Default 2 ≈ worst-shown at ~13% of best's alpha.")]
        public float candidateAlphaFalloff = 2f;
        [Tooltip("World-space offset from ship for the control input panel")]
        public Vector3 controlPanelOffset = new(0f, 2.5f, 0f);

        [NonSerialized] public int selectedCandidateIndex = -1;
        // Candidate subsample drawn this frame, sorted by cost ascending; shared scratch between the gizmo pass and the scene-view selection handles.
        internal int[] visibleCandidateIndices;
        internal int visibleCount;

        private float nextLogTime;
        internal DetectedObstacle[] dbgObstacles;
        internal int dbgObstacleCount;
        public CostBreakdown lastCostBreakdown;
        public float lastSolveTimeMs;

        internal SolverBuffers solver => mpc?.Solver;
        internal Config config => mpc != null ? mpc.Config : default;
        internal Control[] bestSequence => mpc?.BestSequence;
        internal State[] predictedStates => mpc?.PredictedStates;
        internal State lastInitialState => mpc != null ? mpc.LastInitialState : default;
        internal Control lastControl => mpc != null ? mpc.LastControl : default;
        internal float lastBestCost => mpc != null ? mpc.LastBestCost : 0f;

        private void StoreDebugObstacles(ObstacleScan scan)
        {
            if (dbgObstacles == null || dbgObstacles.Length < scan.count)
                dbgObstacles = new DetectedObstacle[Mathf.Max(scan.count, 32)];

            dbgObstacleCount = scan.count;
            for (var i = 0; i < scan.count; i++)
                dbgObstacles[i] = scan.buffer[i];
        }

        private CostBreakdown EvaluateBreakdown(State mpcState)
        {
            var input = solver.BuildCostInput(velocityReference, enemyPos, enemyVel, enemyYaw, enemyYawRate, projectileSpeed, mpcState.vel, anchored);
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
