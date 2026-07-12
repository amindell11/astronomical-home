using Game;
using UnityEditor;
using UnityEngine;

namespace Cameras
{
    internal static class ObserverCamGizmos
    {
        [DrawGizmo(GizmoType.Selected, typeof(ObserverCam))]
        private static void DrawSubjectBounds(ObserverCam cam, GizmoType gizmoType)
        {
            if (!Application.isPlaying || cam.SecondarySubjects == null || cam.SecondarySubjects.Count == 0) return;
            if (!cam.TryGetBoundaryAroundAllSubjects(out var min2D, out var max2D)) return;

            var p00 = GamePlane.PlanePointToWorld(new Vector2(min2D.x, min2D.y));
            var p01 = GamePlane.PlanePointToWorld(new Vector2(min2D.x, max2D.y));
            var p11 = GamePlane.PlanePointToWorld(new Vector2(max2D.x, max2D.y));
            var p10 = GamePlane.PlanePointToWorld(new Vector2(max2D.x, min2D.y));

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p00, p01);
            Gizmos.DrawLine(p01, p11);
            Gizmos.DrawLine(p11, p10);
            Gizmos.DrawLine(p10, p00);
        }
    }
}
