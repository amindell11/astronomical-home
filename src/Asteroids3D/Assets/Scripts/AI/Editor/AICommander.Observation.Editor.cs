#if UNITY_EDITOR
using AI.Debug;
using AI.Observation;
using AI.Scanning;
using Game;
using UnityEngine;

namespace AI
{
    public partial class AICommander
    {
        private const float ObservationThreatRadius = 40f;

        private AIDebugSettings cachedObsSettings;
        private ThreatScanner obsThreatScanner;
        private readonly TacticalObservation obsSnapshot = new();

        private AIDebugSettings ObservationSettings
        {
            get
            {
                if (!cachedObsSettings) cachedObsSettings = DebugSettings;
                return cachedObsSettings;
            }
        }

        void OnDrawGizmos() => DrawObservationGizmos(false);
        void OnDrawGizmosSelected() => DrawObservationGizmos(true);

        private void DrawObservationGizmos(bool isSelected)
        {
            var settings = ObservationSettings;
            if (settings == null || !settings.ShouldDraw(isSelected)) return;
            if (!settings.IsActive(AIDebugChannel.Observation)) return;
            if (!Application.isPlaying || control.Ship == null || Scout == null) return;

            var self = control.Ship;
            obsThreatScanner ??= new ThreatScanner(self.Transform, ObservationThreatRadius);
            obsThreatScanner.Scan();

            var combat = context?.Combat;
            var target = combat != null && combat.HasEnemy
                ? new TargetView(true, combat.EnemyPos, combat.EnemyVel, combat.EnemyForward,
                    combat.EnemyHealthPct, combat.EnemyShieldPct)
                : TargetView.None;

            ObservationExtractor.Populate(obsSnapshot, self, target,
                obsThreatScanner.Contacts, obsThreatScanner.Count, Scout.ObstacleScan, Time.time);

            // Render from the ego-frame tokens, reconstructed back to world: if the markers land on
            // the real entities, the egocentric extraction round-trips correctly.
            var kin = self.Kinematics;
            var frame = new EgoFrame(kin.pos, kin.Forward);
            var shipWorld = GamePlane.PlanePointToWorld(kin.pos);

            DrawSelf(frame, shipWorld);
            DrawTarget(frame, shipWorld);
            DrawThreats(frame);
            DrawObstacles(frame);
        }

        private void DrawSelf(EgoFrame frame, Vector3 shipWorld)
        {
            Gizmos.color = Color.green;
            var fwd = GamePlane.PlaneDirToWorld(frame.PlaneDirection(Vector2.up));
            Gizmos.DrawLine(shipWorld, shipWorld + fwd * 3f);

            Gizmos.color = new Color(0f, 1f, 1f, 0.7f);
            var vel = GamePlane.PlaneDirToWorld(frame.PlaneDirection(obsSnapshot.self.velocity));
            Gizmos.DrawLine(shipWorld, shipWorld + vel * 0.5f);
        }

        private void DrawTarget(EgoFrame frame, Vector3 shipWorld)
        {
            if (!obsSnapshot.hasTarget) return;
            var t = obsSnapshot.target;
            var world = GamePlane.PlanePointToWorld(frame.ToPlane(t.relPosition));

            Gizmos.color = Color.Lerp(Color.red, Color.green, t.healthPct);
            Gizmos.DrawLine(shipWorld, world);
            Gizmos.DrawWireSphere(world, 1.5f);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
            var facing = GamePlane.PlaneDirToWorld(frame.PlaneDirection(t.facing));
            Gizmos.DrawLine(world, world + facing * 3f);
        }

        private void DrawThreats(EgoFrame frame)
        {
            Gizmos.color = Color.red;
            foreach (var th in obsSnapshot.threats)
            {
                var world = GamePlane.PlanePointToWorld(frame.ToPlane(th.relPosition));
                Gizmos.DrawWireSphere(world, 0.8f);
                var relVel = GamePlane.PlaneDirToWorld(frame.PlaneDirection(th.relVelocity));
                Gizmos.DrawLine(world, world + relVel * 0.4f);
            }
        }

        private void DrawObstacles(EgoFrame frame)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            foreach (var ob in obsSnapshot.obstacles)
            {
                var world = GamePlane.PlanePointToWorld(frame.ToPlane(ob.relPosition));
                Gizmos.DrawWireSphere(world, ob.radius);
            }
        }
    }
}
#endif
