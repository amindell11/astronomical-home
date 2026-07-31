using AI.Debug;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Live-editor shim over <see cref="PolicyPainter"/>: the DrawGizmo per-subject hook plus AIDebugChannel.Policy gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class PolicyGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!AIDebugContext.ShouldDraw(AIDebugChannel.Policy, gizmoType)) return;
            var settings = AIDebugContext.Settings;
            PolicyPainter.Draw(new GizmoCanvas(), commander, settings ? settings.policyFanDepth : 0);
        }
    }
}
