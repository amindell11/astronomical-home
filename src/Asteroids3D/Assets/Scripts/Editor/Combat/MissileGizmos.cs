using Game;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Combat.Projectile
{
    /// <summary>One missile's flight state in plane space: body ring, explosion radius, velocity ray, target line, and travelled distance.</summary>
    [InitializeOnLoad]
    internal static class MissileGizmos
    {
        static MissileGizmos() =>
            GizmoView.Register(typeof(Missile), "flight", "Missile Flight",
                "body ring, explosion radius, velocity ray, target line + distance", "Combat");

        private const float BodyRingRadius = 0.5f;
        private const float VelocityRayLength = 2f;
        private const float LabelOffset = 1f;
        private const int LabelFontSize = 9;

        private static readonly Color ExplosionRing = new(1f, 0f, 0f, 0.3f);
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Missile))]
        private static void Draw(Missile missile, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(Missile), "flight") || !GizmoView.InScope(missile)) return;
            var pos = GamePlane.WorldPointToPlane(missile.transform.position);
            Ring(pos, BodyRingRadius, missile.target ? Color.red : Color.yellow);
            Ring(pos, missile.explosionRadius, ExplosionRing);

            var velocity = missile.rb ? GamePlane.WorldDirToPlane(missile.rb.linearVelocity) : Vector2.zero;
            if (velocity.sqrMagnitude > 0.01f)
                Line(pos, pos + velocity.normalized * VelocityRayLength, Color.cyan);

            if (missile.target) Line(pos, GamePlane.WorldPointToPlane(missile.target.position), Color.green);

            // Travelled distance is measured from the launch point, which only exists once fired.
            if (!Application.isPlaying) return;
            Handles.Label(GamePlane.PlanePointToWorld(pos + new Vector2(0f, LabelOffset)),
                $"Dist: {missile.DistanceTraveled:F1}/{missile.MaxDistance:F1}",
                new GUIStyle { normal = { textColor = Color.white }, fontSize = LabelFontSize });
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
