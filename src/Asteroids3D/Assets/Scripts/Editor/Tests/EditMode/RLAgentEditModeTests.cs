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
    /// <summary>Pins the pure agent maps: action thresholds/clamps, the one-time ego→world conversions (velocity and facing), the 26-float combat observation layout, the nearest-N asteroid attention tokens (selection + normalization + cap truncation, no zero-pad), and the chooser's intent shapes (manual aim/fire vs the legacy aimbot mode) and one-shot boost semantics.</summary>
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
            var a = AgentActions.Map(2f, -3f, 0.01f, -0.01f, 0.4f, -0.9f);
            Assert.AreEqual(new Vector2(1f, -1f), a.velocityEgo);
            Assert.AreEqual(new Vector2(0.4f, -0.9f), a.facingEgo);
            Assert.IsTrue(a.fire);
            Assert.IsFalse(a.boost);

            var atThreshold = AgentActions.Map(0f, 0f, 0f, 0f, 0f, 0f);
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
        public void ToFacingRad_MapsEgoDirectionToMpcYaw()
        {
            // MPC yaw convention: fwd = (−sin, cos), so world +Y ⇒ 0 and world +X ⇒ −π/2.
            Assert.AreEqual(0f, AgentActions.ToFacingRad(new Vector2(0f, 1f), Vector2.up), 1e-4f,
                "ego-forward with the nose on +Y stays yaw 0");
            Assert.AreEqual(-0.5f * Mathf.PI,
                AgentActions.ToFacingRad(new Vector2(0f, 1f), Vector2.right), 1e-4f,
                "ego-forward with the nose on +X is yaw −π/2");
            Assert.AreEqual(-0.5f * Mathf.PI,
                AgentActions.ToFacingRad(new Vector2(1f, 0f), Vector2.up), 1e-4f,
                "ego-right from a +Y nose points at world +X");
            Assert.AreEqual(AgentActions.ToFacingRad(new Vector2(0f, 0.1f), Vector2.right),
                AgentActions.ToFacingRad(new Vector2(0f, 1f), Vector2.right), 1e-4f,
                "facing is a direction — magnitude must not change the commanded yaw");
        }

        [Test]
        public void ToFacingRad_DegenerateDirection_HoldsTheCurrentNose()
        {
            Assert.AreEqual(-0.5f * Mathf.PI,
                AgentActions.ToFacingRad(Vector2.zero, Vector2.right), 1e-4f,
                "a zero direction must resolve to the current forward, never to yaw 0");
        }

        [Test]
        public void Fill_LaysOutTheCombatChannels()
        {
            var self = new StubStatus
            {
                // yaw 0 → forward (0,1), right (1,0): ego == plane axes shifted to pos.
                kinematics = new Kinematics(new Vector2(5f, 5f), new Vector2(3f, 0f), 0f, 45f, 0f),
            };
            var target = new TargetView(true, new Vector2(5f, 15f), new Vector2(3f, 5f),
                new Vector2(0f, -1f), 0.7f, 0.2f);
            var buffer = new float[AgentObservations.CombatChannels];

            AgentObservations.Fill(buffer, self, in target,
                inMyEnvelope: true, inEnemyEnvelope: false, primaryWeaponReady: true, primaryHeatPct: 0.6f,
                primaryProjectileSpeed: 0f, // hitscan lead: aim point = target position
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
                0.6f,                // self primary heat pct
                0f, 1f,              // intercept-lead direction ego (hitscan → dead-ahead bearing)
            };
            Assert.AreEqual(AgentObservations.CombatChannels, expected.Length, "the flat vector is combat-only");
            Assert.AreEqual(AgentObservations.CombatChannels, buffer.Length);
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], buffer[i], 1e-4f, $"channel {i}");
        }

        [Test]
        public void Fill_LeadDirection_LeadsAMovingTargetOnTheMotionSide()
        {
            var self = new StubStatus
            {
                kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f),
            };
            // Target dead ahead moving ego-right; a finite projectile speed must pull the lead off the bearing toward the motion.
            var target = new TargetView(true, new Vector2(0f, 10f), new Vector2(5f, 0f),
                new Vector2(0f, -1f), 1f, 1f);
            var buffer = new float[AgentObservations.CombatChannels];

            AgentObservations.Fill(buffer, self, in target,
                inMyEnvelope: false, inEnemyEnvelope: false, primaryWeaponReady: true, primaryHeatPct: 0f,
                primaryProjectileSpeed: 10f, arenaCenterPlane: Vector2.zero, arenaRadius: ArenaRadius);

            var lead = new Vector2(buffer[24], buffer[25]);
            Assert.AreEqual(1f, lead.magnitude, 1e-4f, "the lead channels carry a unit direction");
            Assert.Greater(lead.x, 0.1f, "lead must sit on the target's motion side of the bearing");
            Assert.Greater(lead.y, 0.5f, "lead must still point substantially at the target");
        }

        [Test]
        public void Fill_NoTarget_ZeroesTheTargetBlockAndLead()
        {
            var self = new StubStatus { kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f) };
            var buffer = new float[AgentObservations.CombatChannels];
            AgentObservations.Fill(buffer, self, TargetView.None,
                inMyEnvelope: false, inEnemyEnvelope: false, primaryWeaponReady: false, primaryHeatPct: 0f,
                primaryProjectileSpeed: 40f, arenaCenterPlane: Vector2.zero, arenaRadius: ArenaRadius);

            for (var i = 8; i < 18; i++)
                Assert.AreEqual(0f, buffer[i], $"hasTarget flag and target block must zero (channel {i})");
            Assert.AreEqual(0f, buffer[24], "lead x must zero with no target");
            Assert.AreEqual(0f, buffer[25], "lead y must zero with no target");
        }

        [Test]
        public void BuildObstacleTokens_EgoTransformsAndSelectsNearest_NoPad()
        {
            var self = new StubStatus
            {
                // yaw 90 → forward (-1,0), right (0,1): a real rotation, not the identity frame.
                kinematics = new Kinematics(Vector2.zero, new Vector2(0f, 2f), 90f, 0f, 0f),
            };
            // Deliberately unsorted: distances 10, 20, 4. The nearest carries battle damage (healthPct 0.25).
            var scan = new ObstacleScan(new[]
            {
                new DetectedObstacle(new Vector3(0f, 10f, 0f), 1f, null, Vector2.zero),
                new DetectedObstacle(new Vector3(0f, -20f, 0f), 0.5f, null, new Vector2(0f, 4f)),
                new DetectedObstacle(new Vector3(-4f, 0f, 0f), 2f, null, new Vector2(1f, 2f), healthPct: 0.25f),
            }, 3);
            var dest = new float[3 * AgentObservations.ObstacleTokenFloats];

            var n = AgentObservations.BuildObstacleTokens(dest, maxTokens: 8, self, ArenaRadius, in scan);
            Assert.AreEqual(3, n, "all three asteroids fit under the cap");

            const float maxR = AgentObservations.SpawnSettingsMaxAsteroidRadius;
            var expected = new[]
            {
                // nearest first: (-4,0) d=4 → ego relPos (0,4); relVel (1,0) → ego (0,-1); chipped to 0.25 health.
                0f, 4f / ArenaRadius, 4f / ArenaRadius, 0f, -0.1f, 2f / maxR, 0.25f,
                // (0,10) d=10 → ego (10,0); relVel (0,-2) → ego (-2,0).
                10f / ArenaRadius, 0f, 10f / ArenaRadius, -0.2f, 0f, 1f / maxR, 1f,
                // (0,-20) d=20 → ego (-20,0); relVel (0,2) → ego (2,0).
                -20f / ArenaRadius, 0f, 20f / ArenaRadius, 0.2f, 0f, 0.5f / maxR, 1f,
            };
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], dest[i], 1e-4f, $"token float {i}");
        }

        [Test]
        public void BuildObstacleTokens_TruncatesToNearestN_UnderTheCap()
        {
            var self = new StubStatus { kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f) };
            // Five asteroids at ascending distances; a cap of 2 keeps only the two nearest, drops the rest.
            var scan = new ObstacleScan(new[]
            {
                new DetectedObstacle(new Vector3(0f, 30f, 0f), 1f, null, Vector2.zero),
                new DetectedObstacle(new Vector3(0f, 5f, 0f), 1f, null, Vector2.zero),
                new DetectedObstacle(new Vector3(0f, 50f, 0f), 1f, null, Vector2.zero),
                new DetectedObstacle(new Vector3(0f, 15f, 0f), 1f, null, Vector2.zero),
                new DetectedObstacle(new Vector3(0f, 40f, 0f), 1f, null, Vector2.zero),
            }, 5);
            var dest = new float[2 * AgentObservations.ObstacleTokenFloats];

            var n = AgentObservations.BuildObstacleTokens(dest, maxTokens: 2, self, ArenaRadius, in scan);
            Assert.AreEqual(2, n, "the cap truncates the emitted token count");

            // The two nearest are d=5 and d=15 (distance channel = float index 2 of each 7-float token).
            var d0 = dest[2] * ArenaRadius;
            var d1 = dest[AgentObservations.ObstacleTokenFloats + 2] * ArenaRadius;
            Assert.AreEqual(5f, Mathf.Min(d0, d1), 1e-4f, "nearest kept");
            Assert.AreEqual(15f, Mathf.Max(d0, d1), 1e-4f, "second-nearest kept, farther three dropped");
        }

        [Test]
        public void BuildObstacleTokens_EmptyScan_EmitsNothing()
        {
            var self = new StubStatus { kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f) };
            var dest = new float[AgentObservations.ObstacleTokenFloats];
            Assert.AreEqual(0, AgentObservations.BuildObstacleTokens(dest, maxTokens: 8, self, ArenaRadius, default));
        }

        [Test]
        public void AgentChooser_ManualAction_CarriesFacingAndManualFire_NeverTheAimbot()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var chooser = new AgentChooser();
                chooser.Configure(opponent, 40f);

                chooser.SetAction(new Vector2(4f, 0f), facingRad: 1.2f, fire: true, boost: false, boostAvailable: true);

                var intent = chooser.Decide(null, 0.02f);
                Assert.IsTrue(intent.isValid);
                Assert.IsTrue(intent.hasTarget, "the target snapshot stays (obstacle exclusion keys on it)");
                Assert.IsTrue(intent.hasFacing);
                Assert.AreEqual(1.2f, intent.facingRad, 1e-6f);
                Assert.IsTrue(intent.manualFire);
                Assert.IsTrue(intent.primaryHeld);
                Assert.IsFalse(intent.aimAtTarget, "manual aim must leave the MPC intercept override dormant");
                Assert.AreEqual(0f, intent.projectileSpeed, "no aim-purpose projectile speed on the manual path");
                Assert.IsFalse(intent.enableFiring, "the Gunner path must stay cold on the manual path");
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }

        [Test]
        public void AgentChooser_LegacyAction_KeepsTheAimbotIntentShape()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var chooser = new AgentChooser();
                chooser.Configure(opponent, 40f);

                chooser.SetLegacyAction(new Vector2(4f, 0f), fire: true, boost: false, boostAvailable: true);

                var intent = chooser.Decide(null, 0.02f);
                Assert.IsTrue(intent.isValid);
                Assert.IsTrue(intent.aimAtTarget);
                Assert.AreEqual(40f, intent.projectileSpeed);
                Assert.IsTrue(intent.enableFiring);
                Assert.IsFalse(intent.hasFacing);
                Assert.IsFalse(intent.manualFire);
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
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

                chooser.SetAction(new Vector2(4f, 0f), facingRad: 0f, fire: true, boost: true, boostAvailable: true);

                var first = chooser.Decide(null, 0.02f);
                Assert.IsTrue(first.isValid);
                Assert.IsTrue(first.boost, "boundary tick spends the boost");
                Assert.IsTrue(first.primaryHeld);
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

                chooser.SetAction(new Vector2(4f, 0f), facingRad: 0f, fire: false, boost: true, boostAvailable: false);

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

                chooser.SetAction(Vector2.right, facingRad: 0f, fire: false, boost: false, boostAvailable: true);
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
