#if UNITY_EDITOR
using Game;
using UnityEditor;
using UnityEngine;

namespace Weapons
{
    public partial class Missile
    {
        void OnDrawGizmos()
        {   
            // Draw game-plane normal (for orientation reference)
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, GamePlane.Normal * 2f);

            // Draw missile position
            Gizmos.color = target != null ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        
            // Draw velocity vector
            if (rb != null && rb.linearVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2f);
            }
        
            // Draw line to target
            if (target != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target.position);
            }
        
            // Draw explosion radius
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        
            // Draw travel distance
            if (Application.isPlaying)
            {
                Handles.color = Color.white;
                var traveled = Vector3.Distance(startPosition, transform.position);
                Handles.Label(transform.position + Vector3.up, $"Missile\nDist: {traveled:F1}/{maxDistance:F1}");
            }

            // Draw rotation correction arc (only when a target exists and we applied a turn)
            if (target != null && Mathf.Abs(rotationCorrectionDeg) > 0.1f)
            {
                Handles.color = Color.magenta;
                Handles.DrawWireArc(
                    transform.position,               // centre
                    GamePlane.Normal,                  // plane normal
                    transform.up,                      // start direction
                    rotationCorrectionDeg,             // sweep angle (deg)
                    2f);                               // radius
            }
        }
    }
}
#endif
