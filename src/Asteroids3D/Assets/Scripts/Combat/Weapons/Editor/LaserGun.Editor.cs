#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    public partial class WeaponLaser
    {
        private void OnDrawGizmosSelected()
        {
            // Draw heat bar
            if (!Application.isPlaying || !transform.parent || !Heat) return;
            var position = transform.parent.position + transform.parent.right * 1.5f;
            var heatRatio = Heat.HeatPct;
            
            Handles.Label(position + Vector3.up * 1.2f, $"Heat: {Heat.CurrentHeat:F0}/{Heat.MaxHeat:F0}");

            var barEnd = position + Vector3.up * 1.0f;
            
            // Background
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Handles.DrawAAPolyLine(3f, position, barEnd);

            // Foreground
            if (!(heatRatio > 0)) return;
            Handles.color = Color.Lerp(Color.cyan, Color.red, heatRatio);
            Handles.DrawAAPolyLine(3f, position, Vector3.Lerp(position, barEnd, heatRatio));
        }
    }
}
#endif
