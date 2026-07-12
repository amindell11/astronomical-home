using System.Runtime.CompilerServices;
using AI.Debug;
using AI.Observation;
using AI.Scanning;
using Game;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Renders the tactical-observation tokens reconstructed back to world space: if the
    /// markers land on the real entities, the egocentric extraction round-trips correctly.</summary>
    internal static class AICommanderGizmos
    {
        private const float ThreatRadius = 40f;

        private static readonly ConditionalWeakTable<AICommander, ThreatScanner> Scanners = new();
        private static readonly TacticalObservation Snapshot = new();

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!AIDebugContext.ShouldDraw(AIDebugChannel.Observation, gizmoType)) return;
            if (!Application.isPlaying || commander.context == null || commander.Scout == null) return;

            var self = commander.context.Self;
            var scanner = Scanners.GetValue(commander,
                c => new ThreatScanner(c.context.Self.Transform, ThreatRadius));
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
            var shipWorld = GamePlane.PlanePointToWorld(kin.pos);

            DrawSelf(frame, shipWorld);
            DrawTarget(frame, shipWorld);
            DrawThreats(frame);
            DrawObstacles(frame);
        }

        private static void DrawSelf(EgoFrame frame, Vector3 shipWorld)
        {
            Gizmos.color = Color.green;
            var fwd = GamePlane.PlaneDirToWorld(frame.PlaneDirection(Vector2.up));
            Gizmos.DrawLine(shipWorld, shipWorld + fwd * 3f);

            Gizmos.color = new Color(0f, 1f, 1f, 0.7f);
            var vel = GamePlane.PlaneDirToWorld(frame.PlaneDirection(Snapshot.self.velocity));
            Gizmos.DrawLine(shipWorld, shipWorld + vel * 0.5f);
        }

        private static void DrawTarget(EgoFrame frame, Vector3 shipWorld)
        {
            if (!Snapshot.hasTarget) return;
            var t = Snapshot.target;
            var world = GamePlane.PlanePointToWorld(frame.ToPlane(t.relPosition));

            Gizmos.color = Color.Lerp(Color.red, Color.green, t.healthPct);
            Gizmos.DrawLine(shipWorld, world);
            Gizmos.DrawWireSphere(world, 1.5f);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
            var facing = GamePlane.PlaneDirToWorld(frame.PlaneDirection(t.facing));
            Gizmos.DrawLine(world, world + facing * 3f);
        }

        private static void DrawThreats(EgoFrame frame)
        {
            Gizmos.color = Color.red;
            foreach (var th in Snapshot.threats)
            {
                var world = GamePlane.PlanePointToWorld(frame.ToPlane(th.relPosition));
                Gizmos.DrawWireSphere(world, 0.8f);
                var relVel = GamePlane.PlaneDirToWorld(frame.PlaneDirection(th.relVelocity));
                Gizmos.DrawLine(world, world + relVel * 0.4f);
            }
        }

        private static void DrawObstacles(EgoFrame frame)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            foreach (var ob in Snapshot.obstacles)
            {
                var world = GamePlane.PlanePointToWorld(frame.ToPlane(ob.relPosition));
                Gizmos.DrawWireSphere(world, ob.radius);
            }
        }
    }
}
