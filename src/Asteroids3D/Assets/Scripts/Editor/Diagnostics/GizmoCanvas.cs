using Game;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Live-editor backend for the diagnostic-canvas contract: draws with Gizmos/Handles into the scene view. Cosmetic parity with the capture backend is a non-goal — one painter source of truth is. <see cref="LineWidth"/> is a no-op (Gizmos carry no width).</summary>
    public sealed class GizmoCanvas : IDiagnosticCanvas
    {
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        public float LineWidth { get; set; }

        public void Line(Vector2 a, Vector2 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
        }

        public void Vector(Vector2 origin, Vector2 v, Color color) => Line(origin, origin + v, color);

        public void Ring(Vector2 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius);
        }

        public void Rect(Vector2 center, Vector2 size, Color color)
        {
            var half = size * 0.5f;
            var bl = center + new Vector2(-half.x, -half.y);
            var br = center + new Vector2(half.x, -half.y);
            var tr = center + new Vector2(half.x, half.y);
            var tl = center + new Vector2(-half.x, half.y);
            Line(bl, br, color);
            Line(br, tr, color);
            Line(tr, tl, color);
            Line(tl, bl, color);
        }

        public void Arc(Vector2 center, float radius, Vector2 fromDir, float sweepRad, Color color)
        {
            if (fromDir.sqrMagnitude < 1e-8f) return;
            Handles.color = color;
            Handles.DrawWireArc(GamePlane.PlanePointToWorld(center), PlaneNormal,
                GamePlane.PlaneDirToWorld(fromDir), sweepRad * Mathf.Rad2Deg, radius);
        }

        public void Trail(Vector2 head, Vector2 dir, float length, Color color)
        {
            if (dir.sqrMagnitude < 1e-8f) return;
            Line(head - dir.normalized * length, head, color);
        }

        public void Label(Vector2 pos, string text, Color color, float size = 4f) =>
            Handles.Label(GamePlane.PlanePointToWorld(pos), text,
                new GUIStyle { normal = { textColor = color }, fontSize = Mathf.RoundToInt(size * 3f) });
    }
}
