using AI.Observation;
using AI.Scanning;
using Combat;
using Combat.Projectile;
using Game;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEngine;
using Utils;

namespace Tests.EditMode
{
    /// <summary>
    /// Pins the PR-S2 observation contract: the extractor emits one token per entity and rotates
    /// everything into the observer's egocentric frame, and the new threat scanner classifies and
    /// self-filters in-flight missiles.
    /// </summary>
    [Category("AI")]
    public class TacticalObservationEditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            if (GamePlane.IsConfigured) GamePlane.Reset();
            GamePlane.Configure(PlaneAxis.Y, Vector3.zero);
        }

        [TearDown]
        public void TearDown() => GamePlane.Reset();

        private static FakeShipStatus Self(Vector2 pos, Vector2 vel, float yaw) => new()
        {
            Kinematics = new Kinematics(pos, vel, yaw, 0f, 0f),
            Dynamics = Dynamics.Default,
            HealthPct = 0.8f,
            ShieldPct = 0.5f,
            MaxSpeed = 20f,
            MaxYawRate = 10f,
        };

        [Test]
        public void Populate_EmitsOneTokenPerEntity_InEgoFrame()
        {
            var self = Self(Vector2.zero, Vector2.zero, yaw: 0f);
            var target = new TargetView(true, new Vector2(0f, 10f), Vector2.zero,
                new Vector2(0f, -1f), healthPct: 0.6f, shieldPct: 0.3f);
            var threats = new[] { new ThreatContact(new Vector2(5f, 0f), Vector2.zero, ThreatKind.Missile) };
            var obstacles = new ObstacleScan(
                new[] { new DetectedObstacle(new Vector3(0f, 0f, -10f), 3f, null) }, 1);

            var obs = new TacticalObservation(8, 16);
            ObservationExtractor.Populate(obs, self, target, threats, 1, obstacles, time: 1.5f);

            Assert.IsTrue(obs.hasTarget);
            Assert.AreEqual(1, obs.threatCount);
            Assert.AreEqual(1, obs.obstacleCount);

            Assert.AreEqual(0f, obs.target.relPosition.x, 1e-3f);
            Assert.AreEqual(10f, obs.target.relPosition.y, 1e-3f);
            Assert.AreEqual(10f, obs.target.distance, 1e-3f);
            Assert.AreEqual(0.6f, obs.target.healthPct, 1e-4f);

            Assert.AreEqual(5f, obs.threats[0].relPosition.x, 1e-3f);
            Assert.AreEqual(0f, obs.threats[0].relPosition.y, 1e-3f);

            Assert.AreEqual(0f, obs.obstacles[0].relPosition.x, 1e-3f);
            Assert.AreEqual(-10f, obs.obstacles[0].relPosition.y, 1e-3f);
            Assert.AreEqual(3f, obs.obstacles[0].radius, 1e-3f);

            Assert.AreEqual(0.8f, obs.self.healthPct, 1e-4f);
            Assert.AreEqual(0f, obs.self.speedPct, 1e-4f);
        }

        [Test]
        public void Populate_RotatesPositionsAndVelocities_IntoEgoFrame()
        {
            var self = Self(Vector2.zero, new Vector2(0f, 5f), yaw: 90f);
            var target = new TargetView(true, new Vector2(0f, 10f), Vector2.zero,
                new Vector2(0f, 1f), 1f, 1f);

            var obs = new TacticalObservation(8, 16);
            ObservationExtractor.Populate(obs, self, target,
                System.Array.Empty<ThreatContact>(), 0, default, time: 0f);

            Assert.AreEqual(10f, obs.target.relPosition.x, 1e-3f);
            Assert.AreEqual(0f, obs.target.relPosition.y, 1e-3f);

            Assert.AreEqual(5f, obs.self.velocity.x, 1e-3f);
            Assert.AreEqual(0f, obs.self.velocity.y, 1e-3f);
        }

        [Test]
        public void Populate_TargetRelativeVelocity_IsTargetMinusSelf()
        {
            var self = Self(Vector2.zero, new Vector2(0f, 2f), yaw: 0f);
            var target = new TargetView(true, new Vector2(0f, 10f), new Vector2(0f, -3f),
                new Vector2(0f, -1f), 1f, 1f);

            var obs = new TacticalObservation(8, 16);
            ObservationExtractor.Populate(obs, self, target,
                System.Array.Empty<ThreatContact>(), 0, default, time: 0f);

            Assert.AreEqual(0f, obs.target.relVelocity.x, 1e-3f);
            Assert.AreEqual(-5f, obs.target.relVelocity.y, 1e-3f);
        }

        [Test]
        public void Populate_MultiLobeObstacle_EmitsOneTokenPerLobe()
        {
            var self = Self(Vector2.zero, Vector2.zero, yaw: 0f);
            var obstacle = new DetectedObstacle(Vector3.zero, 5f, null,
                new DetectedObstacle.PlaneCircle(new Vector2(2f, 0f), 1f),
                new DetectedObstacle.PlaneCircle(new Vector2(-2f, 0f), 1f),
                default, lobeCount: 2);
            var obstacles = new ObstacleScan(new[] { obstacle }, 1);

            var obs = new TacticalObservation(8, 16);
            ObservationExtractor.Populate(obs, self, TargetView.None,
                System.Array.Empty<ThreatContact>(), 0, obstacles, time: 0f);

            Assert.IsFalse(obs.hasTarget);
            Assert.AreEqual(2, obs.obstacleCount);
            Assert.AreEqual(2f, obs.obstacles[0].relPosition.x, 1e-3f);
            Assert.AreEqual(-2f, obs.obstacles[1].relPosition.x, 1e-3f);
        }

        [Test]
        public void ThreatScanner_DetectsMissileOnProjectileLayer()
        {
            var origin = new GameObject("origin");
            var missile = CreateMissile(new Vector3(5f, 0f, 0f));
            Physics.SyncTransforms();

            var scanner = new ThreatScanner(origin.transform, origin.transform, radius: 20f);
            scanner.Scan();

            Assert.AreEqual(1, scanner.Count);
            Assert.AreEqual(ThreatKind.Missile, scanner.Buffer[0].kind);
            Assert.AreEqual(5f, scanner.Buffer[0].planePos.x, 1e-3f);
            Assert.AreEqual(0f, scanner.Buffer[0].planePos.y, 1e-3f);

            Object.DestroyImmediate(missile.gameObject);
            Object.DestroyImmediate(origin);
        }

        [Test]
        public void ThreatScanner_ExcludesOwnMissile()
        {
            var origin = new GameObject("origin");
            var shooter = new GameObject("shooter").AddComponent<FakeShooter>();
            shooter.transform.SetParent(origin.transform);
            var missile = CreateMissile(new Vector3(5f, 0f, 0f));
            missile.Initialize(shooter);
            Physics.SyncTransforms();

            var scanner = new ThreatScanner(origin.transform, origin.transform, radius: 20f);
            scanner.Scan();

            Assert.AreEqual(0, scanner.Count);

            Object.DestroyImmediate(missile.gameObject);
            Object.DestroyImmediate(origin);
        }

        private static Missile CreateMissile(Vector3 worldPos)
        {
            var go = new GameObject("missile") { layer = LayerIds.Projectile };
            go.transform.position = worldPos;
            go.AddComponent<Rigidbody>();
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1f;
            return go.AddComponent<Missile>();
        }

        private sealed class FakeShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
        }

        private sealed class FakeShipStatus : IShipStatus
        {
            public ShipId Id { get; set; }
            public Transform Transform { get; set; }
            public Kinematics Kinematics { get; set; }
            public Dynamics Dynamics { get; set; }
            public float HealthPct { get; set; }
            public float ShieldPct { get; set; }
            public bool BoostAvailable { get; set; }
            public float BoostCooldownRemaining { get; set; }
            public float MaxSpeed { get; set; }
            public float MaxYawRate { get; set; }
        }
    }
}
