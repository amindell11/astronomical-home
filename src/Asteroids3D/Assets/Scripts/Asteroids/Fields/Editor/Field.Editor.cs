#if UNITY_EDITOR
using UnityEngine;

namespace Asteroids.Fields
{
    public partial class AsteroidField
    {
        protected virtual void OnDrawGizmosSelected()
        {
            var center = SpawnCenter;
            center.y = 0f;

            Gizmos.color = Color.cyan;
            const int segments = 32;
            var angle = 0f;
            var lastPoint = center + new Vector3(Mathf.Cos(angle) * densityCheckRadius, 0, Mathf.Sin(angle) * densityCheckRadius);
            for (var i = 1; i <= segments; i++)
            {
                angle = (i / (float)segments) * Mathf.PI * 2f;
                var nextPoint = center + new Vector3(Mathf.Cos(angle) * densityCheckRadius, 0, Mathf.Sin(angle) * densityCheckRadius);
                Gizmos.DrawLine(lastPoint, nextPoint);
                lastPoint = nextPoint;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, minSpawnDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, maxSpawnDistance);
        }
    }
}
#endif
