#if UNITY_EDITOR
using AI;
using AI.Debug;
using AI.Scanning;
using AI.States;
using Movement;
using Game;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Movement.MPC
{
    public enum CostBreakdownMode
    {
        CurrentState,
        Trajectory,
    }

    public partial class Navigator
    {
        [Header("Debug Visualization")]
        [Tooltip("Horizontal prediction step for labels")]
        public int labelStep = 5;
        [Tooltip("Show cost breakdown in Inspector")]
        public bool showCostBreakdown = true;
        [Tooltip("Which cost to display: current ship state or full trajectory")]
        public CostBreakdownMode costBreakdownMode = CostBreakdownMode.CurrentState;
        [Tooltip("Log solver performance once per second")]
        public bool logSolverPerformance = false;

        private float nextLogTime;

        private DetectedObstacle[] dbgObstacles;
        private int dbgObstacleCount;

        private DetectedGap[] dbgGaps;
        private int dbgGapCount;
        private int dbgChosenGap = -1;

        // Debug info
        public CostBreakdown lastCostBreakdown;
        public float lastSolveTimeMs;

        // Read-only views into the composed Mpc solver's runtime state. The visualization
        // below was written against these as fields; they now forward to the Mpc object so
        // the gizmo code is unchanged. All return null/default before Initialize.
        private SolverBuffers solver => mpc?.Solver;
        private Config config => mpc != null ? mpc.Config : default;
        private Control[] bestSequence => mpc?.BestSequence;
        private State[] predictedStates => mpc?.PredictedStates;
        private State lastInitialState => mpc != null ? mpc.LastInitialState : default;
        // (ship dynamics now live on the runtime partial's `dynamics` field, set in Initialize)
        private Control lastControl => mpc != null ? mpc.LastControl : default;
        internal float lastBestCost => mpc != null ? mpc.LastBestCost : 0f;

        [Header("Scene Gizmo Sub-Toggles")]
        public bool showObstacleCosts = true;
        public bool showGaps = true;
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
        [System.NonSerialized] public int selectedCandidateIndex = -1;
        // Subsample buffer reused per frame, sorted by cost ascending.
        private int[] visibleCandidateIndices;
        private int visibleCount;
        [Tooltip("World-space offset from ship for the control input panel")]
        public Vector3 controlPanelOffset = new(0f, 2.5f, 0f);

        [Header("Comparison Rollouts")]
        [Tooltip("State profiles to run comparison rollouts for. Each gets its own trajectory drawn in a unique color.")]
        public StateProfile[] comparisonProfiles;

        internal static readonly Color[] ComparisonColors =
        {
            new(1f, 0.4f, 0.1f, 0.7f),  // orange
            new(0.4f, 1f, 0.4f, 0.7f),   // green
            new(1f, 0.4f, 1f, 0.7f),     // magenta
            new(1f, 1f, 0.3f, 0.7f),     // yellow
            new(0.4f, 0.8f, 1f, 0.7f),   // light blue
            new(1f, 0.6f, 0.6f, 0.7f),   // salmon
        };

        internal struct ComparisonResult
        {
            public StateProfile profile;
            public Control[] sequence;
            public State[] trajectory;
            public float cost;
        }
        internal ComparisonResult[] comparisonResults;

        partial void RunComparisonRollouts(State mpcState, ObstacleScan scan)
        {
            if (comparisonProfiles == null || comparisonProfiles.Length == 0)
            {
                comparisonResults = null;
                return;
            }

            if (comparisonResults == null || comparisonResults.Length != comparisonProfiles.Length)
                comparisonResults = new ComparisonResult[comparisonProfiles.Length];

            var costInput = solver.BuildCostInput(GoalPos(), GoalVel(),
                enemyPos, enemyVel, enemyYaw, enemyYawRate, projectileSpeed, mpcState.vel);

            for (var p = 0; p < comparisonProfiles.Length; p++)
            {
                var profile = comparisonProfiles[p];
                if (!profile) continue;

                // Build config with this profile's weights
                var goal = profile.goal;
                var gm = goal?.GoalMode ?? GoalMode.Waypoint;
                var desiredRange = 0f;
                var rangeTolerance = 0f;
                if (goal is TrackEnemyGoal track)
                {
                    desiredRange = track.desiredRange;
                    rangeTolerance = track.rangeTolerance;
                }
                var facingRad = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN;
                var compConfig = mpcSettings.ToConfig(facingRad, gm, desiredRange, rangeTolerance);
                compConfig.ApplyDynamics(dynamics);
                profile.weightOverrides.Apply(ref compConfig);

                var horizon = compConfig.horizon;
                if (comparisonResults[p].sequence == null || comparisonResults[p].sequence.Length != horizon)
                {
                    comparisonResults[p].sequence = new Control[horizon];
                    comparisonResults[p].trajectory = new State[horizon];
                }

                // Rescore the same candidates with different weights
                var seq = comparisonResults[p].sequence;
                comparisonResults[p].cost = solver.Rescore(mpcState, seq,
                    compConfig, dynamics, costInput, lastControl,
                    mpcSettings.samples, mpcSettings.eliteFraction);

                // Roll out trajectory from the rescored elite average
                var current = mpcState;
                var traj = comparisonResults[p].trajectory;
                for (var i = 0; i < horizon; i++)
                {
                    current = Model.Step(current, seq[i], compConfig, dynamics);
                    traj[i] = current;
                }

                comparisonResults[p].profile = profile;
            }
        }

        private AICommander cachedCommander;
        private AIDebugSettings CachedSettings
        {
            get
            {
                if (!cachedCommander)
                    cachedCommander = GetComponent<AICommander>();
                return cachedCommander ? cachedCommander.DebugSettings : null;
            }
        }

        partial void StoreDebugObstacles(ObstacleScan scan)
        {
            if (dbgObstacles == null || dbgObstacles.Length < scan.count)
                dbgObstacles = new DetectedObstacle[Mathf.Max(scan.count, 32)];

            dbgObstacleCount = scan.count;
            for (var i = 0; i < scan.count; i++)
                dbgObstacles[i] = scan.buffer[i];
        }

        partial void StoreDebugGaps(DetectedGap[] gaps, int count, int chosen)
        {
            if (dbgGaps == null || dbgGaps.Length < count)
                dbgGaps = new DetectedGap[Mathf.Max(count, 16)];

            dbgGapCount = count;
            dbgChosenGap = chosen;
            for (var i = 0; i < count; i++)
                dbgGaps[i] = gaps[i];
        }

        private void DrawGaps()
        {
            if (!showGaps || dbgGaps == null || dbgGapCount == 0) return;

            var origin = transform.position;
            var injectedWon = solver != null && solver.LastInjectedCount > 0 &&
                              solver.LastBestIndex >= 1 && solver.LastBestIndex <= solver.LastInjectedCount;

            for (var i = 0; i < dbgGapCount; i++)
            {
                var gap = dbgGaps[i];
                var isChosen = i == dbgChosenGap;
                var dir = new Vector2(-Mathf.Sin(gap.axisAngle), Mathf.Cos(gap.axisAngle));
                var worldDir = GamePlane.PlaneDirToWorld(dir);
                var length = Mathf.Min(gap.depth, 25f);

                // Chosen = green, bank-only = cyan, others = dim white; edge rays show width.
                Gizmos.color = isChosen ? new Color(0.2f, 1f, 0.2f, 0.9f)
                    : gap.bankOnly ? new Color(0.3f, 0.9f, 1f, 0.5f)
                    : new Color(1f, 1f, 1f, 0.35f);
                Gizmos.DrawRay(origin, worldDir * length);

                var half = gap.angularWidth * 0.5f;
                var edgeA = new Vector2(-Mathf.Sin(gap.axisAngle - half), Mathf.Cos(gap.axisAngle - half));
                var edgeB = new Vector2(-Mathf.Sin(gap.axisAngle + half), Mathf.Cos(gap.axisAngle + half));
                var edgeColor = Gizmos.color;
                edgeColor.a *= 0.4f;
                Gizmos.color = edgeColor;
                Gizmos.DrawRay(origin, GamePlane.PlaneDirToWorld(edgeA) * length);
                Gizmos.DrawRay(origin, GamePlane.PlaneDirToWorld(edgeB) * length);

                if (isChosen)
                {
                    Handles.Label(origin + worldDir * (length + 0.5f),
                        $"gap {gap.score:F2}{(gap.bankOnly ? " BANK" : "")}{(injectedWon ? " INJ-WON" : "")}",
                        new GUIStyle { normal = { textColor = Color.green }, fontSize = 10 });
                }
            }
        }

        private CostBreakdown EvaluateBreakdown(State mpcState)
        {
            var input = solver.BuildCostInput(GoalPos(), GoalVel(), enemyPos, enemyVel, enemyYaw, enemyYawRate, projectileSpeed, mpcState.vel);
            if (costBreakdownMode == CostBreakdownMode.CurrentState)
                return Cost.EvaluateBreakdown(mpcState, bestSequence[0], lastControl, input, config, false);
            return Cost.EvaluateTrajectoryBreakdown(mpcState, bestSequence, input, config, dynamics, lastControl);
        }

        partial void LogSolverPerformanceIfNeeded()
        {
            if (!logSolverPerformance || !(Time.time > nextLogTime)) return;
            Debug.Log($"[MPC] {gameObject.name} | Solve: {lastSolveTimeMs:F2}ms | Cost: {lastBestCost:F1}");
            nextLogTime = Time.time + 1f;
        }

        private void OnDrawGizmos() => DrawGizmosImpl(false);
        private void OnDrawGizmosSelected() => DrawGizmosImpl(true);

        void DrawGizmosImpl(bool isSelected)
        {
            if (mpc == null) return;
            var settings = CachedSettings;
            if (settings == null || !settings.ShouldDraw(isSelected)) return;
            if (!settings.IsActive(AIDebugChannel.Steering)) return;

            DrawShipRadius();
            DrawCandidateTrajectories();
            DrawPredictedTrajectory();
            DrawComparisonTrajectories();
            DrawEnemyRollout();
            DrawGoal();
            DrawObstacleDebugInfo();
            DrawGaps();
            DrawControlInputs();
        }

        private void DrawCandidateTrajectories()
        {
            if (!showCandidateTrajectories || solver == null) return;
            var samples = solver.LastSampleCount;
            var horizon = solver.LastHorizon;
            if (samples == 0 || horizon == 0) return;

            var k = Mathf.Min(candidateSampleCount, samples);
            if (visibleCandidateIndices == null || visibleCandidateIndices.Length < k)
                visibleCandidateIndices = new int[k];
            visibleCount = k;

            // Stable per-frame reservoir subsample
            var rng = new Unity.Mathematics.Random((uint)(Time.frameCount * 31u + 1u));
            for (var i = 0; i < k; i++) visibleCandidateIndices[i] = i;
            for (var i = k; i < samples; i++)
            {
                var j = rng.NextInt(0, i + 1);
                if (j < k) visibleCandidateIndices[j] = i;
            }

            // Insertion sort ascending by cost so rank-based alpha is meaningful (best=opaque)
            var costs = solver.Costs;
            for (var a = 1; a < k; a++)
            {
                var idx = visibleCandidateIndices[a];
                var cost = costs[idx];
                var b = a - 1;
                while (b >= 0 && costs[visibleCandidateIndices[b]] > cost)
                {
                    visibleCandidateIndices[b + 1] = visibleCandidateIndices[b];
                    b--;
                }
                visibleCandidateIndices[b + 1] = idx;
            }

            var candidates = solver.Candidates;
            var initial = lastInitialState;
            var denom = Mathf.Max(k - 1, 1);

            for (var i = 0; i < k; i++)
            {
                var idx = visibleCandidateIndices[i];
                var rankFrac = i / (float)denom;
                var alpha = Mathf.Max(0.03f, 0.85f * Mathf.Exp(-rankFrac * candidateAlphaFalloff));
                var isSelected = idx == selectedCandidateIndex;
                Gizmos.color = isSelected
                    ? new Color(1f, 0.9f, 0.2f, 0.95f)
                    : new Color(0.4f, 0.7f, 1f, alpha);

                var prev = initial;
                var prevWorld = GamePlane.PlanePointToWorld(new Vector2(prev.pos.x, prev.pos.y));
                for (var step = 0; step < horizon; step++)
                {
                    var u = candidates[idx * horizon + step];
                    var next = Model.Step(prev, u, config, dynamics);
                    var nextWorld = GamePlane.PlanePointToWorld(new Vector2(next.pos.x, next.pos.y));
                    Gizmos.DrawLine(prevWorld, nextWorld);
                    prev = next;
                    prevWorld = nextWorld;
                }
            }
        }

        // Drops a clickable Handles.Button at each visible candidate's terminal point.
        // Called from NavigatorEditor.OnSceneGUI so it has access to SceneView input.
        internal bool DrawCandidateSelectionHandles()
        {
            if (!showCandidateTrajectories || solver == null) return false;
            if (visibleCandidateIndices == null || visibleCount == 0) return false;

            var horizon = solver.LastHorizon;
            if (horizon == 0) return false;
            var candidates = solver.Candidates;
            var initial = lastInitialState;
            var changed = false;

            for (var i = 0; i < visibleCount; i++)
            {
                var idx = visibleCandidateIndices[i];
                var current = initial;
                for (var step = 0; step < horizon; step++)
                    current = Model.Step(current, candidates[idx * horizon + step], config, dynamics);

                var world = GamePlane.PlanePointToWorld(new Vector2(current.pos.x, current.pos.y));
                var size = HandleUtility.GetHandleSize(world) * 0.05f;
                var isSelected = idx == selectedCandidateIndex;
                Handles.color = isSelected ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.55f);
                if (Handles.Button(world, Quaternion.identity, size, size * 1.6f, Handles.DotHandleCap))
                {
                    selectedCandidateIndex = isSelected ? -1 : idx;
                    changed = true;
                }
            }
            return changed;
        }

        // Rolls the selected candidate's control sequence and returns its cost breakdown.
        // Returns null if no selection or buffers are stale.
        internal CostBreakdown? GetSelectedCandidateBreakdown()
        {
            if (selectedCandidateIndex < 0 || solver == null) return null;
            var samples = solver.LastSampleCount;
            var horizon = solver.LastHorizon;
            if (selectedCandidateIndex >= samples || horizon == 0) return null;

            var candidates = solver.Candidates;
            var seq = new Control[horizon];
            for (var i = 0; i < horizon; i++)
                seq[i] = candidates[selectedCandidateIndex * horizon + i];

            var input = solver.BuildCostInput(GoalPos(), GoalVel(),
                enemyPos, enemyVel, enemyYaw, enemyYawRate, projectileSpeed, lastInitialState.vel);
            return Cost.EvaluateTrajectoryBreakdown(lastInitialState, seq, input, config, dynamics, lastControl);
        }

        private void DrawShipRadius()
        {
            if (dynamics.shipRadius <= 0f) return;
            Handles.color = new Color(0f, 1f, 1f, 0.25f);
            Handles.DrawWireDisc(transform.position, GamePlane.Normal, dynamics.shipRadius);
        }

        private void DrawPredictedTrajectory()
        {
            if (predictedStates == null || predictedStates.Length == 0) return;

            var prevPos = GamePlane.PlanePointToWorld(new Vector2(predictedStates[0].pos.x, predictedStates[0].pos.y));
            var prevU = bestSequence[0];
            var input = solver.BuildCostInput(GoalPos(), GoalVel(), enemyPos, enemyVel, enemyYaw, enemyYawRate, projectileSpeed, predictedStates[0].vel);

            for (var i = 1; i < predictedStates.Length; i++)
            {
                var state = predictedStates[i];
                var u = bestSequence[i];
                var pos = GamePlane.PlanePointToWorld(new Vector2(state.pos.x, state.pos.y));

                var isTerminal = i == predictedStates.Length - 1;
                var stepBreakdown = Cost.EvaluateBreakdown(state, u, prevU, input, config, isTerminal, i);

                var obstacleSeverity = stepBreakdown.collision > 0f ? 5f
                    : config.wObstacle > 0f ? stepBreakdown.obstacle / config.wObstacle : 0f;
                Gizmos.color = showTrajectoryCosts ? GetCostColor(obstacleSeverity) : Color.cyan;

                Gizmos.DrawLine(prevPos, pos);
                Gizmos.DrawSphere(pos, 0.15f);

                // Planned yaw heading tick (MPC convention: fwd = (-sin, cos))
                var yawDir = new Vector2(-Mathf.Sin(state.yaw), Mathf.Cos(state.yaw));
                Gizmos.color = new Color(1f, 1f, 0.4f, 0.7f);
                Gizmos.DrawRay(pos, GamePlane.PlaneDirToWorld(yawDir) * 0.4f);

                if (i % labelStep == 0)
                {
                    Handles.Label(pos + Vector3.up * 0.2f,
                        $"Cost: {stepBreakdown.total:F1}\n(P:{stepBreakdown.pos:F1} O:{stepBreakdown.obstacle + stepBreakdown.collision:F1})",
                        new GUIStyle { normal = { textColor = Color.white }, fontSize = 10 });
                }

                prevPos = pos;
                prevU = u;
            }
        }

        private void DrawComparisonTrajectories()
        {
            if (comparisonResults == null) return;

            for (var p = 0; p < comparisonResults.Length; p++)
            {
                var result = comparisonResults[p];
                if (result.profile == null || result.trajectory == null) continue;

                var color = ComparisonColors[p % ComparisonColors.Length];
                var prevPos = GamePlane.PlanePointToWorld(new Vector2(result.trajectory[0].pos.x, result.trajectory[0].pos.y));

                for (var i = 1; i < result.trajectory.Length; i++)
                {
                    var state = result.trajectory[i];
                    var pos = GamePlane.PlanePointToWorld(new Vector2(state.pos.x, state.pos.y));

                    Gizmos.color = color;
                    Gizmos.DrawLine(prevPos, pos);
                    Gizmos.DrawSphere(pos, 0.1f);

                    prevPos = pos;
                }

                // Label at the end of the trajectory
                var endPos = GamePlane.PlanePointToWorld(new Vector2(
                    result.trajectory[result.trajectory.Length - 1].pos.x,
                    result.trajectory[result.trajectory.Length - 1].pos.y));
                Handles.Label(endPos + Vector3.up * 0.3f,
                    $"{result.profile.name}\nCost: {result.cost:F1}",
                    new GUIStyle
                    {
                        normal = { textColor = color },
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                    });
            }
        }

        private void DrawEnemyRollout()
        {
            if (solver == null || solver.LastEnemyStateCount == 0) return;

            var enemyStates = solver.EnemyStates;
            var count = solver.LastEnemyStateCount;

            var prevPos = GamePlane.PlanePointToWorld(new Vector2(enemyStates[0].pos.x, enemyStates[0].pos.y));

            for (var i = 1; i < count; i++)
            {
                var state = enemyStates[i];
                var pos = GamePlane.PlanePointToWorld(new Vector2(state.pos.x, state.pos.y));

                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
                Gizmos.DrawLine(prevPos, pos);
                Gizmos.DrawSphere(pos, 0.15f);

                if (i % labelStep == 0)
                {
                    Handles.Label(pos + Vector3.up * 0.2f,
                        $"Enemy t+{i}",
                        new GUIStyle { normal = { textColor = new Color(1f, 0.4f, 0.4f) }, fontSize = 10 });
                }

                prevPos = pos;
            }
        }

        private void DrawGoal()
        {
            if (currentWaypoint.isValid)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(GamePlane.PlanePointToWorld(currentWaypoint.position), arriveRadius);
            }

        }

        private void DrawObstacleDebugInfo()
        {
            if (!showObstacleCosts || dbgObstacles == null || dbgObstacleCount == 0) return;

            // Collision boundaries the MPC actually tests: hull at current bank + safety margin.
            var profileScale = config.maxBankAngleRad > 0f
                ? Mathf.Cos(Mathf.Abs(lastControl.strafe) * config.maxBankAngleRad)
                : 1f;
            var hullUnbanked = config.shipRadius + config.collisionSafetyMargin;
            var hullCurrent = config.shipRadius * profileScale + config.collisionSafetyMargin;

            // Stopping distance at the current speed — the admissibility term's reach.
            var vel = predictedStates != null && predictedStates.Length > 0
                ? predictedStates[0].vel
                : default;
            var speed = math.length(vel);
            var decel = config.brakingDecel + config.brakingDrag * speed;
            var stoppingDist = decel > 0f ? speed * speed / (2f * decel) : 0f;

            for (var i = 0; i < dbgObstacleCount; i++)
            {
                var obs = dbgObstacles[i];
                var obsWorldPos = GamePlane.PlanePointToWorld(obs.position);

                // Inner ring: hard collision boundary for the unbanked hull
                Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
                Gizmos.DrawWireSphere(obsWorldPos, obs.radius + hullUnbanked);

                // Bank ring: collision boundary at the current commanded bank (narrower)
                if (hullCurrent < hullUnbanked - 1e-4f)
                {
                    Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
                    Gizmos.DrawWireSphere(obsWorldPos, obs.radius + hullCurrent);
                }

                // Outer ring: where the admissibility cost starts biting at the current
                // speed (clearance == stopping distance while closing head-on).
                if (stoppingDist > 0.05f)
                {
                    Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
                    Gizmos.DrawWireSphere(obsWorldPos, obs.radius + hullCurrent + stoppingDist);
                }
            }
        }

        private void DrawControlInputs()
        {
            if (!showControlInputs || bestSequence == null || bestSequence.Length == 0) return;

            var raw = bestSequence[0];
            var origin = transform.position + controlPanelOffset;

            // Camera-facing basis vectors for the panel
            var cam = Camera.current;
            if (cam == null) return;
            var right = cam.transform.right;
            var up = cam.transform.up;

            var barWidth = 1.2f;
            var barHeight = 0.12f;
            var rowSpacing = 0.22f;
            var halfBar = barWidth * 0.5f;

            var labelStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleRight,
            };
            var valueStyle = new GUIStyle
            {
                fontSize = 10,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                alignment = TextAnchor.MiddleLeft,
            };

            DrawControlBar(origin, right, up, 0, "THR", raw.thrust,
                new Color(0.2f, 0.9f, 0.3f), barWidth, barHeight, halfBar, labelStyle, valueStyle);
            DrawControlBar(origin, right, up, 1, "STR", raw.strafe,
                new Color(0.3f, 0.6f, 1f), barWidth, barHeight, halfBar, labelStyle, valueStyle);
            DrawControlBar(origin, right, up, 2, "YAW", raw.yawTorque,
                new Color(1f, 0.4f, 0.8f), barWidth, barHeight, halfBar, labelStyle, valueStyle);
        }

        private void DrawControlBar(Vector3 origin, Vector3 right, Vector3 up,
            int row, string label, float value, Color color,
            float barWidth, float barHeight, float halfBar, GUIStyle labelStyle, GUIStyle valueStyle)
        {
            var rowOffset = -up * (row * 0.22f);
            var center = origin + rowOffset;
            var barLeft = center - right * halfBar;

            // Background bar (dark, full width)
            var bgColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            DrawQuad(barLeft, right, up, barWidth, barHeight, bgColor);

            // Center tick mark
            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            var midBottom = center - up * barHeight * 0.5f;
            var midTop = center + up * barHeight * 0.5f;
            Gizmos.DrawLine(midBottom, midTop);

            // Applied control bar (center of the bar = zero point)
            var barColor = color;
            barColor.a = 0.85f;
            var valueBarWidth = Mathf.Abs(value) * halfBar;
            var valueBarOrigin = value >= 0 ? center : center - right * valueBarWidth;
            DrawQuad(valueBarOrigin, right, up, valueBarWidth, barHeight * 0.9f, barColor);

            // Label on the left
            var labelPos = barLeft - right * 0.05f;
            Handles.Label(labelPos, label, labelStyle);

            // Value on the right
            var valPos = barLeft + right * (barWidth + 0.08f);
            Handles.Label(valPos, $"{value:+0.00;-0.00}", valueStyle);
        }

        private static void DrawQuad(Vector3 bottomLeft, Vector3 right, Vector3 up,
            float width, float height, Color color)
        {
            if (width < 0.001f) return;
            Gizmos.color = color;
            // Fill with horizontal scan lines
            var steps = Mathf.Max(2, Mathf.CeilToInt(height / 0.02f));
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var y = up * (t * height - height * 0.5f);
                Gizmos.DrawLine(bottomLeft + y, bottomLeft + right * width + y);
            }
        }

        private Color GetCostColor(float obstacleCost)
        {
            if (obstacleCost < 0.1f)
                return Color.cyan;
            else if (obstacleCost < 1f)
                return Color.Lerp(Color.cyan, Color.yellow, obstacleCost);
            else if (obstacleCost < 5f)
                return Color.Lerp(Color.yellow, Color.red, (obstacleCost - 1f) / 4f);
            else
                return Color.red;
        }
    }

    [CustomEditor(typeof(Navigator))]
    public class NavigatorEditor : Editor
    {
        private bool showUnweightedCosts;

        private void OnSceneGUI()
        {
            var nav = (Navigator)target;
            if (!Application.isPlaying) return;
            if (nav.DrawCandidateSelectionHandles())
                Repaint();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var nav = (Navigator)target;
            if (!nav.showCostBreakdown || !Application.isPlaying) return;

            EditorGUILayout.Space();
            var modeLabel = nav.costBreakdownMode == CostBreakdownMode.CurrentState
                ? "MPC Cost Breakdown (Current State)"
                : "MPC Cost Breakdown (Full Trajectory)";
            EditorGUILayout.LabelField(modeLabel, EditorStyles.boldLabel);

            var solveTimeColor = nav.lastSolveTimeMs > 2.0f ? "red" : "lime";
            EditorGUILayout.LabelField($"Solve Time: <color={solveTimeColor}>{nav.lastSolveTimeMs:F2} ms</color>",
                new GUIStyle(EditorStyles.label) { richText = true });

            var breakdown = nav.lastCostBreakdown;
            var total = breakdown.total;
            EditorGUILayout.LabelField($"Total Cost: {total:F2}");

            var horizon = nav.mpcSettings.Horizon;
            var normalizedCost = horizon > 0 ? nav.lastBestCost / horizon : nav.lastBestCost;
            EditorGUILayout.LabelField($"Normalized Cost (per-step): {normalizedCost:F3}");

            showUnweightedCosts = EditorGUILayout.ToggleLeft(
                "Show Unweighted (raw cost / weight)", showUnweightedCosts);

            RenderBreakdownBars(nav.mpcSettings, breakdown);

            if (nav.showCandidateTrajectories && nav.selectedCandidateIndex >= 0)
            {
                EditorGUILayout.Space();
                var selBreakdown = nav.GetSelectedCandidateBreakdown();
                if (selBreakdown.HasValue)
                {
                    var sb = selBreakdown.Value;
                    EditorGUILayout.LabelField(
                        $"Selected Candidate #{nav.selectedCandidateIndex} (trajectory total)",
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Cost: {sb.total:F2}");
                    if (GUILayout.Button("Clear Selection", GUILayout.Width(120)))
                        nav.selectedCandidateIndex = -1;
                    RenderBreakdownBars(nav.mpcSettings, sb);
                }
            }

            if (nav.comparisonResults != null && nav.comparisonResults.Length > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Comparison Rollouts", EditorStyles.boldLabel);
                for (var i = 0; i < nav.comparisonResults.Length; i++)
                {
                    var result = nav.comparisonResults[i];
                    if (result.profile == null) continue;
                    var color = Navigator.ComparisonColors[i % Navigator.ComparisonColors.Length];
                    var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
                    EditorGUILayout.LabelField($"  {result.profile.name}: {result.cost:F1}", style);
                }
            }

            Repaint();
        }

        private void RenderBreakdownBars(MpcSettings s, CostBreakdown breakdown)
        {
            var total = breakdown.total;
            DrawCostBar("Position", breakdown.pos, s.wPos, total, Color.green);
            DrawCostBar("Heading", breakdown.heading, s.wYaw, total, Color.yellow);
            DrawCostBar("Facing", breakdown.facing, s.wFacing, total, Color.cyan);
            DrawCostBar("Velocity", breakdown.vel, s.wVel, total, Color.blue);
            DrawCostBar("Closing", breakdown.closing, s.wClosing, total, new Color(0.4f, 0.9f, 0.7f));
            DrawCostBar("Yaw Rate", breakdown.yawRate, s.wYawRate, total, Color.magenta);
            DrawCostBar("Obstacle", breakdown.obstacle, s.wObstacle, total, Color.red);
            DrawCostBar("Collision", breakdown.collision, s.collisionPenalty, total, new Color(1f, 0f, 0.5f));
            DrawCostBar("LOS", breakdown.los, s.wLos, total, new Color(1f, 0.5f, 0f));
            DrawCostBar("Exposure", breakdown.exposure, s.wExposure, total, new Color(1f, 0.3f, 0.3f));
            DrawCostBar("Tangential", breakdown.tangential, s.wTangential, total, new Color(0.3f, 0.8f, 1f));
            DrawCostBar("Miss Distance", breakdown.missDistance, s.wMissDistance, total, new Color(1f, 0.8f, 0.2f));
            DrawCostBar("Momentum", breakdown.momentum, s.wMomentum, total, new Color(0.6f, 1f, 0.6f));
            DrawCostBar("Effort", breakdown.effort, s.wEffort, total, Color.gray);
            DrawCostBar("Boost Effort", breakdown.boostEffort, s.wBoostEffort, total, new Color(1f, 0.6f, 0f));
            DrawCostBar("Smoothness", breakdown.smoothness, 0f, total, Color.white);
        }

        private void DrawCostBar(string label, float value, float weight, float total, Color color)
        {
            // Bar magnitude / percentage always reflect the WEIGHTED contribution to total
            // (so comparisons between rows remain meaningful regardless of toggle state).
            var pct = total > 0 ? value / total : 0;
            var rect = EditorGUILayout.GetControlRect(false, 18);

            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));

            var barWidth = Mathf.Max(rect.width * Mathf.Abs(pct), Mathf.Abs(value) > 1e-6f ? 3f : 0f);
            var barRect = new Rect(rect.x, rect.y, barWidth, rect.height);
            EditorGUI.DrawRect(barRect, color * 0.7f);

            // The labeled value switches: weighted (default) vs unweighted (raw cost / weight)
            var displayValue = (showUnweightedCosts && weight > 1e-6f) ? value / weight : value;
            var suffix = (showUnweightedCosts && weight > 1e-6f) ? $" ×{weight:G3}" : "";
            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            var absDisp = Mathf.Abs(displayValue);
            var valueStr = absDisp >= 10f ? $"{displayValue:F1}"
                : absDisp >= 1f ? $"{displayValue:F2}"
                : $"{displayValue:F3}";
            EditorGUI.LabelField(rect, $" {label}: {valueStr}{suffix} ({pct * 100:F1}%)", style);
        }
    }
}
#endif
