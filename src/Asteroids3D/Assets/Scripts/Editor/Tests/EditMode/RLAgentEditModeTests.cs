#if UNITY_EDITOR
using System.Collections.Generic;
using AI.Observation;
using AI.Scanning;
using Asteroids;
using Game.RLHarness;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the pure agent maps: the sentence action decode (10 continuous + 8 discrete branches, every channel clamped, referents resolved against the observed roster), the vocabulary/curriculum action mask, the 76-float combat observation layout (28 legacy channels + 6 rock-slot blocks), the nearest-N asteroid attention tokens (selection + normalization + cap truncation, no zero-pad), and the brain's sentence objective shape (all five slots armed per decision, manual fire, never the legacy world facing or aimbot) and one-shot boost semantics.</summary>
    [Category("AI")]
    public class RLAgentEditModeTests
    {
        private const float MaxSpeed = 10f;
        private const float ArenaRadius = 120f;
        private const float SpeedRef = 12f;

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go)
                    Object.DestroyImmediate(go);
            spawned.Clear();
        }

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

        private sealed class RecordingMask : IDiscreteActionMask
        {
            private readonly Dictionary<(int branch, int index), bool> states = new();

            public void SetActionEnabled(int branch, int actionIndex, bool isEnabled) =>
                states[(branch, actionIndex)] = isEnabled;

            /// <summary>ML-Agents' default: an untouched action stays enabled.</summary>
            public bool Enabled(int branch, int index) =>
                !states.TryGetValue((branch, index), out var enabled) || enabled;
        }

        private AsteroidController Rock(float x, float y = 0f)
        {
            var rock = TestRocks.Spawn(new Vector2(x, y));
            spawned.Add(rock.gameObject);
            return rock;
        }

        private static ObstacleScan Scan(params AsteroidController[] rocks)
        {
            var buffer = new DetectedObstacle[rocks.Length];
            for (var i = 0; i < rocks.Length; i++)
                buffer[i] = new DetectedObstacle(rocks[i].transform.position, 2f, rocks[i].SimpleCollider,
                    default, 1f, rocks[i]);
            return new ObstacleScan(buffer, buffer.Length);
        }

        private static ActionBuffers Buffers(float[] continuous = null, int[] discrete = null) => new(
            continuous ?? new float[AgentActions.ContinuousCount],
            discrete ?? new int[AgentActions.BranchSizes.Length]);

        private static float[] Continuous(params (int index, float value)[] channels)
        {
            var c = new float[AgentActions.ContinuousCount];
            foreach (var (index, value) in channels)
                c[index] = value;
            return c;
        }

        private static int[] Discrete(params (int branch, int choice)[] branches)
        {
            var d = new int[AgentActions.BranchSizes.Length];
            foreach (var (branch, choice) in branches)
                d[branch] = choice;
            return d;
        }

        [Test]
        public void ApplySchema_SingleSourcesTheSchemaShape()
        {
            var go = new GameObject("Schema");
            try
            {
                var behavior = go.AddComponent<BehaviorParameters>();
                var buffer = go.AddComponent<BufferSensorComponent>();
                AgentObservations.ApplySchema(behavior, buffer);

                // Literal pins on purpose: a drifted number here is a schema break, and schema breaks are declared, not discovered.
                Assert.AreEqual(76, behavior.BrainParameters.VectorObservationSize, "obs 28 → 76 (§Stage C fork 1)");
                var spec = behavior.BrainParameters.ActionSpec;
                Assert.AreEqual(10, spec.NumContinuousActions, "the sentence head (§Stage C fork 2)");
                CollectionAssert.AreEqual(new[] { 7, 7, 7, 3, 3, 2, 2, 2 }, spec.BranchSizes,
                    "referent ×3, frame ×2, fire primary/secondary, boost");
                Assert.AreEqual(AgentObservations.ObstacleSensorName, buffer.SensorName);
                Assert.AreEqual(AgentObservations.ObstacleTokenFloats, buffer.ObservableSize);
                Assert.AreEqual(AgentObservations.ObstacleTokenCap, buffer.MaxNumObservables);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EpisodeContract_IsV7() => Assert.AreEqual("rl-episode-v7", EpisodeResult.SchemaId,
            "the Stage C3 schema break bumps the episode contract — a consumer reading v6 rows must fail loudly");

        [Test]
        public void Map_DecodesDirectionHeadsAndClampsEveryChannel()
        {
            var roster = new RockSlotRoster();
            var actions = Buffers(
                Continuous((AgentActions.AimX, 0f), (AgentActions.AimY, -1f),
                    (AgentActions.PosX, 2f), (AgentActions.PosY, 0f),
                    (AgentActions.PosSetpoint, 0.5f), (AgentActions.PosWeight, -3f),
                    (AgentActions.VelRadial, 0.3f), (AgentActions.VelTangential, -0.4f),
                    (AgentActions.LaneWeight, -2f), (AgentActions.FieldWeight, 2f)),
                Discrete((AgentActions.FirePrimaryBranch, 1)));

            var a = AgentActions.Map(in actions, roster, SpeedRef, ArenaRadius);

            Assert.AreEqual(Mathf.PI, a.aimOffsetRad, 1e-4f, "(0,-1) faces away from the intercept anchor");
            Assert.AreEqual(1f, a.aimWeight, 1e-6f, "|(0,-1)| = 1 authority");

            Assert.AreEqual(ArenaRadius, a.posOffsetR, 1e-3f, "px clamps to 1 before the arena-radius scale");
            Assert.AreEqual(Mathf.PI / 2f, a.posOffsetThetaRad, 1e-4f, "+x offsets CCW from frame-forward (the AIM convention)");
            Assert.AreEqual(0.5f * ArenaRadius, a.posSetpoint, 1e-3f, "setpoint normalizes by arena radius");
            Assert.AreEqual(-1f, a.posWeight, 1e-6f, "POS weight is signed and clamps into [-1,1]");

            Assert.AreEqual(0.5f, a.velWeight, 1e-6f, "VEL weight = |(vr,vt)|, the θ-head's sole authority channel");
            Assert.AreEqual(0.6f * SpeedRef, a.velRadialSpeed, 1e-4f, "direction × speedRef — magnitude divided out");
            Assert.AreEqual(-0.8f * SpeedRef, a.velTangentialSpeed, 1e-4f);

            Assert.AreEqual(-1f, a.laneWeight, 1e-6f, "LANE weight is signed and clamps into [-1,1]");
            Assert.AreEqual(1f, a.fieldWeight, 1e-6f, "FIELD weight clamps into [0,1]");

            Assert.IsTrue(a.firePrimary);
            Assert.IsFalse(a.fireSecondary);
            Assert.IsFalse(a.boost);
        }

        [Test]
        public void Map_OversizedVelVector_ClampsWeightButKeepsDirection()
        {
            var roster = new RockSlotRoster();
            var actions = Buffers(Continuous(
                (AgentActions.VelRadial, 2f), (AgentActions.VelTangential, -3f)));

            var a = AgentActions.Map(in actions, roster, SpeedRef, ArenaRadius);
            var magnitude = Mathf.Sqrt(2f * 2f + 3f * 3f);
            Assert.AreEqual(1f, a.velWeight, 1e-6f, "weight clamps to 1");
            Assert.AreEqual(2f / magnitude * SpeedRef, a.velRadialSpeed, 1e-4f, "speed stays speedRef-normalized");
            Assert.AreEqual(-3f / magnitude * SpeedRef, a.velTangentialSpeed, 1e-4f);
        }

        [Test]
        public void Map_RockChoice_BindsTheObservedSlot_EmptySlotZeroesTheWeight()
        {
            var rock = Rock(5f, 0f);
            var roster = new RockSlotRoster();
            roster.Update(Vector2.zero, new Vector2(50f, 0f), Scan(rock), default);
            Assert.IsTrue(roster.TryGetSlot(0, out var slotRock), "the lone rock seats in slot 0");

            var actions = Buffers(
                Continuous((AgentActions.AimX, 0f), (AgentActions.AimY, 1f),
                    (AgentActions.PosWeight, 0.8f), (AgentActions.VelRadial, 1f)),
                Discrete((AgentActions.AimReferentBranch, 1),
                    (AgentActions.PosReferentBranch, 5),
                    (AgentActions.VelReferentBranch, 0)));

            var a = AgentActions.Map(in actions, roster, SpeedRef, ArenaRadius);

            Assert.AreEqual(1, a.aimReferent.choice);
            Assert.IsTrue(a.aimReferent.rock.Equals(slotRock), "choice 1 binds the entity slot 0 held when observed");
            Assert.AreEqual(1f, a.aimWeight, 1e-6f);

            Assert.AreEqual(5, a.posReferent.choice);
            Assert.IsTrue(a.posReferent.Empty, "slot 4 is unoccupied");
            Assert.AreEqual(0f, a.posWeight, 1e-6f, "an empty referent zeroes the slot's weight — the invalidation rule at decode time");

            Assert.IsTrue(a.velReferent.Enemy, "choice 0 stays the enemy");
            Assert.AreEqual(1f, a.velWeight, 1e-6f, "the enemy-bound slot keeps its weight");
        }

        [Test]
        public void WriteMask_Pinned_IsTheLegacyEquivalencePoint()
        {
            var rock = Rock(5f, 0f);
            var roster = new RockSlotRoster();
            roster.Update(Vector2.zero, new Vector2(50f, 0f), Scan(rock), default);

            var mask = new RecordingMask();
            AgentActions.WriteMask(mask, roster, released: false);

            foreach (var branch in new[] { AgentActions.AimReferentBranch, AgentActions.PosReferentBranch, AgentActions.VelReferentBranch })
            {
                Assert.IsTrue(mask.Enabled(branch, 0), $"branch {branch}: the enemy referent stays choosable");
                for (var choice = 1; choice < AgentActions.ReferentChoices; choice++)
                    Assert.IsFalse(mask.Enabled(branch, choice), $"branch {branch}: pinned masks rock choice {choice} even when occupied");
            }
            foreach (var branch in new[] { AgentActions.PosFrameBranch, AgentActions.VelFrameBranch })
            {
                Assert.IsTrue(mask.Enabled(branch, (int)ReferentFrame.Position), "Position frame stays choosable");
                Assert.IsFalse(mask.Enabled(branch, (int)ReferentFrame.Facing));
                Assert.IsFalse(mask.Enabled(branch, (int)ReferentFrame.Velocity));
            }
        }

        [Test]
        public void WriteMask_Released_OpensOnlyOccupiedSlots_SecondaryStaysDisengaged()
        {
            var rock = Rock(5f, 0f);
            var roster = new RockSlotRoster();
            roster.Update(Vector2.zero, new Vector2(50f, 0f), Scan(rock), default);

            var mask = new RecordingMask();
            AgentActions.WriteMask(mask, roster, released: true);

            foreach (var branch in new[] { AgentActions.AimReferentBranch, AgentActions.PosReferentBranch, AgentActions.VelReferentBranch })
            {
                Assert.IsTrue(mask.Enabled(branch, 1), $"branch {branch}: the occupied slot opens");
                for (var choice = 2; choice < AgentActions.ReferentChoices; choice++)
                    Assert.IsFalse(mask.Enabled(branch, choice), $"branch {branch}: empty slot choice {choice} is never choosable");
            }
            foreach (var branch in new[] { AgentActions.PosFrameBranch, AgentActions.VelFrameBranch })
                for (var frame = 0; frame < AgentActions.FrameChoices; frame++)
                    Assert.IsTrue(mask.Enabled(branch, frame), $"released opens every frame on branch {branch}");

            Assert.IsTrue(mask.Enabled(AgentActions.FirePrimaryBranch, 1), "the primary trigger is never masked");
            Assert.IsFalse(mask.Enabled(AgentActions.FireSecondaryBranch, 1),
                "the secondary stays disengage-only until marksmanship (#409) arms it");
            Assert.IsTrue(mask.Enabled(AgentActions.BoostBranch, 1), "boost is never masked");
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
                enemyWeaponReady: true, enemyHeatPct: 0.3f, rockSlots: new RockSlotRoster());

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
            Assert.AreEqual(AgentObservations.LegacyCombatChannels, expected.Length, "the legacy block is combat-only");
            Assert.AreEqual(AgentObservations.CombatChannels, buffer.Length);
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], buffer[i], 1e-4f, $"channel {i}");
            for (var i = expected.Length; i < buffer.Length; i++)
                Assert.AreEqual(0f, buffer[i], $"an empty roster zero-fills every rock-slot block (channel {i})");
        }

        [Test]
        public void Fill_RockSlots_CarryValidFlagAndTokenLayout()
        {
            var self = new StubStatus
            {
                // yaw 0 → ego == plane axes shifted to pos; self velocity (2,0) exercises relVel.
                kinematics = new Kinematics(new Vector2(5f, 5f), new Vector2(2f, 0f), 0f, 0f, 0f),
            };
            var rock = Rock(5f, 25f);
            var roster = new RockSlotRoster();
            roster.Update(self.kinematics.pos, new Vector2(50f, 0f), Scan(rock), default);
            var target = new TargetView(true, new Vector2(50f, 0f), Vector2.zero, Vector2.up, 1f, 1f);
            var buffer = new float[AgentObservations.CombatChannels];

            AgentObservations.Fill(buffer, self, in target,
                inMyEnvelope: false, inEnemyEnvelope: false, primaryWeaponReady: false, primaryHeatPct: 0f,
                primaryProjectileSpeed: 0f, arenaCenterPlane: Vector2.zero, arenaRadius: ArenaRadius,
                enemyWeaponReady: false, enemyHeatPct: 0f, rockSlots: roster);

            var i = AgentObservations.LegacyCombatChannels;
            Assert.AreEqual(1f, buffer[i++], "slot 0 valid flag");
            Assert.AreEqual(0f, buffer[i++], 1e-4f, "ego relPos.x / R");
            Assert.AreEqual(20f / ArenaRadius, buffer[i++], 1e-4f, "ego relPos.y / R");
            Assert.AreEqual(20f / ArenaRadius, buffer[i++], 1e-4f, "distance / R");
            Assert.AreEqual(-0.2f, buffer[i++], 1e-4f, "ego relVel.x / MaxSpeed (rock at rest, self moving +x)");
            Assert.AreEqual(0f, buffer[i++], 1e-4f, "ego relVel.y / MaxSpeed");
            Assert.AreEqual(2f / AgentObservations.SpawnSettingsMaxAsteroidRadius, buffer[i++], 1e-4f, "radius norm");
            Assert.AreEqual(1f, buffer[i++], "healthPct");
            for (; i < buffer.Length; i++)
                Assert.AreEqual(0f, buffer[i], $"empty slots zero-fill (channel {i})");
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
                enemyWeaponReady: false, enemyHeatPct: 0f, rockSlots: new RockSlotRoster());

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
                enemyWeaponReady: true, enemyHeatPct: 0.9f, rockSlots: new RockSlotRoster());

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

        private static AgentAction MapOn(RockSlotRoster roster, float[] continuous, int[] discrete)
        {
            var actions = Buffers(continuous, discrete);
            return AgentActions.Map(in actions, roster, SpeedRef, ArenaRadius);
        }

        [Test]
        public void PolicyBrain_SentenceAction_ArmsAllFiveSlots_NeverTheAimbot()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var brain = opponentGo.AddComponent<PolicyBrain>();
                brain.Configure(opponent);

                var action = MapOn(new RockSlotRoster(),
                    Continuous((AgentActions.AimX, Mathf.Sin(1.2f) * 0.4f), (AgentActions.AimY, Mathf.Cos(1.2f) * 0.4f),
                        (AgentActions.PosX, 0f), (AgentActions.PosY, 0.25f),
                        (AgentActions.PosSetpoint, 0.1f), (AgentActions.PosWeight, -0.5f),
                        (AgentActions.VelRadial, 0.6f), (AgentActions.VelTangential, 0f),
                        (AgentActions.LaneWeight, -0.3f), (AgentActions.FieldWeight, 0.7f)),
                    Discrete((AgentActions.FirePrimaryBranch, 1),
                        (AgentActions.PosFrameBranch, (int)ReferentFrame.Facing)));
                brain.SetAction(in action, boostAvailable: true);

                var decision = brain.Decide(null).Value;
                Assert.IsTrue(decision.nav.TryGetAnchorId(out _), "the objective names the enemy it is anchored to");
                Assert.IsFalse(decision.nav.hasPlanarVelocity, "the polar move channel replaces the world reference");

                var sentence = decision.nav.sentence;
                Assert.IsTrue(sentence.aim.armed, "the AIM slot carries the command");
                Assert.AreEqual(1.2f, sentence.aim.offsetRad, 1e-4f);
                Assert.AreEqual(0.4f, sentence.aim.weight, 1e-4f, "authority weight rides the AIM slot");

                Assert.IsTrue(sentence.vel.armed);
                Assert.AreEqual(SpeedRef, sentence.vel.radialSpeed, 1e-4f, "unit direction × speedRef");
                Assert.AreEqual(0f, sentence.vel.tangentialSpeed, 1e-4f);
                Assert.AreEqual(0.6f, sentence.vel.weight, 1e-4f);
                Assert.AreEqual(ReferentFrame.Position, sentence.vel.frame);

                Assert.IsTrue(sentence.pos.armed, "every slot is armed per decision — weight 0 is the silence");
                Assert.AreEqual(0.25f * ArenaRadius, sentence.pos.offsetR, 1e-3f);
                Assert.AreEqual(0.1f * ArenaRadius, sentence.pos.setpoint, 1e-3f);
                Assert.AreEqual(-0.5f, sentence.pos.weight, 1e-4f);
                Assert.AreEqual(ReferentFrame.Facing, sentence.pos.frame, "the frame branch rides into the carrier");

                Assert.IsTrue(sentence.lane.armed);
                Assert.AreEqual(-0.3f, sentence.lane.weight, 1e-4f);
                Assert.IsTrue(sentence.field.armed);
                Assert.AreEqual(0.7f, sentence.field.weight, 1e-4f);

                Assert.IsTrue(decision.engagePrimary, "the policy's fire action gates the primary engage");
                Assert.IsFalse(decision.engageSecondary,
                    "the secondary branch decodes but arrives masked to disengage until #409");
            }
            finally
            {
                Object.DestroyImmediate(opponentGo);
            }
        }

        [Test]
        public void PolicyBrain_RockBoundSlots_RideTheSeats_AndReportBoundRocks()
        {
            var opponentGo = new GameObject("Opponent");
            try
            {
                var opponent = opponentGo.AddComponent<Ship>();
                var brain = opponentGo.AddComponent<PolicyBrain>();
                brain.Configure(opponent);

                var rock = Rock(5f, 0f);
                var roster = new RockSlotRoster();
                roster.Update(Vector2.zero, new Vector2(50f, 0f), Scan(rock), default);

                var action = MapOn(roster,
                    Continuous((AgentActions.AimX, 0f), (AgentActions.AimY, 1f),
                        (AgentActions.PosWeight, 0.5f), (AgentActions.VelRadial, 1f)),
                    Discrete((AgentActions.PosReferentBranch, 1)));
                brain.SetAction(in action, boostAvailable: false);

                var bound = new AI.AsteroidRef[PolicyBrain.MaxBoundRocks];
                Assert.AreEqual(1, brain.GetBoundRocks(bound), "one distinct rock is bound");
                Assert.IsTrue(bound[0].Equals(AI.AsteroidRef.Of(rock)));

                var decision = brain.Decide(null).Value;
                Assert.AreEqual(1, decision.nav.sentence.pos.referent, "the rock claims seat 1");
                Assert.IsTrue(decision.nav.RockSeat(1).Equals(AI.AsteroidRef.Of(rock)), "the seat carries the identity");
                Assert.AreEqual(0, decision.nav.sentence.aim.referent, "AIM stays on the enemy stream");
                Assert.AreEqual(0, decision.nav.sentence.vel.referent);
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

                var action = MapOn(new RockSlotRoster(),
                    Continuous((AgentActions.AimY, 1f), (AgentActions.VelRadial, 0.4f)),
                    Discrete((AgentActions.FirePrimaryBranch, 1), (AgentActions.BoostBranch, 1)));
                brain.SetAction(in action, boostAvailable: true);

                var first = brain.Decide(null).Value;
                Assert.IsTrue(first.boost, "boundary tick spends the boost");
                Assert.IsTrue(first.engagePrimary);
                Assert.AreEqual(SpeedRef, first.nav.sentence.vel.radialSpeed, 1e-4f);

                var second = brain.Decide(null).Value;
                Assert.IsFalse(second.boost, "boost is one-shot per decision");
                Assert.AreEqual(first.nav.sentence.vel.radialSpeed, second.nav.sentence.vel.radialSpeed,
                    "the cached sentence holds for the interval");
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

                var action = MapOn(new RockSlotRoster(),
                    Continuous((AgentActions.AimY, 1f)),
                    Discrete((AgentActions.BoostBranch, 1)));
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

                var action = MapOn(new RockSlotRoster(), Continuous((AgentActions.AimY, 1f)), Discrete());
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
