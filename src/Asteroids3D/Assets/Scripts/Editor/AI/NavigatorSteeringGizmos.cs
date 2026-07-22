using AI.Debug;
using Game;
using Movement.MPC;
using Movement.MPC.Field;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Scene gizmos for the MPC navigator behind the Steering debug channel: trajectories (predicted, candidate, enemy), obstacle rings, flee field, goal, and the control-input panel.</summary>
    internal static class NavigatorSteeringGizmos
    {

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Navigator))]
        private static void Draw(Navigator nav, GizmoType gizmoType)
        {
            if (!AIDebugContext.ShouldDraw(AIDebugChannel.Steering, gizmoType)) return;
            if (nav.mpc == null) return;

            DrawShipRadius(nav);
            DrawFleeField(nav);
            DrawCandidateTrajectories(nav);
            DrawPredictedTrajectory(nav);
            DrawEnemyRollout(nav);
            DrawGoal(nav);
            DrawObstacleDebugInfo(nav);
            DrawControlInputs(nav);
        }

        private static void DrawFleeField(Navigator nav)
        {
            if (!nav.showFleeField || nav.fleeFieldBaker == null) return;
            NavFieldServiceGizmos.DrawField(nav.fleeFieldBaker.Front);
        }

        private static void DrawCandidateTrajectories(Navigator nav)
        {
            if (!nav.showCandidateTrajectories || nav.solver == null) return;
            var samples = nav.solver.LastSampleCount;
            var horizon = nav.solver.LastHorizon;
            if (samples == 0 || horizon == 0) return;

            var k = Mathf.Min(nav.candidateSampleCount, samples);
            if (nav.visibleCandidateIndices == null || nav.visibleCandidateIndices.Length < k)
                nav.visibleCandidateIndices = new int[k];
            nav.visibleCount = k;

            // Stable per-frame reservoir subsample
            var rng = new Unity.Mathematics.Random((uint)(Time.frameCount * 31u + 1u));
            for (var i = 0; i < k; i++) nav.visibleCandidateIndices[i] = i;
            for (var i = k; i < samples; i++)
            {
                var j = rng.NextInt(0, i + 1);
                if (j < k) nav.visibleCandidateIndices[j] = i;
            }

            // Insertion sort ascending by cost so rank-based alpha is meaningful (best=opaque)
            var costs = nav.solver.Costs;
            for (var a = 1; a < k; a++)
            {
                var idx = nav.visibleCandidateIndices[a];
                var cost = costs[idx];
                var b = a - 1;
                while (b >= 0 && costs[nav.visibleCandidateIndices[b]] > cost)
                {
                    nav.visibleCandidateIndices[b + 1] = nav.visibleCandidateIndices[b];
                    b--;
                }
                nav.visibleCandidateIndices[b + 1] = idx;
            }

            var candidates = nav.solver.Candidates;
            var initial = nav.lastInitialState;
            var denom = Mathf.Max(k - 1, 1);

            for (var i = 0; i < k; i++)
            {
                var idx = nav.visibleCandidateIndices[i];
                var rankFrac = i / (float)denom;
                var alpha = Mathf.Max(0.03f, 0.85f * Mathf.Exp(-rankFrac * nav.candidateAlphaFalloff));
                var isSelected = idx == nav.selectedCandidateIndex;
                Gizmos.color = isSelected
                    ? new Color(1f, 0.9f, 0.2f, 0.95f)
                    : new Color(0.4f, 0.7f, 1f, alpha);

                var prev = initial;
                var prevWorld = GamePlane.PlanePointToWorld(new Vector2(prev.pos.x, prev.pos.y));
                for (var step = 0; step < horizon; step++)
                {
                    var u = candidates[idx * horizon + step];
                    var next = Model.Step(prev, u, nav.config, nav.dynamics);
                    var nextWorld = GamePlane.PlanePointToWorld(new Vector2(next.pos.x, next.pos.y));
                    Gizmos.DrawLine(prevWorld, nextWorld);
                    prev = next;
                    prevWorld = nextWorld;
                }
            }
        }

        private static void DrawShipRadius(Navigator nav)
        {
            if (nav.dynamics.shipRadius <= 0f) return;
            Handles.color = new Color(0f, 1f, 1f, 0.25f);
            Handles.DrawWireDisc(nav.transform.position, GamePlane.Normal, nav.dynamics.shipRadius);
        }

        private static void DrawPredictedTrajectory(Navigator nav)
        {
            var predictedStates = nav.predictedStates;
            if (predictedStates == null || predictedStates.Length == 0) return;

            var bestSequence = nav.bestSequence;
            var config = nav.config;
            var prevPos = GamePlane.PlanePointToWorld(new Vector2(predictedStates[0].pos.x, predictedStates[0].pos.y));
            var prevU = bestSequence[0];
            var input = nav.solver.BuildCostInput(nav.GoalPos(), nav.GoalVel(),
                nav.enemyPos, nav.enemyVel, nav.enemyYaw, nav.enemyYawRate, nav.projectileSpeed, predictedStates[0].vel);

            for (var i = 1; i < predictedStates.Length; i++)
            {
                var state = predictedStates[i];
                var u = bestSequence[i];
                var pos = GamePlane.PlanePointToWorld(new Vector2(state.pos.x, state.pos.y));

                var isTerminal = i == predictedStates.Length - 1;
                var stepBreakdown = Cost.EvaluateBreakdown(state, u, prevU, input, config, isTerminal, i);

                var obstacleSeverity = stepBreakdown.collision > 0f ? 5f
                    : config.wObstacle > 0f ? stepBreakdown.obstacle / config.wObstacle : 0f;
                Gizmos.color = nav.showTrajectoryCosts ? GetCostColor(obstacleSeverity) : Color.cyan;

                Gizmos.DrawLine(prevPos, pos);
                Gizmos.DrawSphere(pos, 0.15f);

                // Planned yaw heading tick (MPC convention: fwd = (-sin, cos))
                var yawDir = new Vector2(-Mathf.Sin(state.yaw), Mathf.Cos(state.yaw));
                Gizmos.color = new Color(1f, 1f, 0.4f, 0.7f);
                Gizmos.DrawRay(pos, GamePlane.PlaneDirToWorld(yawDir) * 0.4f);

                if (i % nav.labelStep == 0)
                {
                    Handles.Label(pos + Vector3.up * 0.2f,
                        $"Cost: {stepBreakdown.total:F1}\n(P:{stepBreakdown.pos:F1} O:{stepBreakdown.obstacle + stepBreakdown.collision:F1})",
                        new GUIStyle { normal = { textColor = Color.white }, fontSize = 10 });
                }

                prevPos = pos;
                prevU = u;
            }
        }


        private static void DrawEnemyRollout(Navigator nav)
        {
            if (nav.solver == null || nav.solver.LastEnemyStateCount == 0) return;

            var enemyStates = nav.solver.EnemyStates;
            var count = nav.solver.LastEnemyStateCount;

            var prevPos = GamePlane.PlanePointToWorld(new Vector2(enemyStates[0].pos.x, enemyStates[0].pos.y));

            for (var i = 1; i < count; i++)
            {
                var state = enemyStates[i];
                var pos = GamePlane.PlanePointToWorld(new Vector2(state.pos.x, state.pos.y));

                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
                Gizmos.DrawLine(prevPos, pos);
                Gizmos.DrawSphere(pos, 0.15f);

                if (i % nav.labelStep == 0)
                {
                    Handles.Label(pos + Vector3.up * 0.2f,
                        $"Enemy t+{i}",
                        new GUIStyle { normal = { textColor = new Color(1f, 0.4f, 0.4f) }, fontSize = 10 });
                }

                prevPos = pos;
            }
        }

        private static void DrawGoal(Navigator nav)
        {
            if (!nav.CurrentWaypoint.isValid) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GamePlane.PlanePointToWorld(nav.CurrentWaypoint.position), nav.arriveRadius);
        }

        private static void DrawObstacleDebugInfo(Navigator nav)
        {
            if (!nav.showObstacleCosts || nav.dbgObstacles == null || nav.dbgObstacleCount == 0) return;

            // Collision boundaries the MPC actually tests: hull at current bank + safety margin.
            var config = nav.config;
            var profileScale = config.maxBankAngleRad > 0f
                ? Mathf.Cos(Mathf.Abs(nav.lastControl.strafe) * config.maxBankAngleRad)
                : 1f;
            var hullUnbanked = config.shipRadius + config.collisionSafetyMargin;
            var hullCurrent = config.shipRadius * profileScale + config.collisionSafetyMargin;

            var vel = nav.predictedStates != null && nav.predictedStates.Length > 0
                ? nav.predictedStates[0].vel
                : default;
            var speed = math.length(vel);
            var halfLatAccel = 0.5f * Mathf.Max(config.maxLatAccel, 1e-4f);

            for (var i = 0; i < nav.dbgObstacleCount; i++)
            {
                var obs = nav.dbgObstacles[i];
                var obsWorldPos = GamePlane.PlanePointToWorld(obs.position);

                Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
                Gizmos.DrawWireSphere(obsWorldPos, obs.radius + hullUnbanked);

                if (hullCurrent < hullUnbanked - 1e-4f)
                {
                    Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
                    Gizmos.DrawWireSphere(obsWorldPos, obs.radius + hullCurrent);
                }

                // Turn-away bite range: head-on distance inside which lateral thrust can't sidestep a full corridor before impact (½·a_lat·t² == corridor at t = along/speed).
                var corridor = obs.radius + hullCurrent;
                var biteRange = speed > 0.05f ? speed * math.sqrt(corridor / halfLatAccel) : 0f;
                if (biteRange > 0.05f)
                {
                    Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
                    Gizmos.DrawWireSphere(obsWorldPos, corridor + biteRange);
                }
            }
        }

        private static void DrawControlInputs(Navigator nav)
        {
            if (!nav.showControlInputs || nav.bestSequence == null || nav.bestSequence.Length == 0) return;

            var raw = nav.bestSequence[0];
            var origin = nav.transform.position + nav.controlPanelOffset;

            var cam = Camera.current;
            if (cam == null) return;
            var right = cam.transform.right;
            var up = cam.transform.up;

            var barWidth = 1.2f;
            var barHeight = 0.12f;
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

        private static void DrawControlBar(Vector3 origin, Vector3 right, Vector3 up,
            int row, string label, float value, Color color,
            float barWidth, float barHeight, float halfBar, GUIStyle labelStyle, GUIStyle valueStyle)
        {
            var rowOffset = -up * (row * 0.22f);
            var center = origin + rowOffset;
            var barLeft = center - right * halfBar;

            var bgColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            DrawQuad(barLeft, right, up, barWidth, barHeight, bgColor);

            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            var midBottom = center - up * barHeight * 0.5f;
            var midTop = center + up * barHeight * 0.5f;
            Gizmos.DrawLine(midBottom, midTop);

            // Applied control bar — center of the bar is the zero point.
            var barColor = color;
            barColor.a = 0.85f;
            var valueBarWidth = Mathf.Abs(value) * halfBar;
            var valueBarOrigin = value >= 0 ? center : center - right * valueBarWidth;
            DrawQuad(valueBarOrigin, right, up, valueBarWidth, barHeight * 0.9f, barColor);

            var labelPos = barLeft - right * 0.05f;
            Handles.Label(labelPos, label, labelStyle);

            var valPos = barLeft + right * (barWidth + 0.08f);
            Handles.Label(valPos, $"{value:+0.00;-0.00}", valueStyle);
        }

        private static void DrawQuad(Vector3 bottomLeft, Vector3 right, Vector3 up,
            float width, float height, Color color)
        {
            if (width < 0.001f) return;
            Gizmos.color = color;
            // Gizmos has no filled quad; approximate with horizontal scan lines.
            var steps = Mathf.Max(2, Mathf.CeilToInt(height / 0.02f));
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var y = up * (t * height - height * 0.5f);
                Gizmos.DrawLine(bottomLeft + y, bottomLeft + right * width + y);
            }
        }

        private static Color GetCostColor(float obstacleCost)
        {
            if (obstacleCost < 0.1f)
                return Color.cyan;
            if (obstacleCost < 1f)
                return Color.Lerp(Color.cyan, Color.yellow, obstacleCost);
            if (obstacleCost < 5f)
                return Color.Lerp(Color.yellow, Color.red, (obstacleCost - 1f) / 4f);
            return Color.red;
        }
    }
}
