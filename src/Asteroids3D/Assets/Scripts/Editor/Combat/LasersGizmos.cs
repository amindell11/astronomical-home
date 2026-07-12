using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    internal static class LasersGizmos
    {
        [DrawGizmo(GizmoType.Selected, typeof(Lasers))]
        private static void DrawHeatBar(Lasers lasers, GizmoType gizmoType)
        {
            var parent = lasers.transform.parent;
            if (!Application.isPlaying || !parent || !lasers.Heat) return;
            var position = parent.position + parent.right * 1.5f;
            var heatRatio = lasers.Heat.HeatPct;

            Handles.Label(position + Vector3.up * 1.2f,
                $"Heat: {lasers.Heat.CurrentHeat:F0}/{lasers.Heat.MaxHeat:F0}");

            var barEnd = position + Vector3.up * 1.0f;

            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Handles.DrawAAPolyLine(3f, position, barEnd);

            if (!(heatRatio > 0)) return;
            Handles.color = Color.Lerp(Color.cyan, Color.red, heatRatio);
            Handles.DrawAAPolyLine(3f, position, Vector3.Lerp(position, barEnd, heatRatio));
        }
    }
}
