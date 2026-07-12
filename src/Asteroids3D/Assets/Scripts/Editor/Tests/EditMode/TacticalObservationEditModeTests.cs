using AI.Observation;
using AI.Scanning;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEngine;
using Utils;

namespace Tests.EditMode
{
    /// <summary>
    /// Pins the egocentric, target-relative observation extraction (roadmap PR-S2). The extractor is
    /// pure over its inputs, so every geometric guarantee — frame round-trip, target/threat mapping,
    /// obstacle-lobe expansion — is checked here without physics. The <see cref="ThreatScanner"/>
    /// sweep and the gizmo rendering are validated live in the editor, not here.
    /// </summary>
    [Category("AI")]
    public class TacticalObservationEditModeTests
    {
        private const float Eps = 1e-3f;

        private sealed class FakeShip : IShipStatus
        {
            public Kinematics Kinematics { get; set; }
            public ShipId Id => default;
            public Transform Transform => null;
            public Dynamics Dynamics { get; set; }
            public float HealthPct { get; set; } = 1f;
            public float ShieldPct { get; set; } = 1f;
            public bool BoostAvailable { get; set; } = true;
            public float BoostCooldownRemaining { get; set; }
            public float MaxSpeed { get; set; } = 10f;
            public float MaxYawRate { get; set; } = 90f;
        }

        private static Kinematics Pose(Vector2 pos, Vector2 vel, float yaw = 0f, float yawRate = 0f) =>
            new(pos, vel, yaw, yawRate, 0f);

        private static FakeShip ShipAt(Vector2 pos, Vector2 vel = default, float yaw = 0f, float yawRate = 0f) =>
            new() { Kinematics = Pose(pos, vel, yaw, yawRate) };

        // ── EgoFrame round-trips ──────────────────────────────────────────────────

        [Test]
        public void EgoFrame_PointToPlane_RoundTrips()
        {
            var frame = new EgoFrame(new Vector2(3f, -2f), new Vector2(0.6f, 0.8f));
            var ego = new Vector2(1.25f, -4f);
            var back = frame.Point(frame.ToPlane(ego));
            Assert.That(back.x, Is.EqualTo(ego.x).Within(Eps));
            Assert.That(back.y, Is.EqualTo(ego.y).Within(Eps));
        }

        [Test]
        public void EgoFrame_DirectionPlaneDirection_RoundTrips()
        {
            var frame = new EgoFrame(new Vector2(3f, -2f), new Vector2(-1f, 0f));
            var ego = new Vector2(2f, 5f);
            var back = frame.Direction(frame.PlaneDirection(ego));
            Assert.That(back.x, Is.EqualTo(ego.x).Within(Eps));
            Assert.That(back.y, Is.EqualTo(ego.y).Within(Eps));
        }

        // ── Target mapping ────────────────────────────────────────────────────────

        [Test]
        public void Target_Ahead_MapsToPositiveForward()
        {
            var self = ShipAt(Vector2.zero); // facing +Y
            var target = new TargetView(true, new Vector2(0f, 5f), Vector2.zero, Vector2.up, 1f, 1f);
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, target, null, 0, default, 0f);

            Assert.IsTrue(obs.hasTarget);
            Assert.That(obs.target.relPosition.x, Is.EqualTo(0f).Within(Eps));
            Assert.That(obs.target.relPosition.y, Is.EqualTo(5f).Within(Eps));
            Assert.That(obs.target.distance, Is.EqualTo(5f).Within(Eps));
        }

        [Test]
        public void Target_ToTheRight_MapsToPositiveRight()
        {
            var self = ShipAt(Vector2.zero); // facing +Y → right = +X
            var target = new TargetView(true, new Vector2(4f, 0f), Vector2.zero, Vector2.up, 1f, 1f);
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, target, null, 0, default, 0f);

