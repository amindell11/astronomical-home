#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Asteroids.Fields
{
    public partial class UpdatingAsteroidField
    {
        protected override void OnDrawGizmosSelected()
        {
            // Draw the base class gizmos first (initial spawn zone and density check)
            base.OnDrawGizmosSelected();

            // Now draw our update spawn zone
            var center = SpawnCenter;
            center.y = 0f;

            // Draw update spawn zone with different colors
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(center, updateMinSpawnDistance);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, updateMaxSpawnDistance);

            // Add labels to distinguish the zones
            Handles.color = Color.white;
            Handles.Label(center + Vector3.forward * (minSpawnDistance + 2f), "Initial Min");
            Handles.Label(center + Vector3.forward * (maxSpawnDistance + 2f), "Initial Max");
            Handles.Label(center + Vector3.forward * (updateMinSpawnDistance + 2f), "Update Min");
            Handles.Label(center + Vector3.forward * (updateMaxSpawnDistance + 2f), "Update Max");
        }
    }
}
#endif
