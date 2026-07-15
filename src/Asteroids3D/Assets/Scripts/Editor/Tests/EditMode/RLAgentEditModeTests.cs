#if UNITY_EDITOR
using AI.Observation;
using Game.RLHarness;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the pure agent maps: action thresholds/clamps, the one-time ego→world conversion, the 23-float observation layout, and the chooser's one-shot boost semantics.</summary>
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
            var buffer = new float[AgentObservations.Size];

            AgentObservations.Fill(buffer, self, in target,
                inMyEnvelope: true, inEnemyEnvelope: false, primaryWeaponReady: true,
                arenaCenterPlane: new Vector2(5f, 65f), arenaRadius: ArenaRadius);

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
            };
            Assert.AreEqual(AgentObservations.Size, expected.Length);
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], buffer[i], 1e-4f, $"channel {i}");
        }

        [Test]
        public void Fill_NoTarget_ZeroesTheTargetBlock()
        {
            var self = new StubStatus { kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f) };
            var buffer = new float[AgentObservations.Size];
            AgentObservations.Fill(buffer, self, TargetView.None,
                inMyEnvelope: false, inEnemyEnvelope: false, primaryWeaponReady: false,
                arenaCenterPlane: Vector2.zero, arenaRadius: ArenaRadius);

            for (var i = 8; i < 18; i++)
                Assert.AreEqual(0f, buffer[i], $"hasTarget flag and target block must zero (channel {i})");
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
