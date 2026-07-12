using Game;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Player
{
    internal static class PlayerCommanderGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(PlayerCommander))]
        private static void Draw(PlayerCommander commander, GizmoType gizmoType)
        {
            if (!commander.showMouseGizmos || !Application.isPlaying ||
                !commander.useMouseDirection || !commander.playerInput.WantsToRotate) return;

            var position = commander.transform.position;
            var scale = commander.mouseGizmoScale;

            SuperGizmos.DrawArrow(position, commander.directionToMouse,
                SuperGizmos.HeadType.Sphere, 0.1f * scale, Color.red, scale);

            SuperGizmos.DrawArrow(position, commander.projectedDirection,
                SuperGizmos.HeadType.Cube, 0.08f * scale, Color.orange, scale);

            SuperGizmos.DrawArrow(position, GamePlane.Normal,
                SuperGizmos.HeadType.Cube, 0.05f * scale, Color.blue, scale);

            SuperGizmos.DrawArrow(position, GamePlane.Forward,
                SuperGizmos.HeadType.Cube, 0.06f * scale, Color.green, scale);
        }
    }
}