            Assert.That(obs.target.relPosition.x, Is.EqualTo(4f).Within(Eps));
            Assert.That(obs.target.relPosition.y, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void Target_RotatedSelf_ProjectsIntoEgoFrame()
        {
            var self = ShipAt(Vector2.zero, yaw: -90f); // Forward = (1, 0)
            var target = new TargetView(true, new Vector2(5f, 0f), Vector2.zero, Vector2.up, 1f, 1f);
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, target, null, 0, default, 0f);

            // 5 units along world +X, which is now straight ahead → ego (0, 5).
            Assert.That(obs.target.relPosition.x, Is.EqualTo(0f).Within(Eps));
            Assert.That(obs.target.relPosition.y, Is.EqualTo(5f).Within(Eps));
        }

        [Test]
        public void Target_RelativeVelocity_IsEnemyMinusSelf()
        {
            var self = ShipAt(Vector2.zero, vel: new Vector2(0f, 2f)); // facing +Y
            var target = new TargetView(true, new Vector2(0f, 8f), new Vector2(0f, 5f), Vector2.up, 1f, 1f);
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, target, null, 0, default, 0f);

            Assert.That(obs.target.relVelocity.x, Is.EqualTo(0f).Within(Eps));
            Assert.That(obs.target.relVelocity.y, Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void NoTarget_LeavesHasTargetFalse()
        {
            var obs = new TacticalObservation();
            ObservationExtractor.Populate(obs, ShipAt(Vector2.zero), TargetView.None, null, 0, default, 0f);
            Assert.IsFalse(obs.hasTarget);
        }

        // ── Self resources ────────────────────────────────────────────────────────

        [Test]
        public void Self_SpeedAndYawRate_NormalizeAndClamp()
        {
            var self = ShipAt(Vector2.zero, vel: new Vector2(0f, 5f), yawRate: 45f);
            self.MaxSpeed = 10f;
            self.MaxYawRate = 90f;
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, TargetView.None, null, 0, default, 0f);

            Assert.That(obs.self.speedPct, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(obs.self.yawRatePct, Is.EqualTo(0.5f).Within(Eps));

            self.Kinematics = Pose(Vector2.zero, Vector2.zero, 0f, -180f); // beyond max → clamps to -1
            ObservationExtractor.Populate(obs, self, TargetView.None, null, 0, default, 0f);
            Assert.That(obs.self.yawRatePct, Is.EqualTo(-1f).Within(Eps));
        }

        // ── Threats ───────────────────────────────────────────────────────────────

        [Test]
        public void Threats_PopulateWithEgoKinematics()
        {
            var self = ShipAt(Vector2.zero, vel: new Vector2(0f, 1f)); // facing +Y
            var threats = new[]
            {
                new ThreatContact(new Vector2(0f, 10f), new Vector2(0f, -4f), ThreatKind.Missile),
            };
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, TargetView.None, threats, 1, default, 0f);

            Assert.That(obs.threats.Count, Is.EqualTo(1));
            var th = obs.threats[0];
            Assert.That(th.relPosition.y, Is.EqualTo(10f).Within(Eps));
            Assert.That(th.distance, Is.EqualTo(10f).Within(Eps));
            Assert.That(th.relVelocity.y, Is.EqualTo(-5f).Within(Eps)); // (-4) - (1)
            Assert.That(th.kind, Is.EqualTo(ThreatKind.Missile));
        }

        [Test]
        public void Threats_RespectCount_IgnoringTrailingBuffer()
        {
            var self = ShipAt(Vector2.zero);
            var threats = new[]
            {
                new ThreatContact(new Vector2(0f, 3f), Vector2.zero, ThreatKind.Missile),
                new ThreatContact(new Vector2(0f, 9f), Vector2.zero, ThreatKind.Missile),
            };
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, TargetView.None, threats, 1, default, 0f);

