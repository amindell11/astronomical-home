#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Weapons
{
    public partial class LaserGun
    {
        void OnDrawGizmosSelected()
        {
            // Draw heat bar
            if (Application.isPlaying && transform.parent != null && _heat != null)
            {
                Vector3 position = transform.parent.position + transform.parent.right * 1.5f;
                float heatRatio = _heat.HeatPct;
            
                Handles.Label(position + Vector3.up * 1.2f, $"Heat: {_heat.CurrentHeat:F0}/{_heat.MaxHeat:F0}");

                Vector3 barStart = position;
                Vector3 barEnd = position + Vector3.up * 1.0f;
            
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
