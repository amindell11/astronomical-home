#if UNITY_EDITOR
using UnityEngine;

namespace EnemyAI
{
    public partial class AIContext
    {
        void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;
        
            Vector3 pos = transform.position;
        
            // Nearby ship radius
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, nearbyShipRadius);
        
            // Asteroid cover radius
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.2f);
            Gizmos.DrawWireSphere(pos, asteroidCoverRadius);
        }
    }
}
#endif
