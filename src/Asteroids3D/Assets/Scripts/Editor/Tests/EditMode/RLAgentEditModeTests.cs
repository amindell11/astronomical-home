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
    /// <summary>Pins the pure agent maps: the 5-continuous + 2-discrete action decode (anchored facing offset/weight, polar speeds, discrete branches), the 28-float combat observation layout (self + enemy weapon channels), the nearest-N asteroid attention tokens (selection + normalization + cap truncation, no zero-pad), and the brain's anchored objective shape (anchored facing/velocity + manual fire, never the legacy world facing or aimbot) and one-shot boost semantics.</summary>
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
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => RLAgentEditModeTests.MaxSpeed;
            public float MaxYawRate => 90f;
        }

        [Test]
        public void Map_DecodesAnchoredScalarsAndDiscreteBranches()
        {
            // (0,-1) = face away: offset π, full authority; speeds scale by maxSpeed; discrete[0]=fire.
            var a = AgentActions.Map(0f, -1f, 2f, -3f, 5f, fire: 1, boost: 0, MaxSpeed);
            Assert.AreEqual(Mathf.PI, a.facingOffsetRad, 1e-4f, "(0,-1) faces away from the intercept anchor");
            Assert.AreEqual(1f, a.facingWeight, 1e-6f, "|(0,-1)| = 1 authority");
            Assert.AreEqual(2f * MaxSpeed, a.radialSpeed, 1e-4f);
            Assert.AreEqual(-3f * MaxSpeed, a.tangentialSpeed, 1e-4f);
            Assert.AreEqual(1f, a.velocityWeight, 1e-6f, "vw clamps into [0,1]");
            Assert.IsTrue(a.fire, "discrete[0]==1 fires");
            Assert.IsFalse(a.boost, "discrete[1]==0 no boost");
        }

        [Test]
        public void Map_AimAtIntercept_IsOffsetZero_AndClampsWeights()
        {
            var aim = AgentActions.Map(0f, 1f, 0f, 0f, -0.5f, fire: 0, boost: 1, MaxSpeed);
            Assert.AreEqual(0f, aim.facingOffsetRad, 1e-6f, "(0,+1) aims at intercept — offset 0");
            Assert.AreEqual(1f, aim.facingWeight, 1e-6f);
            Assert.AreEqual(0f, aim.velocityWeight, 1e-6f, "negative vw clamps to 0 (residual-policy start delegates to the prior)");
            Assert.IsFalse(aim.fire);
            Assert.IsTrue(aim.boost, "discrete[1]==1 boosts");

            var corner = AgentActions.Map(1f, 1f, 0f, 0f, 2f, fire: 0, boost: 0, MaxSpeed);
            Assert.AreEqual(1f, corner.facingWeight, 1e-6f, "the action-box corner clamps facing authority to 1");
            Assert.AreEqual(1f, corner.velocityWeight, 1e-6f, "vw > 1 clamps to 1");
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
                arenaCenterPlane: new Vector2(5f, 65f), arenaRadius: ArenaRadius,
                enemyWeaponReady: true, enemyHeatPct: 0.3f);

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
                1f,                  // self primary weapon ready
                0.6f,                // self primary heat pct
                0f, 1f,              // intercept-lead direction ego (hitscan → dead-ahead bearing)
                1f,                  // enemy primary weapon ready
                0.3f,                // enemy primary heat pct
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
                primaryProjectileSpeed: 10f, arenaCenterPlane: Vector2.zero, arenaRadius: ArenaRadius,
                enemyWeaponReady: false, enemyHeatPct: 0f);

            var lead = new Vector2(buffer[24], buffer[25]);
            Assert.AreEqual(1f, lead.magnitude, 1e-4f, "the lead channels carry a unit direction");
            Assert.Greater(lead.x, 0.1f, "lead must sit on the target's motion side of the bearing");
            Assert.Greater(lead.y, 0.5f, "lead must still point substantially at the target");
        }

        [Test]
        public void Fill_NoTarget_ZeroesTheTargetBlockLeadAndEnemyWeapon()
        {
            var self = new StubStatus { kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f) };
            var buffer = new float[AgentObservations.CombatChannels];
            AgentObservations.Fill(buffer, self, TargetView.None,
                inMyEnvelope: false, inEnemyEnvelope: false, primaryWeaponReady: false, primaryHeatPct: 0f,
                primaryProjectileSpeed: 40f, arenaCenterPlane: Vector2.zero, arenaRadius: ArenaRadius,
                enemyWeaponReady: true, enemyHeatPct: 0.9f);

            for (var i = 8; i < 18; i++)
                Assert.AreEqual(0f, buffer[i], $"hasTarget flag and target block must zero (channel {i})");
            Assert.AreEqual(0f, buffer[24], "lead x must zero with no target");
            Assert.AreEqual(0f, buffer[25], "lead y must zero with no target");
            Assert.AreEqual(0f, buffer[26], "enemy weapon ready must zero with no target");
            Assert.AreEqual(0f, buffer[27], "enemy heat must zero with no target");
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
        public void PolicyBrain_ManualAction_CarriesAnchoredFacingAndVelocity_NeverTheAimbot()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var brain = opponentGo.AddComponent<PolicyBrain>();
                brain.Configure(opponent);

                var action = new AgentAction(facingOffsetRad: 1.2f, facingWeight: 0.4f,
                    radialSpeed: 4f, tangentialSpeed: -2f, velocityWeight: 0.6f, fire: true, boost: false);
                brain.SetAction(in action, boostAvailable: true);

                var decision = brain.Decide(null).Value;
                Assert.IsTrue(decision.nav.TryGetAnchorId(out _), "the objective names the enemy it is anchored to");

                // B1 boundary pin: the facing command rides the anchored channel, and the world reference stays unarmed.
                Assert.IsFalse(decision.nav.hasPlanarVelocity, "the polar move channel replaces the world reference");
                Assert.IsTrue(decision.nav.sentence.aim.armed, "the AIM slot carries the command");
                Assert.AreEqual(1.2f, decision.nav.sentence.aim.offsetRad, 1e-6f);
                Assert.AreEqual(0.4f, decision.nav.sentence.aim.weight, 1e-6f, "authority weight rides the AIM slot");

                Assert.IsTrue(decision.nav.sentence.vel.armed);
                Assert.AreEqual(4f, decision.nav.sentence.vel.radialSpeed, 1e-6f);
                Assert.AreEqual(-2f, decision.nav.sentence.vel.tangentialSpeed, 1e-6f);
                Assert.AreEqual(0.6f, decision.nav.sentence.vel.weight, 1e-6f);

                Assert.IsTrue(decision.engagePrimary, "the policy's fire action gates the primary engage");
                Assert.IsFalse(decision.engageSecondary,
                    "the secondary stays disengaged — arming it is Intent_Grammar Stage C work");
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }

        [Test]
        public void PolicyBrain_BoostEmitsOnExactlyOneDecide()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var brain = opponentGo.AddComponent<PolicyBrain>();
                brain.Configure(opponent);

                var action = new AgentAction(facingOffsetRad: 0f, facingWeight: 1f,
                    radialSpeed: 4f, tangentialSpeed: 0f, velocityWeight: 1f, fire: true, boost: true);
                brain.SetAction(in action, boostAvailable: true);

                var first = brain.Decide(null).Value;
                Assert.IsTrue(first.boost, "boundary tick spends the boost");
                Assert.IsTrue(first.engagePrimary);
                Assert.AreEqual(4f, first.nav.sentence.vel.radialSpeed, 1e-6f);

                var second = brain.Decide(null).Value;
                Assert.IsFalse(second.boost, "boost is one-shot per decision");
                Assert.AreEqual(first.nav.sentence.vel.radialSpeed, second.nav.sentence.vel.radialSpeed,
                    "the cached anchored command holds for the interval");
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }

        [Test]
        public void PolicyBrain_BoostCommandedWhileUnavailable_StaysANoOp()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var brain = opponentGo.AddComponent<PolicyBrain>();
                brain.Configure(opponent);

                var action = new AgentAction(facingOffsetRad: 0f, facingWeight: 1f,
                    radialSpeed: 4f, tangentialSpeed: 0f, velocityWeight: 1f, fire: false, boost: true);
                brain.SetAction(in action, boostAvailable: false);

                Assert.IsFalse(brain.Decide(null).Value.boost,
                    "boost observed unavailable at the boundary must stay a no-op even if the cooldown expires before the next tick");
                Assert.IsFalse(brain.Decide(null).Value.boost);
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }

        [Test]
        public void PolicyBrain_NoActionOrReset_ReturnsNone()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var brain = opponentGo.AddComponent<PolicyBrain>();
                brain.Configure(opponent);

                Assert.IsFalse(brain.Decide(null).HasValue, "no action yet → no decision");

                var action = new AgentAction(facingOffsetRad: 0f, facingWeight: 1f,
                    radialSpeed: 1f, tangentialSpeed: 0f, velocityWeight: 1f, fire: false, boost: false);
                brain.SetAction(in action, boostAvailable: true);
                Assert.IsTrue(brain.Decide(null).HasValue);

                brain.ResetState();
                Assert.IsFalse(brain.Decide(null).HasValue, "reset discards the cached action");
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }
    }
}
#endif
