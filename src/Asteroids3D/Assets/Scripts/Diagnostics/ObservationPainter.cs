using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AI;
using AI.Observation;
using AI.Scanning;
using Ships;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Markers landing on the real entities proves the egocentric extraction round-trips.</summary>
    public sealed class ObservationPainter : IDiagnosticPainter
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

        private static readonly ConditionalWeakTable<AICommander, ThreatScanner> Scanners = new();
        private static readonly TacticalObservation Snapshot = new();

        private readonly List<AICommander> commanders = new();

        public ObservationPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.Observation;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var commander in commanders) Draw(canvas, commander);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var commander = ship.GetComponentInChildren<AICommander>();
            if (commander) commanders.Add(commander);
        }

        public static void Draw(IDiagnosticCanvas canvas, AICommander commander)
        {
            if (commander.context == null || commander.Scout == null) return;

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

            DrawSelf(canvas, frame, kin.pos);
            DrawTarget(canvas, frame, kin.pos);
            DrawThreats(canvas, frame);
            DrawObstacles(canvas, frame);
        }

        private static void DrawSelf(IDiagnosticCanvas canvas, EgoFrame frame, Vector2 pos)
        {
            canvas.Line(pos, pos + frame.PlaneDirection(Vector2.up) * SelfForwardLength, Color.green);
            canvas.Line(pos, pos + frame.PlaneDirection(Snapshot.self.velocity) * SelfVelocityScale, SelfVelocity);
        }

        private static void DrawTarget(IDiagnosticCanvas canvas, EgoFrame frame, Vector2 pos)
        {
            if (!Snapshot.hasTarget) return;
            var t = Snapshot.target;
            var target = frame.ToPlane(t.relPosition);

            var color = Color.Lerp(Color.red, Color.green, t.healthPct);
            canvas.Line(pos, target, color);
            canvas.Ring(target, TargetRingRadius, color);
            canvas.Line(target, target + frame.PlaneDirection(t.facing) * TargetFacingLength, TargetFacing);
        }

        private static void DrawThreats(IDiagnosticCanvas canvas, EgoFrame frame)
        {
            foreach (var threat in Snapshot.threats)
            {
                var at = frame.ToPlane(threat.relPosition);
                canvas.Ring(at, ThreatRingRadius, Color.red);
                canvas.Line(at, at + frame.PlaneDirection(threat.relVelocity) * ThreatVelocityScale, Color.red);
            }
        }

        private static void DrawObstacles(IDiagnosticCanvas canvas, EgoFrame frame)
        {
            foreach (var obstacle in Snapshot.obstacles)
                canvas.Ring(frame.ToPlane(obstacle.relPosition), obstacle.radius, Obstacle);
        }
    }
}
