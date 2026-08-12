using Game;
using UnityEditor;
using UnityEngine;

namespace Cameras
{
    /// <summary>The boundary the observer camera is framing, as a plane-space rectangle.</summary>
    internal static class ObserverCamGizmos
    {
        [DrawGizmo(GizmoType.Selected, typeof(ObserverCam))]
        private static void DrawSubjectBounds(ObserverCam cam, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!cam.TryGetBoundaryAroundAllSubjects(out var min, out var max)) return;

            Gizmos.color = Color.yellow;
            var bl = new Vector2(min.x, min.y);
            var br = new Vector2(max.x, min.y);
            var tr = new Vector2(max.x, max.y);
            var tl = new Vector2(min.x, max.y);
            Line(bl, br);
            Line(br, tr);
            Line(tr, tl);
            Line(tl, bl);
        }

        private static void Line(Vector2 a, Vector2 b) =>
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
    }
}
