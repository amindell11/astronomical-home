using Game;
using UnityEditor;
using UnityEngine;

namespace Ships.Damage
{
    internal static class DamageControllerGizmos
    {
        [DrawGizmo(GizmoType.Selected, typeof(DamageController))]
        private static void DrawHealthBars(DamageController damage, GizmoType gizmoType)
        {
            const float baseOffset = 2f;
            const float barSpacing = 0.25f;
            const float textSpacing = .75f;
            const float barWidth = 3.5f;
            const float barHeight = 0.25f;
            const float barDepth = 0.1f;

            var shieldBarPos = damage.transform.position + GamePlane.Forward * baseOffset;
            var healthBarPos = shieldBarPos + GamePlane.Forward * barSpacing;
            var shieldTextPos = healthBarPos + GamePlane.Forward * (barSpacing * 2 + textSpacing * 2);
            var healthTextPos = shieldTextPos + GamePlane.Forward * textSpacing;

            var barSize = GamePlane.Right * barWidth + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;

            Gizmos.color = Color.gray;
            Gizmos.DrawCube(shieldBarPos, barSize);
            if (damage.maxShield > 0)
            {
                var shieldPercent = damage.Shield.Pct;
                Gizmos.color = Color.cyan;
                var fillPos = shieldBarPos - GamePlane.Right * (barWidth * (1f - shieldPercent) * 0.5f);
                var fillSize = GamePlane.Right * (barWidth * shieldPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
                Gizmos.DrawCube(fillPos, fillSize);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawCube(healthBarPos, barSize);
            if (damage.maxHealth > 0)
            {
                var healthPercent = damage.Health.Pct;
                Gizmos.color = Color.green;
                var fillPos = healthBarPos - GamePlane.Right * (barWidth * (1f - healthPercent) * 0.5f);
                var fillSize = GamePlane.Right * (barWidth * healthPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
                Gizmos.DrawCube(fillPos, fillSize);
            }

            var shieldText = $"Shield: {damage.Shield.CurrentValue:F1}/{damage.maxShield:F1}";
            var healthText = $"Health: {damage.Health.CurrentValue:F1}/{damage.maxHealth:F1}";

            var style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.alignment = TextAnchor.MiddleCenter;

            Handles.Label(shieldTextPos, shieldText, style);
            Handles.Label(healthTextPos, healthText, style);
        }
    }
}
