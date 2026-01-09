#if UNITY_EDITOR
using EnemyAI;
using Game;
using UnityEngine;

namespace Ships.Control
{
    public partial class AI
    {
        void OnDrawGizmos()
        {
            var waypoint = navigator?.CurrentWaypoint ?? new AINavigator.Waypoint { isValid = false };
            if (waypoint.isValid)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, GamePlane.PlanePointToWorld(waypoint.position));
            }
        }
    }
}
#endif
