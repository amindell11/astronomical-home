#if UNITY_EDITOR
using Game;
using UnityEngine;
using Utils;

namespace Ships.Movement
{
    public partial class Controller
    {
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showMovementGizmos) return;

            var pos = transform.position;
            float scale = movementGizmoScale;

            SuperGizmos.DrawArrow(pos, GamePlane.PlaneDirToWorld(flightComputer.Outputs.Thrust), 
                SuperGizmos.HeadType.Sphere, 0.15f, Color.yellow, scale);

            SuperGizmos.DrawArrow(pos, GamePlane.PlaneDirToWorld(flightComputer.Outputs.Strafe), 
                SuperGizmos.HeadType.Cube, 0.25f, Color.yellow, scale);
        }
    }
}
#endif
