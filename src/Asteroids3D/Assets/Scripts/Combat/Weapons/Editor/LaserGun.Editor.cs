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
            if (Application.isPlaying && transform.parent != null && heat != null)
            {
                var position = transform.parent.position + transform.parent.right * 1.5f;
                var heatRatio = heat.HeatPct;
            
                Handles.Label(position + Vector3.up * 1.2f, $"Heat: {heat.CurrentHeat:F0}/{heat.MaxHeat:F0}");

                var barStart = position;
                var barEnd = position + Vector3.up * 1.0f;
            
                // Background
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                Handles.DrawAAPolyLine(3f, barStart, barEnd);

                // Foreground
                if (heatRatio > 0)
                {
                    Handles.color = Color.Lerp(Color.cyan, Color.red, heatRatio);
                    Handles.DrawAAPolyLine(3f, barStart, Vector3.Lerp(barStart, barEnd, heatRatio));
                }
            }
        }
    }
}
#endif
