using Game;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Player
{
    [InitializeOnLoad]
    internal static class PlayerCommanderGizmos
    {
        static PlayerCommanderGizmos() =>
            GizmoView.Register(typeof(PlayerCommander), "mouse", "Mouse Aim",
                "red/orange/blue/green facing + plane arrows", "Steering");

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(PlayerCommander))]
        private static void Draw(PlayerCommander commander, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(PlayerCommander), "mouse") || !GizmoView.InScope(commander) ||
                !Application.isPlaying || !commander.useMouseDirection || !commander.playerInput.WantsToRotate) return;

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
