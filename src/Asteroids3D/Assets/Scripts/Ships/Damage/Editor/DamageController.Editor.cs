#if UNITY_EDITOR
using Game;
using UnityEditor;
using UnityEngine;

namespace Ships.Damage
{
    public partial class Controller
    {
        private void OnDrawGizmosSelected()
        {
            /* ------------------- Configurable offsets ------------------- */
            const float baseOffset   = 2f;   // distance from ship to first element (shield bar)
            const float barSpacing   = 0.25f; // gap between shield and health bars
            const float textSpacing  = .75f;  // gap between each text line
            const float barWidth     = 3.5f;
            const float barHeight    = 0.25f;
            const float barDepth     = 0.1f; // thickness along GamePlane.Normal

            /* ------------------- Position chain ------------------------- */
            // Start just "above" the ship along the forward axis
            var shieldBarPos  = transform.position + GamePlane.Forward * baseOffset;
            var healthBarPos  = shieldBarPos   + GamePlane.Forward * barSpacing;
            var shieldTextPos = healthBarPos   + GamePlane.Forward * (barSpacing*2+textSpacing*2);
            var healthTextPos = shieldTextPos  + GamePlane.Forward * textSpacing;

            /* ------------------- Draw Bars ------------------------------ */
            var barSize = GamePlane.Right * barWidth + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;

            // Shield Bar (background + fill)
            Gizmos.color = Color.gray; // background
            Gizmos.DrawCube(shieldBarPos, barSize);
            if (maxShield > 0)
            {
                var shieldPercent = Shield.Pct;
                Gizmos.color = Color.cyan; // fill
                var fillPos  = shieldBarPos - GamePlane.Right * (barWidth * (1f - shieldPercent) * 0.5f);
                var fillSize = GamePlane.Right * (barWidth * shieldPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
                Gizmos.DrawCube(fillPos, fillSize);
            }

            // Health Bar (background + fill)
            Gizmos.color = Color.red; // background
            Gizmos.DrawCube(healthBarPos, barSize);
            if (maxHealth > 0)
            {
                var healthPercent = Health.Pct;
                Gizmos.color = Color.green; // fill
                var fillPos  = healthBarPos - GamePlane.Right * (barWidth * (1f - healthPercent) * 0.5f);
                var fillSize = GamePlane.Right * (barWidth * healthPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
                Gizmos.DrawCube(fillPos, fillSize);
            }

            /* ------------------- Draw Text ------------------------------ */
            var shieldText = $"Shield: {Shield.CurrentValue:F1}/{maxShield:F1}";
            var healthText = $"Health: {Health.CurrentValue:F1}/{maxHealth:F1}";

            var style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.alignment = TextAnchor.MiddleCenter;

            Handles.Label(shieldTextPos, shieldText, style);
            Handles.Label(healthTextPos, healthText, style);
        }
    }
}
#endif
