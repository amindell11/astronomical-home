using Game;
using Movement.MPC;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>What the MPC solved this step: the sampled candidate fan, the chosen path with its costs, the enemy rollout, the collision hulls it tested, and the applied control bars. Per-Navigator toggles choose which of those subviews are worth their cost.</summary>
    internal static class NavigatorGizmos
    {
        private const float NodeRadius = 0.15f;
        private const float YawTickLength = 0.4f;
        private const float LabelSize = 3f;
        private const float BarWidth = 1.2f;
        private const float BarHeight = 0.12f;

        private static readonly Color SelectedCandidate = new(1f, 0.9f, 0.2f, 0.95f);
        private static readonly Color YawTick = new(1f, 1f, 0.4f, 0.7f);
        private static readonly Color EnemyRollout = new(1f, 0.3f, 0.3f, 0.8f);
        private static readonly Color EnemyLabel = new(1f, 0.4f, 0.4f);
        private static readonly Color ShipRadius = new(0f, 1f, 1f, 0.25f);
        private static readonly Color HullUnbanked = new(1f, 1f, 1f, 0.8f);
        private static readonly Color HullBanked = new(0.3f, 0.8f, 1f, 0.8f);
        private static readonly Color BiteRange = new(1f, 1f, 0f, 0.35f);
        private static readonly Vector2 LabelOffset = new(0f, 0.2f);
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        [DrawGizmo(GizmoType.Selected, typeof(Navigator))]
        private static void Draw(Navigator nav, GizmoType gizmoType)
        {
            if (!Application.isPlaying || nav.mpc == null) return;

            if (nav.solver != null)
            {
                if (nav.showCandidateTrajectories) DrawCandidates(nav);
                DrawPredicted(nav);
                DrawEnemyRollout(nav);
            }
            if (nav.showObstacleCosts) DrawObstacles(nav);
            if (nav.showControlInputs) DrawControlInputs(nav);
        }

        private static void DrawCandidates(Navigator nav)
        {
            var samples = nav.solver.LastSampleCount;
            var horizon = nav.solver.LastHorizon;
            if (samples == 0 || horizon == 0) return;

            var k = Mathf.Min(nav.candidateSampleCount, samples);
            SubsampleByCost(nav, samples, k);

            var candidates = nav.solver.Candidates;
            var initial = nav.lastInitialState;
            var denom = Mathf.Max(k - 1, 1);

            for (var i = 0; i < k; i++)
            {
                var idx = nav.visibleCandidateIndices[i];
                var rankFrac = i / (float)denom;
                var alpha = Mathf.Max(0.03f, 0.85f * Mathf.Exp(-rankFrac * nav.candidateAlphaFalloff));
                var color = idx == nav.selectedCandidateIndex
                    ? SelectedCandidate
                    : new Color(0.4f, 0.7f, 1f, alpha);

                var prev = initial;
                for (var step = 0; step < horizon; step++)
                {
                    var next = Model.Step(prev, candidates[idx * horizon + step], nav.config, nav.dynamics);
                    Line(Plane(prev.pos), Plane(next.pos), color);
                    prev = next;
                }
            }
        }

        // Stable per-frame reservoir subsample, then insertion-sorted ascending by cost so rank-based alpha reads best=opaque.
        private static void SubsampleByCost(Navigator nav, int samples, int k)
        {
            if (nav.visibleCandidateIndices == null || nav.visibleCandidateIndices.Length < k)
                nav.visibleCandidateIndices = new int[k];
            nav.visibleCount = k;

            var rng = new Unity.Mathematics.Random((uint)(Time.frameCount * 31u + 1u));
            for (var i = 0; i < k; i++) nav.visibleCandidateIndices[i] = i;
            for (var i = k; i < samples; i++)
            {
                var j = rng.NextInt(0, i + 1);
                if (j < k) nav.visibleCandidateIndices[j] = i;
            }

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
        }

        private static void DrawPredicted(Navigator nav)
        {
            var states = nav.predictedStates;
            var sequence = nav.bestSequence;
            if (states == null || states.Length == 0 || sequence == null || sequence.Length == 0) return;

            var config = nav.config;
            var withCosts = nav.showTrajectoryCosts;
            var input = withCosts
                ? nav.solver.BuildCostInput(nav.CostVelocityReference, nav.enemyPos, nav.enemyVel,
                    nav.enemyYaw, nav.enemyYawRate, nav.projectileSpeed, states[0].vel, nav.sentence,
                    nav.referent1, nav.referent2, nav.referent3)
                : default;
            var prevPos = Plane(states[0].pos);
            var prevU = sequence[0];

            for (var i = 1; i < states.Length; i++)
            {
                var state = states[i];
                var u = sequence[i];
                var pos = Plane(state.pos);
                var color = Color.cyan;

                if (withCosts)
                {
                    var breakdown = Cost.EvaluateBreakdown(state, u, prevU, input, config, i);
                    var severity = breakdown.collision > 0f ? 5f
                        : config.wObstacle > 0f ? breakdown.obstacle / config.wObstacle : 0f;
                    color = CostColor(severity);
                    if (i % nav.labelStep == 0)
                        Label(pos + LabelOffset,
                            $"Cost: {breakdown.total:F1}\n(O:{breakdown.obstacle + breakdown.collision:F1})",
                            Color.white);
                }

                Line(prevPos, pos, color);
                Ring(pos, NodeRadius, color);

                // MPC facing convention: fwd = (-sin(yaw), cos(yaw)).
                var yawDir = new Vector2(-Mathf.Sin(state.yaw), Mathf.Cos(state.yaw));
                Line(pos, pos + yawDir * YawTickLength, YawTick);

                prevPos = pos;
                prevU = u;
            }
        }

        private static void DrawEnemyRollout(Navigator nav)
        {
            var count = nav.solver.LastEnemyStateCount;
            if (count == 0) return;

            var states = nav.solver.EnemyStates;
            var prevPos = Plane(states[0].pos);

            for (var i = 1; i < count; i++)
            {
                var pos = Plane(states[i].pos);
                Line(prevPos, pos, EnemyRollout);
                Ring(pos, NodeRadius, EnemyRollout);
                if (i % nav.labelStep == 0) Label(pos + LabelOffset, $"Enemy t+{i}", EnemyLabel);
                prevPos = pos;
            }
        }

        private static void DrawObstacles(Navigator nav)
        {
            if (nav.dynamics.shipRadius > 0f)
                Ring(GamePlane.WorldPointToPlane(nav.transform.position), nav.dynamics.shipRadius, ShipRadius);
            if (!nav.scout) return;

            var scan = nav.scout.ObstacleScan;
            if (scan.count == 0) return;

            var config = nav.config;
            var profileScale = config.maxBankAngleRad > 0f
                ? Mathf.Cos(Mathf.Abs(nav.lastControl.strafe) * config.maxBankAngleRad)
                : 1f;
            var hullUnbanked = config.shipRadius + config.collisionSafetyMargin;
            var hullCurrent = config.shipRadius * profileScale + config.collisionSafetyMargin;

            var states = nav.predictedStates;
            var speed = states != null && states.Length > 0 ? math.length(states[0].vel) : 0f;
            var halfLatAccel = 0.5f * Mathf.Max(config.maxLatAccel, 1e-4f);

            for (var i = 0; i < scan.count; i++)
            {
                var obs = scan.buffer[i];
                Ring(obs.position, obs.radius + hullUnbanked, HullUnbanked);

                if (hullCurrent < hullUnbanked - 1e-4f)
                    Ring(obs.position, obs.radius + hullCurrent, HullBanked);

                // Turn-away bite range: head-on distance inside which lateral thrust can't sidestep a full corridor before impact (½·a_lat·t² == corridor at t = along/speed).
                var corridor = obs.radius + hullCurrent;
                var biteRange = speed > 0.05f ? speed * math.sqrt(corridor / halfLatAccel) : 0f;
                if (biteRange > 0.05f) Ring(obs.position, corridor + biteRange, BiteRange);
            }
        }

        // Camera-facing billboard bars: no plane-space form, so they are drawn against the current camera basis.
        private static void DrawControlInputs(Navigator nav)
        {
            var sequence = nav.bestSequence;
            if (sequence == null || sequence.Length == 0) return;

            var cam = Camera.current;
            if (!cam) return;

            var raw = sequence[0];
            var origin = nav.transform.position + nav.controlPanelOffset;
            var right = cam.transform.right;
            var up = cam.transform.up;

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
                new Color(0.2f, 0.9f, 0.3f), labelStyle, valueStyle);
            DrawControlBar(origin, right, up, 1, "STR", raw.strafe,
                new Color(0.3f, 0.6f, 1f), labelStyle, valueStyle);
            DrawControlBar(origin, right, up, 2, "YAW", raw.yawTorque,
                new Color(1f, 0.4f, 0.8f), labelStyle, valueStyle);
        }

        private static void DrawControlBar(Vector3 origin, Vector3 right, Vector3 up,
            int row, string label, float value, Color color, GUIStyle labelStyle, GUIStyle valueStyle)
        {
            var halfBar = BarWidth * 0.5f;
            var center = origin - up * (row * 0.22f);
            var barLeft = center - right * halfBar;

            DrawQuad(barLeft, right, up, BarWidth, BarHeight, new Color(0.15f, 0.15f, 0.15f, 0.6f));

            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            Gizmos.DrawLine(center - up * BarHeight * 0.5f, center + up * BarHeight * 0.5f);

            var barColor = color;
            barColor.a = 0.85f;
            var valueBarWidth = Mathf.Abs(value) * halfBar;
            var valueBarOrigin = value >= 0 ? center : center - right * valueBarWidth;
            DrawQuad(valueBarOrigin, right, up, valueBarWidth, BarHeight * 0.9f, barColor);

            Handles.Label(barLeft - right * 0.05f, label, labelStyle);
            Handles.Label(barLeft + right * (BarWidth + 0.08f), $"{value:+0.00;-0.00}", valueStyle);
        }

        private static void DrawQuad(Vector3 bottomLeft, Vector3 right, Vector3 up,
            float width, float height, Color color)
        {
            if (width < 0.001f) return;
            Gizmos.color = color;
            // Gizmos has no filled quad; approximate with scan lines.
            var steps = Mathf.Max(2, Mathf.CeilToInt(height / 0.02f));
            for (var i = 0; i <= steps; i++)
            {
                var y = up * (i / (float)steps * height - height * 0.5f);
                Gizmos.DrawLine(bottomLeft + y, bottomLeft + right * width + y);
            }
        }

        private static void Label(Vector2 pos, string text, Color color) =>
            Handles.Label(GamePlane.PlanePointToWorld(pos), text,
                new GUIStyle { normal = { textColor = color }, fontSize = Mathf.RoundToInt(LabelSize * 3f) });

        private static void Ring(Vector2 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius);
        }

        private static void Line(Vector2 a, Vector2 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
        }

        private static Vector2 Plane(float2 p) => new(p.x, p.y);

        private static Color CostColor(float obstacleCost)
        {
            if (obstacleCost < 0.1f) return Color.cyan;
            if (obstacleCost < 1f) return Color.Lerp(Color.cyan, Color.yellow, obstacleCost);
            if (obstacleCost < 5f) return Color.Lerp(Color.yellow, Color.red, (obstacleCost - 1f) / 4f);
            return Color.red;
        }
    }
}
