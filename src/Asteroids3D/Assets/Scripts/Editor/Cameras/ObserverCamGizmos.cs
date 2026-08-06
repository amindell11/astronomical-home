using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Cameras
{
    /// <summary>Subject-bounds rectangle drawn onto a <see cref="GizmoCanvas"/>, gated by <see cref="DiagnosticGate"/>. Editor-only, so the atom name lives here rather than in the painter registry: an ObserverCam is world-scoped, and <c>PainterContext</c> carries only per-ship subjects — capturing this would need a seam that does not exist yet.</summary>
    internal static class ObserverCamGizmos
    {
        internal const string CamBounds = "cam-bounds";

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(ObserverCam))]
        private static void DrawSubjectBounds(ObserverCam cam, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(CamBounds, gizmoType)) return;
            if (!cam.TryGetBoundaryAroundAllSubjects(out var min, out var max)) return;
            new GizmoCanvas().Rect((min + max) * 0.5f, max - min, Color.yellow);
        }
    }
}
