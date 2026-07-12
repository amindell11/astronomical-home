using AI.Debug;
using Ships.Command;
using UnityEditor;
using UnityEngine;

namespace AI
{
    internal static class GunnerGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Gunner))]
        private static void Draw(Gunner gunner, GizmoType gizmoType)
        {
            if (!AIDebugContext.ShouldDraw(AIDebugChannel.Targeting, gizmoType)) return;

            DrawTargeting(gunner);
            DrawLineOfSight(gunner);
        }

        private static void DrawTargeting(Gunner gunner)
        {
            if (!gunner.HasTarget) return;

            var pos = gunner.transform.position;
            var targetPos = gunner.Target;

            Gizmos.color = Color.gray;
            Gizmos.DrawLine(pos, targetPos);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(targetPos, Vector3.one * 2f);

            var dirToTarget = (targetPos - pos).normalized;
            Gizmos.DrawRay(pos, dirToTarget * 5f);
        }

        private static void DrawLineOfSight(Gunner gunner)
        {
            var sight = gunner.weapons?.Sight(WeaponSlot.Primary);
            if (!gunner.HasTarget || sight == null) return;

            var firePos = sight.FirePoint;
            var targetPos = gunner.Target;

            var hasLOS = Combat.TargetingMath.IsLineClear(firePos, targetPos);
            Gizmos.color = hasLOS ? Color.green : Color.red;
            Gizmos.DrawLine(firePos, targetPos);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePos, 0.5f);
        }
    }
}
