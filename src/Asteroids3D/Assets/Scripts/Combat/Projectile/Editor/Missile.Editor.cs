#if UNITY_EDITOR
using Game;
using UnityEditor;
using UnityEngine;

namespace Combat.Projectile
{
    public partial class Missile
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = target ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            if (rb && rb.linearVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2f);
            }

            if (target)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target.position);
            }

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);

            if (!Application.isPlaying) return;
            Handles.color = Color.white;
            Handles.Label(transform.position + Vector3.up, $"Dist: {DistanceTraveled:F1}/{maxDistance:F1}");
        }
    }
}
#endif
