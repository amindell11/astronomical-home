#if UNITY_EDITOR
using AI;
using AI.Context;
using Game.RLHarness;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the K1-2 velrebase seam: the extracted law cores stay byte-identical to the legacy formulas, the anchored pack carries the same numbers under the K1-1 sign pins, the readout counter runs at the 5 Hz recompute cadence, and the probe sampler's kinematics/tracking math on synthetic samples.</summary>
    [Category("AI")]
    public class RLVelRebaseEditModeTests
    {
        private const float Dt = 0.02f;

        // ---- Law cores: exact-float pins against the pre-extraction formulas ----

        private static Kinematics Kin(Vector2 pos, Vector2 vel) => new(pos, vel, 0f, 0f, 0f);

        [Test]
        public void OrbitVelocity_IsByteIdenticalToTheLegacyFormula()
        {
            var self = Kin(new Vector2(3.2f, -1.7f), new Vector2(4f, 2f));
            var enemy = Kin(new Vector2(-6.5f, 9.1f), new Vector2(-1f, 0.5f));
            const float orbitRadius = 15.3f;
            const int orbitDirection = -1;
            const float speedFraction = 0.55f;
            const float maxSpeed = 25f;

            // Verbatim copy of the pre-K1-2 OrbiterBrain.BuildDecision velocity math.
            var los = enemy.pos - self.pos;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;
            var tangent = orbitDirection * new Vector2(-losHat.y, losHat.x);
            var vTan = speedFraction * maxSpeed;
            var centripetal = 2.5f * vTan * vTan / Mathf.Max(r, 1f);
            var radial = (0.9f * (orbitRadius - r) - centripetal) * -losHat;
            var expected = Vector2.ClampMagnitude(vTan * tangent + radial, maxSpeed);

            var actual = ArchetypeLaws.OrbitVelocity(in self, in enemy, orbitRadius, orbitDirection,
                speedFraction, maxSpeed);
            Assert.AreEqual(expected.x, actual.x, "exact-float: the extraction must not reorder the law");
            Assert.AreEqual(expected.y, actual.y);
        }

        [Test]
        public void FleeVelocity_IsByteIdenticalToTheLegacyFormula()
        {
            var selfPos = new Vector2(2.4f, -8.8f);
            var threatPos = new Vector2(-3.1f, 4.4f);
            const float speedFraction = 0.85f;
            const float maxSpeed = 25f;
            const int jukeSign = -1;

            // Verbatim copy of the pre-K1-2 EvaderBrain.BuildDecision velocity math.
            var away = selfPos - threatPos;
            var fleeHat = away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.up;
            var dir = (fleeHat + 0.6f * jukeSign * new Vector2(-fleeHat.y, fleeHat.x)).normalized;
            var expected = speedFraction * maxSpeed * dir;

            var actual = ArchetypeLaws.FleeVelocity(selfPos, threatPos, jukeSign, speedFraction * maxSpeed);
            Assert.AreEqual(expected.x, actual.x, "exact-float: the extraction must not reorder the law");
            Assert.AreEqual(expected.y, actual.y);
        }

        // ---- Anchored pack: K1-1 sign pins on the same numbers ----

        [Test]
        public void ToAnchored_ClosingVelocity_IsPositiveRadial()
        {
            // Enemy dead ahead on +Y: a command straight at it is pure vr > 0.
            var anchored = VelocityRebase.ToAnchored(new Vector2(0f, 3f), Vector2.zero, new Vector2(0f, 10f));
            Assert.IsTrue(anchored.vel.armed);
            Assert.AreEqual(1f, anchored.vel.weight);
            Assert.That(anchored.vel.radialSpeed, Is.EqualTo(3f).Within(1e-5f), "vr > 0 closes along +losHat");
            Assert.That(anchored.vel.tangentialSpeed, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ToAnchored_CcwVelocity_IsPositiveTangential()
        {
            // Ship due south of the enemy: CCW around it moves +X there (the Cost.AnchoredVelocityRef pin, inverted).
            var anchored = VelocityRebase.ToAnchored(new Vector2(4f, 0f), Vector2.zero, new Vector2(0f, 10f));
            Assert.That(anchored.vel.tangentialSpeed, Is.EqualTo(4f).Within(1e-5f), "vt > 0 is CCW around the enemy");
            Assert.That(anchored.vel.radialSpeed, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ToAnchored_RoundTripsThroughTheResolverWithTheEnemyVelocityLead()
        {
            var law = new Vector2(3.5f, -2f);
            var selfPos = new Vector2(1f, 2f);
            var enemyPos = new Vector2(-7f, 6f);
            var anchored = VelocityRebase.ToAnchored(law, selfPos, enemyPos);

            var still = Cost.AnchoredVelocityRef(new float2(selfPos.x, selfPos.y),
                new float2(enemyPos.x, enemyPos.y), float2.zero,
                anchored.vel.radialSpeed, anchored.vel.tangentialSpeed);
            Assert.That(still.x, Is.EqualTo(law.x).Within(1e-4f),
                "against a stationary enemy the rebased command resolves back to the law's world command");
            Assert.That(still.y, Is.EqualTo(law.y).Within(1e-4f));

            var moving = Cost.AnchoredVelocityRef(new float2(selfPos.x, selfPos.y),
                new float2(enemyPos.x, enemyPos.y), new float2(2f, -1f),
                anchored.vel.radialSpeed, anchored.vel.tangentialSpeed);
            Assert.That(moving.x, Is.EqualTo(law.x + 2f).Within(1e-4f),
                "the same numbers under anchored semantics carry the enemy-velocity lead — deliberate (K1-2 brief)");
            Assert.That(moving.y, Is.EqualTo(law.y - 1f).Within(1e-4f));
        }

        // ---- Brain pack + readout cadence ----

        private sealed class StubStatus : IShipStatus
        {
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => Movement.Dynamics.Default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
        }

        private GameObject targetGo;
        private GameObject scoutGo;

        [SetUp]
        public void SetUp()
        {
            targetGo = new GameObject("VelRebaseTarget");
            scoutGo = new GameObject("VelRebaseScout");
        }

        [TearDown]
        public void TearDown()
        {
            if (targetGo) Object.DestroyImmediate(targetGo);
            if (scoutGo) Object.DestroyImmediate(scoutGo);
        }

        private ArchetypeBrain NewBrain(ArchetypeDrive drive)
        {
            var brain = scoutGo.AddComponent<ArchetypeBrain>();
            var shape = new OpponentDraw { desiredRange = 12f, speedFraction = 1f };
            brain.Configure(targetGo.AddComponent<Ship>(), OpponentArchetype.Aggressor, in shape, 1,
                Vector2.zero, 120f, drive);
            return brain;
        }

        [Test]
        public void Pack_OpenLoopLegacy_EmitsTheLawUnchangedAndDisarmsFire()
        {
            var brain = NewBrain(ArchetypeDrive.OpenLoopLegacy);

            var decision = brain.Pack(Vector2.zero, new Vector2(3f, -4f), engages: true);

            Assert.AreEqual(3f, decision.nav.planarVelocity.x, "exact-float: no border steer on the open-loop arms");
            Assert.AreEqual(-4f, decision.nav.planarVelocity.y);
            Assert.IsFalse(decision.nav.sentence.vel.armed);
            Assert.IsFalse(decision.primary.IsAuto, "a hit would perturb the paired enemy path");
            Assert.IsTrue(decision.nav.TryGetAnchorId(out _), "both arms keep the anchor for the facing channel");
        }

        [Test]
        public void Pack_OpenLoopAnchored_CarriesTheSameNumbersInThePolarChannel()
        {
            var brain = NewBrain(ArchetypeDrive.OpenLoopAnchored);

            // Target ship sits at the origin (default kinematics); law command from (0,-10) straight at it.
            var decision = brain.Pack(new Vector2(0f, -10f), new Vector2(0f, 5f), engages: true);

            Assert.IsTrue(decision.nav.sentence.vel.armed);
            Assert.That(decision.nav.sentence.vel.radialSpeed, Is.EqualTo(5f).Within(1e-5f));
            Assert.That(decision.nav.sentence.vel.tangentialSpeed, Is.EqualTo(0f).Within(1e-5f));
            Assert.AreEqual(1f, decision.nav.sentence.vel.weight);
            Assert.IsFalse(decision.nav.hasPlanarVelocity, "the polar channel replaces the world reference");
            Assert.IsFalse(decision.primary.IsAuto);
        }

        [Test]
        public void Pack_Production_KeepsTheBorderSteeredReferenceByteForByte()
        {
            var brain = NewBrain(ArchetypeDrive.Production);

            // Outward-bound command deep in the border margin so the steer visibly acts.
            var selfPos = new Vector2(100f, 0f);
            var law = new Vector2(20f, 1f);
            var decision = brain.Pack(selfPos, law, engages: true);

            var expected = ArchetypeSteering.BorderTangentSteer(selfPos, law,
                Vector2.zero, 120f, ArchetypeSteering.BorderMargin);
            Assert.AreEqual(expected.x, decision.nav.planarVelocity.x, "exact-float: the production pack IS Steered");
            Assert.AreEqual(expected.y, decision.nav.planarVelocity.y);
            Assert.IsTrue(decision.primary.IsAuto, "production fire authority is untouched");
        }

        [Test]
        public void ReadoutCounter_BumpsPerRecomputeNotPerDecide()
        {
            var brain = NewBrain(ArchetypeDrive.OpenLoopLegacy);
            var ctx = new AIContext(new StubStatus(), scoutGo.AddComponent<AI.Scout>());

            Assert.AreEqual(0, brain.TotalDecisions);
            for (var tick = 0; tick < 25; tick++)
                brain.Decide(ctx);

            Assert.AreEqual(3, brain.TotalDecisions, "5 Hz cadence: recomputes at ticks 0, 10, 20 only");
            var zero = Kin(Vector2.zero, Vector2.zero);
            var expected = RangerBrain.HoldRangeVelocity(in zero, in zero, 12f, Dynamics.Default.maxSpeed);
            Assert.AreEqual(expected, brain.LastCommand.worldVelocity, "the recompute's law flows into the readout");
        }

        // ---- Sampler math on synthetic samples ----

        private sealed class FakeVelReadout : IScriptedVelocityReadout
        {
            public ArchetypeDrive Drive { get; set; } = ArchetypeDrive.OpenLoopLegacy;
            public int TotalDecisions { get; private set; }
            public ScriptedVelocityCommand LastCommand { get; private set; }

            public void Decide(in ScriptedVelocityCommand command)
            {
                LastCommand = command;
                TotalDecisions++;
            }
        }

        private static Kinematics Moving(Vector2 pos, Vector2 vel, float yawRate = 0f) =>
            new(pos, vel, 0f, yawRate, 0f);

        [Test]
        public void Sampler_BinsTrackingErrorByRangeOnceACommandExists()
        {
            var readout = new FakeVelReadout();
            var sampler = new VelRebaseSampler(readout);
            var enemyAt5 = Moving(new Vector2(5f, 0f), Vector2.zero);

            // No decision yet: kinematics accumulate, tracking must not.
            sampler.Sample(Moving(Vector2.zero, new Vector2(1f, 0f)), enemyAt5, Dt);
            Assert.AreEqual(0, sampler.trackErrAnnulus.Count);

            readout.Decide(new ScriptedVelocityCommand(new Vector2(3f, 0f), 0f, 0f));
            sampler.Sample(Moving(Vector2.zero, new Vector2(1f, 0f)), enemyAt5, Dt);                       // range 5 → annulus, err 2
            sampler.Sample(Moving(Vector2.zero, new Vector2(1f, 0f)), Moving(new Vector2(2f, 0f), Vector2.zero), Dt);  // range 2 → under 3
            sampler.Sample(Moving(Vector2.zero, new Vector2(1f, 0f)), Moving(new Vector2(10f, 0f), Vector2.zero), Dt); // range 10 → beyond 8

            Assert.AreEqual(1, sampler.trackErrAnnulus.Count);
            Assert.That(sampler.trackErrAnnulus[0], Is.EqualTo(2f).Within(1e-5f));
            Assert.AreEqual(1, sampler.trackErrUnder3.Count);
            Assert.AreEqual(1, sampler.trackErrBeyond8.Count);
            Assert.AreEqual(1, sampler.Decisions);
            Assert.AreEqual(4, sampler.Steps);
        }

        [Test]
        public void Sampler_AnchoredArm_ReResolvesTheReferencePerStep()
        {
            var readout = new FakeVelReadout { Drive = ArchetypeDrive.OpenLoopAnchored };
            var sampler = new VelRebaseSampler(readout);
            readout.Decide(new ScriptedVelocityCommand(Vector2.zero, radialSpeed: 2f, tangentialSpeed: 0f));

            // Enemy on +Y drifting +X: vRef = enemyVel + 2·losHat = (1,2); a matching ship velocity is zero error.
            sampler.Sample(Moving(Vector2.zero, new Vector2(1f, 2f)),
                Moving(new Vector2(0f, 5f), new Vector2(1f, 0f)), Dt);
            Assert.That(sampler.trackErrAnnulus[0], Is.EqualTo(0f).Within(1e-4f));

            // Same command, enemy now on +X: the reference re-resolves without a new decision.
            sampler.Sample(Moving(Vector2.zero, new Vector2(1f, 2f)),
                Moving(new Vector2(5f, 0f), new Vector2(1f, 0f)), Dt);
            Assert.That(sampler.trackErrAnnulus[1], Is.EqualTo(Mathf.Sqrt(8f)).Within(1e-4f),
                "vRef = (3,0) against ship vel (1,2) → |(-2,2)|; the 50 Hz re-resolution is the instrument");
        }

        [Test]
        public void Sampler_CountsHeadingTurnReversalsAboveTheSpeedFloor()
        {
            var readout = new FakeVelReadout();
            var sampler = new VelRebaseSampler(readout);
            var enemy = Moving(new Vector2(5f, 0f), Vector2.zero);

            Vector2 Heading(float deg) => 2f * new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

            sampler.Sample(Moving(Vector2.zero, Heading(0f)), enemy, Dt);
            sampler.Sample(Moving(Vector2.zero, Heading(10f)), enemy, Dt);   // +10
            sampler.Sample(Moving(Vector2.zero, Heading(5f)), enemy, Dt);    // −5 → reversal 1
            sampler.Sample(Moving(Vector2.zero, Heading(15f)), enemy, Dt);   // +10 → reversal 2
            Assert.AreEqual(2, sampler.VelHeadingReversals);

            // Below the floor the heading is noise: the streak resets instead of counting a flip.
            sampler.Sample(Moving(Vector2.zero, new Vector2(0.01f, 0f)), enemy, Dt);
            sampler.Sample(Moving(Vector2.zero, Heading(5f)), enemy, Dt);
            sampler.Sample(Moving(Vector2.zero, Heading(10f)), enemy, Dt);
            Assert.AreEqual(2, sampler.VelHeadingReversals, "sub-floor samples break the delta chain");
        }

        [Test]
        public void Sampler_MeasuresLateralAccelAgainstThePriorHeading()
        {
            var readout = new FakeVelReadout();
            var sampler = new VelRebaseSampler(readout);
            var enemy = Moving(new Vector2(5f, 0f), Vector2.zero);

            sampler.Sample(Moving(Vector2.zero, new Vector2(2f, 0f)), enemy, dt: 0.1f);
            sampler.Sample(Moving(Vector2.zero, new Vector2(2f, 2f)), enemy, dt: 0.1f);

            Assert.AreEqual(1, sampler.LateralAccelSamples);
            Assert.That(sampler.AbsLateralAccelSum, Is.EqualTo(20f).Within(1e-3f),
                "accel (0,20) against prior heading +X is fully lateral");
        }

        [Test]
        public void Sampler_ZeroDecisionEpisodeZeroFillsTheRow()
        {
            var sampler = new VelRebaseSampler(new FakeVelReadout());
            var result = new EpisodeResult { simSeconds = 0f };

            var row = sampler.ToRow(in result, "Orbiter-legacy", "legacy");

            Assert.AreEqual(VelRebaseRow.SchemaId, row.schema);
            Assert.AreEqual("Orbiter-legacy", row.opponent);
            Assert.AreEqual("legacy", row.arm);
            Assert.AreEqual(0, row.decisions);
            Assert.AreEqual(0f, row.trackErrMeanAnnulus);
            Assert.AreEqual(0f, row.velHeadingReversalsPerSec);
        }

        [Test]
        public void Summary_PoolsSamplesAcrossEpisodesInsteadOfAveragingEpisodeStats()
        {
            var readout = new FakeVelReadout();
            var pool = new VelRebasePool();
            var enemy = Moving(new Vector2(5f, 0f), Vector2.zero);

            var first = new VelRebaseSampler(readout);
            readout.Decide(new ScriptedVelocityCommand(new Vector2(3f, 0f), 0f, 0f));
            first.Sample(Moving(Vector2.zero, new Vector2(-97f, 0f)), enemy, Dt);   // err 100
            var resultA = new EpisodeResult { simSeconds = 1f };
            first.DrainInto(pool, in resultA);

            var second = new VelRebaseSampler(readout);
            readout.Decide(new ScriptedVelocityCommand(new Vector2(3f, 0f), 0f, 0f));
            second.Sample(Moving(Vector2.zero, new Vector2(-7f, 0f)), enemy, Dt);   // errs 10, 20, 30
            second.Sample(Moving(Vector2.zero, new Vector2(-17f, 0f)), enemy, Dt);
            second.Sample(Moving(Vector2.zero, new Vector2(-27f, 0f)), enemy, Dt);
            var resultB = new EpisodeResult { simSeconds = 3f };
            second.DrainInto(pool, in resultB);

            var summary = VelRebaseSummary.Summarize("Aggressor-anchored", "anchored", pool);

            Assert.AreEqual(VelRebaseSummary.SchemaId, summary.schema);
            Assert.AreEqual("Aggressor-anchored", summary.opponent);
            Assert.AreEqual("anchored", summary.arm);
            Assert.AreEqual(2, summary.episodes);
            Assert.AreEqual(2f, summary.meanEpisodeSeconds);
            Assert.AreEqual(4, summary.stepsAnnulus);
            Assert.AreEqual(20f, summary.trackErrMedianAnnulus,
                "pooled p50 over {100,10,20,30} = 20; mean-of-episode-medians would say 60");
            Assert.AreEqual(40f, summary.trackErrMeanAnnulus, 1e-4f);
        }
    }
}
#endif
