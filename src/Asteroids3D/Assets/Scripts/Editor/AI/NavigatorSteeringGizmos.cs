using AI.Debug;
using Movement.MPC;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Steering-channel gate for the MPC navigator's gizmos. Interim home until the
    /// MPC domain's editor-assembly conversion moves the drawing itself out of Navigator.Editor.cs.</summary>
    internal static class NavigatorSteeringGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Navigator))]
        private static void Draw(Navigator navigator, GizmoType gizmoType)
        {
            var isSelected = (gizmoType & GizmoType.Selected) != 0;
            if (!AIDebugContext.ShouldDraw(AIDebugChannel.Steering, isSelected)) return;
            navigator.DrawGizmosImpl();
        }
    }
}
