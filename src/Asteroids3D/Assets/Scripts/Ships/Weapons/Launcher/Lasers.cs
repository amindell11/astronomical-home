using System;
using Ships.Weapons.Conditions;
using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Concrete weapon that fires pooled <see cref="LaserProjectile"/> instances.
    /// All common launcher logic lives in <see cref="LauncherBase{TProj}"/>.
    /// This weapon uses a heat system for ammo.
    /// </summary>
    public class LaserGun : LauncherBase<LaserProjectile>
    {
        public float ProjectileSpeed => projectilePrefab.LaserSpeed;

        private Heat _heat;

        protected override void Awake()
        {
            base.Awake();
            _heat = GetComponent<Heat>();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // Draw heat bar
            if (Application.isPlaying && transform.parent && _heat != null)
            {
                Vector3 position = transform.parent.position + transform.parent.right * 1.5f;
                float heatRatio = _heat.HeatPct;
            
                UnityEditor.Handles.Label(position + Vector3.up * 1.2f, $"Heat: {_heat.CurrentHeat:F0}/{_heat.MaxHeat:F0}");

                Vector3 barStart = position;
                Vector3 barEnd = position + Vector3.up * 1.0f;
            
                // Background
                UnityEditor.Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                UnityEditor.Handles.DrawAAPolyLine(3f, barStart, barEnd);

                // Foreground
                if (heatRatio > 0)
                {
                    UnityEditor.Handles.color = Color.Lerp(Color.cyan, Color.red, heatRatio);
                    UnityEditor.Handles.DrawAAPolyLine(3f, barStart, Vector3.Lerp(barStart, barEnd, heatRatio));
                }
            }
        }
#endif
    }
} 