            Assert.That(obs.threats.Count, Is.EqualTo(1));
        }

        // ── Obstacles ─────────────────────────────────────────────────────────────

        [Test]
        public void Obstacles_LobedObstacle_EmitsOneTokenPerLobe()
        {
            var self = ShipAt(Vector2.zero);
            var lobed = new DetectedObstacle(
                Vector3.zero, 2f, null,
                new DetectedObstacle.PlaneCircle(new Vector2(0f, 4f), 1f),
                new DetectedObstacle.PlaneCircle(new Vector2(0f, 6f), 1f),
                default, 2);
            var scan = new ObstacleScan(new[] { lobed }, 1);
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, TargetView.None, null, 0, scan, 0f);

            Assert.That(obs.obstacles.Count, Is.EqualTo(2));
            Assert.That(obs.obstacles[0].relPosition.y, Is.EqualTo(4f).Within(Eps));
            Assert.That(obs.obstacles[1].relPosition.y, Is.EqualTo(6f).Within(Eps));
        }

        [Test]
        public void Obstacles_NonLobed_EmitsPrimaryCircle()
        {
            var self = ShipAt(Vector2.zero);
            var plain = new DetectedObstacle(new Vector3(0f, 7f, 0f), 3f, null);
            var scan = new ObstacleScan(new[] { plain }, 1);
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, TargetView.None, null, 0, scan, 0f);

            Assert.That(obs.obstacles.Count, Is.EqualTo(1));
            Assert.That(obs.obstacles[0].relPosition.y, Is.EqualTo(7f).Within(Eps));
            Assert.That(obs.obstacles[0].radius, Is.EqualTo(3f).Within(Eps));
        }

        // ── Reuse ─────────────────────────────────────────────────────────────────

        [Test]
        public void Populate_ClearsPreviousContents()
        {
            var self = ShipAt(Vector2.zero);
            var threats = new[] { new ThreatContact(new Vector2(0f, 3f), Vector2.zero, ThreatKind.Missile) };
            var obs = new TacticalObservation();

            ObservationExtractor.Populate(obs, self, new TargetView(true, new Vector2(0f, 2f), Vector2.zero, Vector2.up, 1f, 1f), threats, 1, default, 0f);
            Assert.That(obs.threats.Count, Is.EqualTo(1));
            Assert.IsTrue(obs.hasTarget);

            ObservationExtractor.Populate(obs, self, TargetView.None, null, 0, default, 0f);
            Assert.That(obs.threats.Count, Is.EqualTo(0));
            Assert.IsFalse(obs.hasTarget);
        }

        // ── ThreatScanner (layer + tag classification) ─────────────────────────────

        [Test]
        public void ThreatScanner_DetectsTaggedMissile_AndIgnoresUntaggedProjectile()
        {
            var origin = new GameObject("origin");
            var missile = MakeProjectile(new Vector3(0f, 5f, 0f), new Vector3(0f, -8f, 0f), TagNames.Missile);
            var laser = MakeProjectile(new Vector3(1f, 4f, 0f), new Vector3(0f, -20f, 0f), "Untagged");
            try
            {
                Physics.SyncTransforms();
                var scanner = new ThreatScanner(origin.transform, 50f);
                scanner.Scan();

                Assert.That(scanner.Count, Is.EqualTo(1), "only the tagged missile counts as a threat");
                var c = scanner.Contacts[0];
                Assert.That(c.kind, Is.EqualTo(ThreatKind.Missile));
                Assert.That(c.planePos.y, Is.EqualTo(5f).Within(0.01f));
                Assert.That(c.planeVel.y, Is.EqualTo(-8f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(origin);
                Object.DestroyImmediate(missile);
                Object.DestroyImmediate(laser);
            }
        }

        private static GameObject MakeProjectile(Vector3 pos, Vector3 vel, string tag)
        {
            var go = new GameObject("projectile") { layer = LayerMask.NameToLayer(TagNames.Projectile), tag = tag };
            go.transform.position = pos;
            go.AddComponent<SphereCollider>().radius = 0.5f;
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = vel;
            return go;
        }
    }
}
