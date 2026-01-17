#if UNITY_EDITOR
using AI.Steering;
using Game;
using UnityEngine;
using AI;
namespace Ships.Control

{
    public partial class AICommander
    {
        private void OnDrawGizmos()
        {
            var waypoint = Navigator?.CurrentWaypoint ?? new Navigator.Waypoint { isValid = false };
            if (!waypoint.isValid) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, GamePlane.PlanePointToWorld(waypoint.position));
        }
    }
}
#endif
