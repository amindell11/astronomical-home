#if UNITY_EDITOR
using AI.Observation;
using AI.Scanning;
using Game.RLHarness;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the pure agent maps: action thresholds/clamps, the one-time ego→world conversion, the 72-float observation layout (24 combat channels + 8 obstacle tokens × 6), and the chooser's one-shot boost semantics.</summary>
    [Category("AI")]
    public class RLAgentEditModeTests
    {
        private const float MaxSpeed = 10f;
        private const float ArenaRadius = 120f;

        private sealed class StubStatus : IShipStatus
        {
            public Kinematics kinematics;
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => kinematics;
            public Dynamics Dynamics => default;
            public float HealthPct => 0.5f;
            public float ShieldPct => 0.25f;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float MaxSpeed => RLAgentEditModeTests.MaxSpeed;
            public float MaxYawRate => 90f;
        }

        [Test]
        public void Map_ClampsVelocityAndThresholdsTriggers()
        {
            var a = AgentActions.Map(2f, -3f, 0.01f, -0.01f);
            Assert.AreEqual(new Vector2(1f, -1f), a.velocityEgo);
            Assert.IsTrue(a.fire);
            Assert.IsFalse(a.boost);

            var atThreshold = AgentActions.Map(0f, 0f, 0f, 0f);
            Assert.IsFalse(atThreshold.fire, "fire gates on strictly positive");
            Assert.IsFalse(atThreshold.boost, "boost gates on strictly positive");
        }

        [Test]
        public void ToWorldVelocity_RotatesEgoIntoThePlaneFrame()
        {
            // Ship facing +X: ego-forward (0,1) → world +X, ego-right (1,0) → world −Y.
            var forward = new Vector2(1f, 0f);
            AssertVector(new Vector2(MaxSpeed, 0f),
                AgentActions.ToWorldVelocity(new Vector2(0f, 1f), forward, MaxSpeed));
            AssertVector(new Vector2(0f, -MaxSpeed),
                AgentActions.ToWorldVelocity(new Vector2(1f, 0f), forward, MaxSpeed));
        }

        [Test]
        public void ToWorldVelocity_ClampsToMaxSpeed()
        {
            var world = AgentActions.ToWorldVelocity(new Vector2(1f, 1f), Vector2.up, MaxSpeed);
            Assert.AreEqual(MaxSpeed, world.magnitude, 1e-4f);
        }

        [Test]
        public void EgoWorldConversion_RoundTrips()
        {
            var forward = new Vector2(Mathf.Cos(0.7f), Mathf.Sin(0.7f));
            var ego = new Vector2(0.3f, -0.4f);
            var roundTripped = AgentActions.ToEgoAction(
                AgentActions.ToWorldVelocity(ego, forward, MaxSpeed), forward, MaxSpeed);
            AssertVector(ego, roundTripped);
        }

        [Test]
        public void Fill_LaysOutAllChannels()
        {
            var self = new StubStatus
            {
                // yaw 0 → forward (0,1), right (1,0): ego == plane axes shifted to pos.
                kinematics = new Kinematics(new Vector2(5f, 5f), new Vector2(3f, 0f), 0f, 45f, 0f),
            };
            var target = new TargetView(true, new Vector2(5f, 15f), new Vector2(3f, 5f),
                new Vector2(0f, -1f), 0.7f, 0.2f);
            var scan = new ObstacleScan(new[]
            {
                new DetectedObstacle(new Vector3(8f, 5f, 0f), 2f, null, new Vector2(1f, 0f)),
            }, 1);
            var buffer = new float[AgentObservations.Size];

            AgentObservations.Fill(buffer, self, in target,
                inMyEnvelope: true, inEnemyEnvelope: false, primaryWeaponReady: true, primaryHeatPct: 0.6f,
                arenaCenterPlane: new Vector2(5f, 65f), arenaRadius: ArenaRadius, in scan);

            var expected = new[]
            {
                0.3f, 0f,            // self velocity ego / MaxSpeed
                0.3f,                // speedPct
                0.5f,                // yawRatePct (45 / 90)
                0.5f, 0.25f,         // health, shield
                1f, 0f,              // boost available, cooldown pct
                1f,                  // hasTarget
                0f, 10f / ArenaRadius,   // target relPosition / R
                10f / ArenaRadius,       // distance / R
                0f, 0.5f,            // relVelocity / MaxSpeed
                0f, -1f,             // target facing (ego)
                0.7f, 0.2f,          // target health, shield
                1f, 0f,              // inMyEnvelope, inEnemyEnvelope
                0f, 60f / ArenaRadius,   // arena center ego / R
                1f,                  // primary weapon ready
                0.6f,                // self primary heat pct
                3f / ArenaRadius, 0f,    // token 0 relPos ego / R
                3f / ArenaRadius,        // token 0 distance / R
                -0.2f, 0f,           // token 0 relVel ego / MaxSpeed
                2f / AgentObservations.SpawnSettingsMaxAsteroidRadius, // token 0 radius
            };
            Assert.AreEqual(AgentObservations.Size, expected.Length + 7 * 6, "8 tokens × 6 floats after channel 23");
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], buffer[i], 1e-4f, $"channel {i}");
            for (var i = expected.Length; i < buffer.Length; i++)
                Assert.AreEqual(0f, buffer[i], $"unused token slots must zero-pad (channel {i})");
        }

        [Test]
        public void Fill_NoTarget_ZeroesTheTargetBlock()
        {
            var self = new StubStatus { kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f) };
            var buffer = new float[AgentObservations.Size];
            AgentObservations.Fill(buffer, self, TargetView.None,
                inMyEnvelope: false, inEnemyEnvelope: false, primaryWeaponReady: false, primaryHeatPct: 0f,
                arenaCenterPlane: Vector2.zero, arenaRadius: ArenaRadius, default);

            for (var i = 8; i < 18; i++)
                Assert.AreEqual(0f, buffer[i], $"hasTarget flag and target block must zero (channel {i})");
            for (var i = 24; i < buffer.Length; i++)
                Assert.AreEqual(0f, buffer[i], $"a default scan must zero the whole token block (channel {i})");
        }

        [Test]
        public void Fill_ObstacleTokens_EgoTransformsSortsByDistanceAndZeroPads()
        {
            var self = new StubStatus
            {
                // yaw 90 → forward (-1,0), right (0,1): a real rotation, not the identity frame.
                kinematics = new Kinematics(Vector2.zero, new Vector2(0f, 2f), 90f, 0f, 0f),
            };
            // Deliberately unsorted: distances 10, 20, 4.
            var scan = new ObstacleScan(new[]
            {
                new DetectedObstacle(new Vector3(0f, 10f, 0f), 1f, null, Vector2.zero),
                new DetectedObstacle(new Vector3(0f, -20f, 0f), 0.5f, null, new Vector2(0f, 4f)),
                new DetectedObstacle(new Vector3(-4f, 0f, 0f), 2f, null, new Vector2(1f, 2f)),
            }, 3);
            var buffer = new float[AgentObservations.Size];

            AgentObservations.Fill(buffer, self, TargetView.None,
                inMyEnvelope: false, inEnemyEnvelope: false, primaryWeaponReady: false, primaryHeatPct: 0f,
                arenaCenterPlane: Vector2.zero, arenaRadius: ArenaRadius, in scan);

            const float maxR = AgentObservations.SpawnSettingsMaxAsteroidRadius;
            var expectedTokens = new[]
            {
                // nearest first: (-4,0) d=4 → ego relPos (0,4); relVel (1,0) → ego (0,-1).
                0f, 4f / ArenaRadius, 4f / ArenaRadius, 0f, -0.1f, 2f / maxR,
                // (0,10) d=10 → ego (10,0); relVel (0,-2) → ego (-2,0).
                10f / ArenaRadius, 0f, 10f / ArenaRadius, -0.2f, 0f, 1f / maxR,
                // (0,-20) d=20 → ego (-20,0); relVel (0,2) → ego (2,0).
                -20f / ArenaRadius, 0f, 20f / ArenaRadius, 0.2f, 0f, 0.5f / maxR,
            };
            for (var i = 0; i < expectedTokens.Length; i++)
                Assert.AreEqual(expectedTokens[i], buffer[24 + i], 1e-4f, $"token channel {24 + i}");
            for (var i = 24 + expectedTokens.Length; i < buffer.Length; i++)
                Assert.AreEqual(0f, buffer[i], $"empty slot channels must zero (radius 0 ⇔ empty) (channel {i})");
        }

        [Test]
        public void AgentChooser_BoostEmitsOnExactlyOneDecide()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var chooser = new AgentChooser();
                chooser.Configure(opponent, 40f);

                chooser.SetAction(new Vector2(4f, 0f), fire: true, boost: true, boostAvailable: true);

                var first = chooser.Decide(null, 0.02f);
                Assert.IsTrue(first.isValid);
                Assert.IsTrue(first.boost, "boundary tick spends the boost");
                Assert.IsTrue(first.enableFiring);
                Assert.AreEqual(new Vector2(4f, 0f), first.velocityReference);

                var second = chooser.Decide(null, 0.02f);
                Assert.IsTrue(second.isValid);
                Assert.IsFalse(second.boost, "boost is one-shot per decision");
                Assert.AreEqual(first.velocityReference, second.velocityReference,
                    "the cached world-plane reference holds for the interval");
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }

        [Test]
        public void AgentChooser_BoostCommandedWhileUnavailable_StaysANoOp()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var chooser = new AgentChooser();
                chooser.Configure(opponent, 40f);

                chooser.SetAction(new Vector2(4f, 0f), fire: false, boost: true, boostAvailable: false);

                var first = chooser.Decide(null, 0.02f);
                Assert.IsTrue(first.isValid);
                Assert.IsFalse(first.boost,
                    "boost observed unavailable at the boundary must stay a no-op even if the cooldown expires before the next tick");
                Assert.IsFalse(chooser.Decide(null, 0.02f).boost);
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }

        [Test]
        public void AgentChooser_NoActionOrReset_ReturnsNone()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var chooser = new AgentChooser();
                chooser.Configure(opponent, 40f);

                Assert.IsFalse(chooser.Decide(null, 0.02f).isValid, "no action yet → idle");

                chooser.SetAction(Vector2.right, fire: false, boost: false, boostAvailable: true);
                Assert.IsTrue(chooser.Decide(null, 0.02f).isValid);

                chooser.Reset();
                Assert.IsFalse(chooser.Decide(null, 0.02f).isValid, "reset discards the cached action");
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }

        private static void AssertVector(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f);
            Assert.AreEqual(expected.y, actual.y, 1e-4f);
        }
    }
}
#endif
