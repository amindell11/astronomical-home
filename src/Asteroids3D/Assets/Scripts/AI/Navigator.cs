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
        private bool boostCommanded;
        protected internal float2 enemyPos;
        protected internal float2 enemyVel;
        protected internal float enemyYaw = float.NaN;
        protected internal float enemyYawRate;
        protected internal float projectileSpeed;
        protected Dynamics enemyDynamics;
        protected internal AnchoredIntent anchored;

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

        /// <summary>A zero reference is a valid "stop", so the arm flag — not the reference value — gates activity.</summary>
        internal bool ShouldIdle() => !hasVelocityReference;

        internal void ApplyControl(in MpcResult r)
        {
            currentCommand.thrust = r.thrust;
            currentCommand.strafe = r.strafe;
            currentCommand.yawTorque = r.yawTorque;
            currentCommand.boost = boostCommanded ? Mathf.Max(r.boost, 1f) : r.boost;
        }

        /// <summary>The single production entry point for driving the navigator, resetting every field each call so the result depends only on the objective, never on prior state or call order. Composes the granular Set*/Clear* seam below (which tests also drive directly).</summary>
        public void ApplyObjective(in NavObjective objective)
        {
            if (objective.IsIdle)
            {
                ResetNavigation();
                return;
            }

            SetVelocityReference(objective.planarVelocity);
            ClearFacingOverride();
            anchored = objective.anchored;

            // The builder can only arm these channels through an anchor, so the anchor is live whenever they are.
            if (anchored.hasFacing || anchored.hasVelocity)
                SetEnemyState(objective.anchor);
            else
                ClearEnemyState();
        }

        /// <summary>The ability lane's passthrough: a commanded boost floors the solver's own boost channel for this tick.</summary>
        public void CommandBoost(bool commanded)
        {
            boostCommanded = commanded;
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

        // Solve state the diagnostic painters read; unguarded because painters compile into the player.
        internal SolverBuffers solver => mpc?.Solver;
        internal Config config => mpc != null ? mpc.Config : default;
        internal Control[] bestSequence => mpc?.BestSequence;
        internal State[] predictedStates => mpc?.PredictedStates;
        internal State lastInitialState => mpc != null ? mpc.LastInitialState : default;
        internal Control lastControl => mpc != null ? mpc.LastControl : default;

        [NonSerialized] public int selectedCandidateIndex = -1;
        // Shared scratch between the painter and the scene-view selection handles; sorted by cost ascending.
        internal int[] visibleCandidateIndices;
        internal int visibleCount;

#if UNITY_EDITOR
        [Header("Debug Visualization")]
        [Tooltip("Show cost breakdown in Inspector")]
        public bool showCostBreakdown = true;
        [Tooltip("Which cost to display: current ship state or full trajectory")]
        public CostBreakdownMode costBreakdownMode = CostBreakdownMode.CurrentState;
        [Tooltip("Log solver performance once per second")]
        public bool logSolverPerformance = false;

        private float nextLogTime;
        public CostBreakdown lastCostBreakdown;
        public float lastSolveTimeMs;

        internal float lastBestCost => mpc != null ? mpc.LastBestCost : 0f;

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
