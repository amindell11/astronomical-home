using UnityEditor;
using UnityEngine;

namespace Combat.Projectile
{
    internal static class MissileGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Missile))]
        private static void Draw(Missile missile, GizmoType gizmoType)
        {
            var position = missile.transform.position;

            Gizmos.color = missile.target ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(position, 0.5f);

            if (missile.rb && missile.rb.linearVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(position, missile.rb.linearVelocity.normalized * 2f);
            }

            if (missile.target)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, missile.target.position);
            }

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(position, missile.explosionRadius);

            if (!Application.isPlaying) return;
            Handles.color = Color.white;
            Handles.Label(position + Vector3.up,
                $"Dist: {missile.DistanceTraveled:F1}/{missile.MaxDistance:F1}");
        }
    }
}
