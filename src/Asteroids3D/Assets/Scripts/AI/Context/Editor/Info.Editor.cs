#if UNITY_EDITOR
using UnityEngine;
namespace AI.Context
{
    public partial class Info
    {
        void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;

            Vector3 pos = transform.position;

            // Nearby ship radius
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, nearbyShipRadius);
        }
    }
}
#endif
