using Game;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Combat.Projectiles
{
    /// <summary>A live projectile's heading as a trail drawn back from its head, so a bolt reads as motion in a still frame.</summary>
    [InitializeOnLoad]
    internal static class ProjectileGizmos
    {
        static ProjectileGizmos() =>
            GizmoView.Register(typeof(ProjectileBase), "trail", "Bolt Trail",
                "yellow heading trail behind the bolt", "Combat");

        private const float TrailLength = 2.5f;
        private const float MinSpeedSqr = 1e-4f;

        private static readonly Color BoltColor = new(1f, 0.95f, 0.2f);

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(ProjectileBase))]
        private static void Draw(ProjectileBase projectile, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(ProjectileBase), "trail") || !GizmoView.InScope(projectile)) return;
            var worldDir = projectile.rb && projectile.rb.linearVelocity.sqrMagnitude > MinSpeedSqr
                ? projectile.rb.linearVelocity
                : projectile.transform.up;
            var dir = GamePlane.WorldDirToPlane(worldDir);
            if (dir.sqrMagnitude < 1e-8f) return;

            var head = GamePlane.WorldPointToPlane(projectile.transform.position);
            Gizmos.color = BoltColor;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(head - dir.normalized * TrailLength),
                GamePlane.PlanePointToWorld(head));
        }
    }
}
