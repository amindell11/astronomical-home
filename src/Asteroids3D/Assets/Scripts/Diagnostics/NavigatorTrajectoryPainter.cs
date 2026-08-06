using System.Collections.Generic;
using Movement.MPC;
using Ships;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>The MPC plan in plane-space: a cost-ranked subsample of the candidate fan, the chosen trajectory colored by per-step obstacle cost with planned-yaw ticks and cost labels, and the enemy rollout. Navigators are cached at construction.</summary>
    public sealed class NavigatorTrajectoryPainter : IDiagnosticPainter
    {
        private const int LabelStep = 5;
        private const int CandidateSampleCount = 32;
        private const float CandidateAlphaFalloff = 2f;
        private const float NodeRadius = 0.15f;
        private const float YawTickLength = 0.4f;
        private const float LabelSize = 3f;

        private static readonly Color SelectedCandidate = new(1f, 0.9f, 0.2f, 0.95f);
        private static readonly Color YawTick = new(1f, 1f, 0.4f, 0.7f);
        private static readonly Color EnemyRollout = new(1f, 0.3f, 0.3f, 0.8f);
        private static readonly Color EnemyLabel = new(1f, 0.4f, 0.4f);
        private static readonly Vector2 LabelOffset = new(0f, 0.2f);

        private readonly List<Navigator> navigators = new();

        public NavigatorTrajectoryPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.MpcTrajectories;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var nav in navigators) Draw(canvas, nav);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var nav = ship.GetComponentInChildren<Navigator>();
            if (nav) navigators.Add(nav);
        }

        public static void Draw(IDiagnosticCanvas canvas, Navigator nav)
        {
            if (nav.solver == null) return;
            DrawCandidates(canvas, nav);
            DrawPredicted(canvas, nav);
            DrawEnemyRollout(canvas, nav);
        }

        private static void DrawCandidates(IDiagnosticCanvas canvas, Navigator nav)
        {
            var samples = nav.solver.LastSampleCount;
            var horizon = nav.solver.LastHorizon;
            if (samples == 0 || horizon == 0) return;

            var k = Mathf.Min(CandidateSampleCount, samples);
            SubsampleByCost(nav, samples, k);

            var candidates = nav.solver.Candidates;
            var initial = nav.lastInitialState;
            var denom = Mathf.Max(k - 1, 1);

            for (var i = 0; i < k; i++)
            {
                var idx = nav.visibleCandidateIndices[i];
                var rankFrac = i / (float)denom;
                var alpha = Mathf.Max(0.03f, 0.85f * Mathf.Exp(-rankFrac * CandidateAlphaFalloff));
                var color = idx == nav.selectedCandidateIndex
                    ? SelectedCandidate
                    : new Color(0.4f, 0.7f, 1f, alpha);

                var prev = initial;
                for (var step = 0; step < horizon; step++)
                {
                    var next = Model.Step(prev, candidates[idx * horizon + step], nav.config, nav.dynamics);
                    canvas.Line(Plane(prev.pos), Plane(next.pos), color);
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

        private static void DrawPredicted(IDiagnosticCanvas canvas, Navigator nav)
        {
            var states = nav.predictedStates;
            var sequence = nav.bestSequence;
            if (states == null || states.Length == 0 || sequence == null || sequence.Length == 0) return;

            var config = nav.config;
            var input = nav.solver.BuildCostInput(nav.velocityReference, nav.enemyPos, nav.enemyVel,
                nav.enemyYaw, nav.enemyYawRate, nav.projectileSpeed, states[0].vel, nav.anchored);
            var prevPos = Plane(states[0].pos);
            var prevU = sequence[0];

            for (var i = 1; i < states.Length; i++)
            {
                var state = states[i];
                var u = sequence[i];
                var pos = Plane(state.pos);
                var breakdown = Cost.EvaluateBreakdown(state, u, prevU, input, config, i);

                var severity = breakdown.collision > 0f ? 5f
                    : config.wObstacle > 0f ? breakdown.obstacle / config.wObstacle : 0f;
                var color = CostColor(severity);
                canvas.Line(prevPos, pos, color);
                canvas.Ring(pos, NodeRadius, color);

                // MPC facing convention: fwd = (-sin(yaw), cos(yaw)).
                var yawDir = new Vector2(-Mathf.Sin(state.yaw), Mathf.Cos(state.yaw));
                canvas.Line(pos, pos + yawDir * YawTickLength, YawTick);

                if (i % LabelStep == 0)
                    canvas.Label(pos + LabelOffset,
                        $"Cost: {breakdown.total:F1}\n(O:{breakdown.obstacle + breakdown.collision:F1})",
                        Color.white, LabelSize);

                prevPos = pos;
                prevU = u;
            }
        }

        private static void DrawEnemyRollout(IDiagnosticCanvas canvas, Navigator nav)
        {
            var count = nav.solver.LastEnemyStateCount;
            if (count == 0) return;

            var states = nav.solver.EnemyStates;
            var prevPos = Plane(states[0].pos);

            for (var i = 1; i < count; i++)
            {
                var pos = Plane(states[i].pos);
                canvas.Line(prevPos, pos, EnemyRollout);
                canvas.Ring(pos, NodeRadius, EnemyRollout);
                if (i % LabelStep == 0)
                    canvas.Label(pos + LabelOffset, $"Enemy t+{i}", EnemyLabel, LabelSize);
                prevPos = pos;
            }
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
