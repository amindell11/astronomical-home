using Game;
using Utils;
using UnityEngine;

namespace Player
{
    public partial class PlayerCommander
    {
        private void OnDrawGizmos()
        {
            if (!showMouseGizmos || !Application.isPlaying || !useMouseDirection || ! PlayerInputReader.WantsToRotate) return;
        
            var position = transform.position;
            var scale = mouseGizmoScale;

            SuperGizmos.DrawArrow(position, directionToMouse, 
                SuperGizmos.HeadType.Sphere, 0.1f * scale, Color.red, scale);
        
            SuperGizmos.DrawArrow(position, projectedDirection, 
                SuperGizmos.HeadType.Cube, 0.08f * scale, Color.orange, scale);
        
            SuperGizmos.DrawArrow(position, GamePlane.Normal, 
                SuperGizmos.HeadType.Cube, 0.05f * scale, Color.blue, scale);
        
            SuperGizmos.DrawArrow(position, GamePlane.Forward, 
                SuperGizmos.HeadType.Cube, 0.06f * scale, Color.green, scale);
        }
    }
}
