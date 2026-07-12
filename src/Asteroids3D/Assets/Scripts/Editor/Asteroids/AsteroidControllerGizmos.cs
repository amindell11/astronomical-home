using UnityEditor;
using UnityEngine;

namespace Asteroids
{
    internal static class AsteroidControllerGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(AsteroidController))]
        private static void DrawVelocityAndHealth(AsteroidController asteroid, GizmoType gizmoType)
        {
            var transform = asteroid.transform;
            if (asteroid.Rb)
            {
                Gizmos.color = Color.yellow;
                var velocity = asteroid.Rb.linearVelocity;
                var start = transform.position;
                var end = start + velocity.normalized * 2f;
                Gizmos.DrawLine(start, end);

                var right = Quaternion.Euler(0, 0, 30) * -velocity.normalized * 0.4f;
                var left = Quaternion.Euler(0, 0, -30) * -velocity.normalized * 0.4f;
                Gizmos.DrawLine(end, end + right);
                Gizmos.DrawLine(end, end + left);
            }

            var damage = asteroid.Damage;
            if (!Application.isPlaying || !(damage.MaxHealth > 0f)) return;
            var healthPercent = Mathf.Clamp01(damage.Health / damage.MaxHealth);
            Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercent);

            var barLength = transform.localScale.x;
            var barScale = 0.05f * transform.localScale.x;
            var barSize = new Vector3(barLength * healthPercent, barScale, barScale);
            var barPosition = transform.position + Vector3.up * (transform.localScale.x * 0.6f);
            Gizmos.DrawCube(barPosition, barSize);
        }
    }
}
