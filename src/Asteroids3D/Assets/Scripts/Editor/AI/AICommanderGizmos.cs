using System.Runtime.CompilerServices;
using AI.Observation;
using AI.Scanning;
using Game;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Markers landing on the real entities proves the egocentric extraction round-trips.</summary>
    internal static class AICommanderGizmos
    {
        private const float ThreatScanRadius = 40f;
        private const float SelfForwardLength = 3f;
        private const float SelfVelocityScale = 0.5f;
        private const float TargetRingRadius = 1.5f;
        private const float TargetFacingLength = 3f;
        private const float ThreatRingRadius = 0.8f;
        private const float ThreatVelocityScale = 0.4f;

        private static readonly Color SelfVelocity = new(0f, 1f, 1f, 0.7f);
        private static readonly Color TargetFacing = new(1f, 0.4f, 0f, 0.9f);
        private static readonly Color Obstacle = new(1f, 1f, 1f, 0.25f);
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        private static readonly ConditionalWeakTable<AICommander, ThreatScanner> Scanners = new();
        private static readonly TacticalObservation Snapshot = new();

        [DrawGizmo(GizmoType.Selected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!Application.isPlaying || commander.context == null || commander.Scout == null) return;

            var self = commander.context.Self;
            var scanner = Scanners.GetValue(commander,
                c => new ThreatScanner(c.context.Self.Transform, ThreatScanRadius));
            scanner.Scan();

            var combat = commander.context.Combat;
            var target = combat != null && combat.HasEnemy
                ? new TargetView(true, combat.EnemyPos, combat.EnemyVel, combat.EnemyForward,
                    combat.EnemyHealthPct, combat.EnemyShieldPct)
                : TargetView.None;

            ObservationExtractor.Populate(Snapshot, self, target,
                scanner.Contacts, scanner.Count, commander.Scout.ObstacleScan, Time.time);

            var kin = self.Kinematics;
            var frame = new EgoFrame(kin.pos, kin.Forward);

            DrawSelf(frame, kin.pos);
            DrawTarget(frame, kin.pos);
            DrawThreats(frame);
            DrawObstacles(frame);
        }

        private static void DrawSelf(EgoFrame frame, Vector2 pos)
        {
            Line(pos, pos + frame.PlaneDirection(Vector2.up) * SelfForwardLength, Color.green);
            Line(pos, pos + frame.PlaneDirection(Snapshot.self.velocity) * SelfVelocityScale, SelfVelocity);
        }

        private static void DrawTarget(EgoFrame frame, Vector2 pos)
        {
            if (!Snapshot.hasTarget) return;
            var t = Snapshot.target;
            var target = frame.ToPlane(t.relPosition);

            var color = Color.Lerp(Color.red, Color.green, t.healthPct);
            Line(pos, target, color);
            Ring(target, TargetRingRadius, color);
            Line(target, target + frame.PlaneDirection(t.facing) * TargetFacingLength, TargetFacing);
        }

        private static void DrawThreats(EgoFrame frame)
        {
            foreach (var threat in Snapshot.threats)
            {
                var at = frame.ToPlane(threat.relPosition);
                Ring(at, ThreatRingRadius, Color.red);
                Line(at, at + frame.PlaneDirection(threat.relVelocity) * ThreatVelocityScale, Color.red);
            }
        }

        private static void DrawObstacles(EgoFrame frame)
        {
            foreach (var obstacle in Snapshot.obstacles)
                Ring(frame.ToPlane(obstacle.relPosition), obstacle.radius, Obstacle);
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
