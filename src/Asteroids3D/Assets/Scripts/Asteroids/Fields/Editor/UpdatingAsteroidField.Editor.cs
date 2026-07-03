#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Asteroids.Fields
{
    public partial class UpdatingAsteroidField
    {
        protected override void OnDrawGizmosSelected()
        {
            // Field boundary from the base class.
            base.OnDrawGizmosSelected();
            if (!settings) return;

            // Streaming radii around the anchor (the field origin at edit time).
            var center = transform.position;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(center, settings.loadRadius);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, settings.UnloadRadius);

            Handles.color = Color.white;
            Handles.Label(center + Vector3.forward * (settings.loadRadius + 2f), "Load");
            Handles.Label(center + Vector3.forward * (settings.UnloadRadius + 2f), "Unload");
        }
    }
}
#endif
