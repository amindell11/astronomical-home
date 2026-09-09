using Game;
using Game.Diagnostics;
using Movement.MPC;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>The ship's baked terminal field in plane space: the grid domain, occupied cells, a detour-excess heat over free cells normalised to the grid's own maximum, the goal and its seed cell, and the excess the emitted plan pays at its endpoint. Registers as the Navigator's "field" subview.</summary>
    [InitializeOnLoad]
    internal static class TerminalFieldGizmos
    {
        static TerminalFieldGizmos() =>
            GizmoView.Register(typeof(Navigator), "field", "Terminal Field",
                "route-field grid: red occupied cells, green→red detour-excess heat, magenta goal, endpoint excess", "Steering");

        private const float CellFill = 0.85f;
        private const float MinExcessDrawn = 0.25f;

        private static readonly Color Domain = new(1f, 1f, 1f, 0.35f);
        private static readonly Color Occupied = new(1f, 0.15f, 0.15f, 0.4f);
        private static readonly Color Disconnected = new(0.6f, 0.2f, 0.8f, 0.3f);
        private static readonly Color HeatLow = new(0.2f, 1f, 0.2f, 0.08f);
        private static readonly Color HeatHigh = new(1f, 0.3f, 0.1f, 0.35f);
        private static readonly Color Goal = new(1f, 0.2f, 1f, 0.9f);
        private static readonly Color Seed = new(1f, 1f, 1f, 0.9f);
        private static readonly Color Endpoint = new(1f, 0.9f, 0.2f, 0.9f);
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Navigator))]
        private static void Draw(Navigator nav, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(Navigator), "field") || !GizmoView.InScope(nav)) return;
            if (!Application.isPlaying || nav.mpc == null) return;
            var field = nav.mpc.TerminalField;
            var view = field.View;
            if (!view.IsValid) return;

            var n = view.resolution;
            var h = view.spacing;
            var occupied = field.Occupied;

            var maxExcess = 0f;
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                if (occupied[x + y * n] != 0 || !math.isfinite(view.CellDistance(x, y))) continue;
                maxExcess = math.max(maxExcess, view.CellExcess(x, y));
            }
            var heatSpan = math.max(maxExcess, h);

            var previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(GamePlane.PlanePointToWorld(Vector2.zero), GamePlane.Rotation, Vector3.one);
            var cell = new Vector3(h * CellFill, h * CellFill, 0.05f);
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var centre = view.CellCentre(x, y);
                Color color;
                if (occupied[x + y * n] != 0) color = Occupied;
                else if (!math.isfinite(view.CellDistance(x, y))) color = Disconnected;
                else
                {
                    var excess = view.CellExcess(x, y);
                    if (excess < MinExcessDrawn) continue;
                    color = Color.Lerp(HeatLow, HeatHigh, excess / heatSpan);
                }
                Gizmos.color = color;
                Gizmos.DrawCube(new Vector3(centre.x, centre.y, 0f), cell);
            }

            Gizmos.color = Domain;
            var lo = view.origin;
            var hi = view.origin + (n - 1) * h;
            Gizmos.DrawLine(new Vector3(lo.x, lo.y, 0f), new Vector3(hi.x, lo.y, 0f));
            Gizmos.DrawLine(new Vector3(hi.x, lo.y, 0f), new Vector3(hi.x, hi.y, 0f));
            Gizmos.DrawLine(new Vector3(hi.x, hi.y, 0f), new Vector3(lo.x, hi.y, 0f));
            Gizmos.DrawLine(new Vector3(lo.x, hi.y, 0f), new Vector3(lo.x, lo.y, 0f));
            Gizmos.matrix = previous;

            Ring(view.goal, math.max(1f, 0.3f * h), Goal);
            Ring(view.CellCentre(view.seedIndex % n, view.seedIndex / n), 0.5f * h, Seed);

            var ship = nav.lastInitialState.pos;
            var predicted = nav.predictedStates;
            var endpointText = "";
            if (predicted != null && predicted.Length > 0)
            {
                var end = predicted[predicted.Length - 1].pos;
                Ring(end, 0.4f * h, Endpoint);
                endpointText = $"  end +{view.DetourExcess(end):F1} m";
            }
            Handles.color = Domain;
            Handles.Label(GamePlane.PlanePointToWorld(ship + new float2(0f, -1.2f * h)),
                $"field h={h:F1} m  here +{view.DetourExcess(ship):F1} m{endpointText}  max {maxExcess:F0} m  bakes {field.BakeCount}");
        }

        private static void Ring(float2 centre, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(new Vector2(centre.x, centre.y)), PlaneNormal, radius);
        }
    }
}
