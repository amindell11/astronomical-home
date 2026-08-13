using Combat;
using Game;
using Ships.Command;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Where the gunner is aiming and whether the shot is clear: gunner-to-target line, target marker, aim ray, and fire-point line of sight.</summary>
    internal static class GunnerGizmos
    {
        private const float AimRayLength = 5f;
        private const float FirePointRadius = 0.5f;

        private static readonly Vector2 TargetMarkerSize = new(2f, 2f);
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        [DrawGizmo(GizmoType.Selected, typeof(Gunner))]
        private static void Draw(Gunner gunner, GizmoType gizmoType)
        {
            if (!Application.isPlaying || !gunner.HasTarget) return;

            var target = GamePlane.WorldPointToPlane(gunner.Target);
            DrawTargeting(GamePlane.WorldPointToPlane(gunner.transform.position), target);
            DrawLineOfSight(gunner, target);
        }

        private static void DrawTargeting(Vector2 pos, Vector2 target)
        {
            Line(pos, target, Color.gray);
            Rect(target, TargetMarkerSize, Color.red);

            var toTarget = target - pos;
            if (toTarget.sqrMagnitude < 1e-8f) return;
            Line(pos, pos + toTarget.normalized * AimRayLength, Color.red);
        }

        private static void DrawLineOfSight(Gunner gunner, Vector2 target)
        {
            var sight = gunner.weapons?.Sight(WeaponSlot.Primary);
            if (sight == null) return;

            var firePos = sight.FirePoint;
            var firePlane = GamePlane.WorldPointToPlane(firePos);
            Line(firePlane, target, TargetingMath.IsLineClear(firePos, gunner.Target) ? Color.green : Color.red);
            Ring(firePlane, FirePointRadius, Color.cyan);
        }

        private static void Rect(Vector2 center, Vector2 size, Color color)
        {
            var half = size * 0.5f;
            var bl = center + new Vector2(-half.x, -half.y);
            var br = center + new Vector2(half.x, -half.y);
            var tr = center + new Vector2(half.x, half.y);
            var tl = center + new Vector2(-half.x, half.y);
            Line(bl, br, color);
            Line(br, tr, color);
            Line(tr, tl, color);
            Line(tl, bl, color);
        }

        private static void Ring(Vector2 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius);
        }

        private static void Line(Vector2 a, Vector2 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
        }
    }
}
