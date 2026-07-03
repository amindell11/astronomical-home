#if UNITY_EDITOR
using UnityEngine;

namespace Asteroids.Fields
{
    public partial class AsteroidField
    {
        protected virtual void OnDrawGizmosSelected()
        {
            if (!settings) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, settings.fieldRadius);
        }
    }
}
#endif
