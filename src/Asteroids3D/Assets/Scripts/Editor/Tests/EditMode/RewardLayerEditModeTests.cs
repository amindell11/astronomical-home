using Game.RLHarness;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("AI")]
    public class RewardLayerEditModeTests
    {
        private static RewardSpec Spec => RewardSpec.Default;

        private static CombatSnapshot Snapshot(
            float myPool = 200f, float enemyPool = 200f,
            float myMaxPool = 200f, float enemyMaxPool = 200f,
            bool myAlive = true, bool enemyAlive = true,
            bool inMyEnvelope = false, bool inEnemyEnvelope = false,
            float myDistFromCenter = 0f, float enemyDistFromCenter = 0f) => new()
        {
            myPool = myPool,
            enemyPool = enemyPool,
            myMaxPool = myMaxPool,
            enemyMaxPool = enemyMaxPool,
            myAlive = myAlive,
            enemyAlive = enemyAlive,
            inMyEnvelope = inMyEnvelope,
            inEnemyEnvelope = inEnemyEnvelope,
            myDistFromCenter = myDistFromCenter,
            enemyDistFromCenter = enemyDistFromCenter,
        };

        [Test]
        public void Dense_EnemyDamageRewards_OwnDamagePenalizes_NormalizedPerShip()
        {
            var prev = Snapshot(myPool: 200f, enemyPool: 100f, myMaxPool: 200f, enemyMaxPool: 100f);
            var next = Snapshot(myPool: 150f, enemyPool: 75f, myMaxPool: 200f, enemyMaxPool: 100f);

            var reward = RewardTerms.PoolDifferential(in prev, in next, 2f);

            const float expected = 2f * (25f / 100f) - 2f * (50f / 200f);
            Assert.AreEqual(expected, reward, 1e-6f);
        }

        [Test]
        public void Dense_ShieldRegenIsNegativeEnemyLoss()
        {
            var prev = Snapshot(enemyPool: 100f);
            var next = Snapshot(enemyPool: 120f);

            Assert.AreEqual(-0.1f, RewardTerms.PoolDifferential(in prev, in next, 1f), 1e-6f);
        }

        [Test]
        public void Dense_DeltaSampling_TelescopesAcrossIntermediateSnapshots()
        {
            var a = Snapshot(myPool: 200f, enemyPool: 200f);
            var b = Snapshot(myPool: 170f, enemyPool: 140f);
            var c = Snapshot(myPool: 90f, enemyPool: 110f);

            var stepped = RewardTerms.PoolDifferential(in a, in b, 1f)
                          + RewardTerms.PoolDifferential(in b, in c, 1f);
            var direct = RewardTerms.PoolDifferential(in a, in c, 1f);

            Assert.AreEqual(direct, stepped, 1e-6f);
        }

        [Test]
        public void EnvelopePhi_SignsFollowSeekAndDeny()
        {
            var spec = Spec;
            Assert.AreEqual(spec.envelopeK1,
                PotentialShaping.EnvelopePhi(Snapshot(inMyEnvelope: true), in spec), 1e-6f);
            Assert.AreEqual(-spec.envelopeK2,
                PotentialShaping.EnvelopePhi(Snapshot(inEnemyEnvelope: true), in spec), 1e-6f);
            Assert.AreEqual(spec.envelopeK1 - spec.envelopeK2,
                PotentialShaping.EnvelopePhi(Snapshot(inMyEnvelope: true, inEnemyEnvelope: true), in spec), 1e-6f);
        }

        [Test]
        public void ShapingStep_IsGammaPhiNextMinusPhiPrev()
        {
            Assert.AreEqual(0.99f * 0.3f - 0.5f, PotentialShaping.Step(0.5f, 0.3f, 0.99f, terminal: false), 1e-6f);
        }

        [Test]
        public void ShapingStep_TerminalForcesPhiNextToZero()
        {
            Assert.AreEqual(-0.5f, PotentialShaping.Step(0.5f, 123f, 0.99f, terminal: true), 1e-6f);
        }

        [Test]
        public void BorderRamp_ZeroInsideSoftRadius_OneAtBoundary_Monotone()
        {
            const float radius = 100f;
            const float soft = 0.8f;

            Assert.AreEqual(0f, PotentialShaping.BorderRamp(0f, radius, soft));
            Assert.AreEqual(0f, PotentialShaping.BorderRamp(80f, radius, soft));
            Assert.AreEqual(0.25f, PotentialShaping.BorderRamp(90f, radius, soft), 1e-6f);
            Assert.AreEqual(1f, PotentialShaping.BorderRamp(100f, radius, soft), 1e-6f);
            Assert.Greater(PotentialShaping.BorderRamp(110f, radius, soft), 1f);
        }

        [Test]
        public void BorderPhi_IsNegativeRampWeighted()
        {
            var spec = Spec;
            var atBoundary = Snapshot(myDistFromCenter: spec.arenaRadius);
            Assert.AreEqual(-spec.borderKb, PotentialShaping.BorderPhi(in atBoundary, in spec), 1e-6f);
        }

        [Test]
        public void Outcome_BaselineDeath_IsWin()
        {
            var spec = Spec;
            var verdict = EpisodeRules.Evaluate(Snapshot(enemyAlive: false), in spec);
            Assert.AreEqual(EpisodeOutcome.Win, verdict.outcome);
            Assert.AreEqual(1f, RewardTerms.Outcome(verdict.outcome));
        }

        [Test]
        public void Outcome_AgentDeath_IsLoss()
        {
            var spec = Spec;
            var verdict = EpisodeRules.Evaluate(Snapshot(myAlive: false), in spec);
            Assert.AreEqual(EpisodeOutcome.Loss, verdict.outcome);
            Assert.IsFalse(verdict.mutualKill);
            Assert.AreEqual(-1f, RewardTerms.Outcome(verdict.outcome));
        }

        [Test]
        public void Outcome_MutualKill_IsLossWithFlag()
        {
            var spec = Spec;
            var verdict = EpisodeRules.Evaluate(Snapshot(myAlive: false, enemyAlive: false), in spec);
            Assert.AreEqual(EpisodeOutcome.Loss, verdict.outcome);
            Assert.IsTrue(verdict.mutualKill);
        }

        [Test]
        public void Outcome_AgentOutOfBounds_IsLoss()
        {
            var spec = Spec;
            var verdict = EpisodeRules.Evaluate(Snapshot(myDistFromCenter: spec.arenaRadius + 1f), in spec);
            Assert.AreEqual(EpisodeOutcome.Loss, verdict.outcome);
            Assert.IsTrue(verdict.agentExitedBounds);
        }

        [Test]
        public void Outcome_BaselineOutOfBounds_IsDrawWithAnomalyFlag()
        {
            var spec = Spec;
            var verdict = EpisodeRules.Evaluate(Snapshot(enemyDistFromCenter: spec.arenaRadius + 1f), in spec);
            Assert.AreEqual(EpisodeOutcome.Draw, verdict.outcome);
            Assert.IsTrue(verdict.baselineExitedBounds);
            Assert.AreEqual(0f, RewardTerms.Outcome(verdict.outcome));
        }

        [Test]
        public void Outcome_DeathBeatsOutOfBounds()
        {
            var spec = Spec;
            var verdict = EpisodeRules.Evaluate(
                Snapshot(enemyAlive: false, myDistFromCenter: spec.arenaRadius + 1f), in spec);
            Assert.AreEqual(EpisodeOutcome.Win, verdict.outcome);
        }

        [Test]
        public void Outcome_BothAliveInsideBounds_IsUnresolved()
        {
            var spec = Spec;
            Assert.AreEqual(EpisodeOutcome.Unresolved, EpisodeRules.Evaluate(Snapshot(), in spec).outcome);
        }

        [Test]
        public void Poses_ReplayFromSeedAndIndex_AndRespectSeparationBand()
        {
            var spec = Spec;
            var a = EpisodePoses.Derive(in spec, 7, Vector2.zero);
            var b = EpisodePoses.Derive(in spec, 7, Vector2.zero);
            Assert.AreEqual(a.agentPos, b.agentPos);
            Assert.AreEqual(a.baselinePos, b.baselinePos);
            Assert.AreEqual(a.agentRotDeg, b.agentRotDeg);
            Assert.AreEqual(a.baselineRotDeg, b.baselineRotDeg);

            var c = EpisodePoses.Derive(in spec, 8, Vector2.zero);
            Assert.AreNotEqual(a.agentPos, c.agentPos);

            var separation = (a.baselinePos - a.agentPos).magnitude;
            Assert.GreaterOrEqual(separation, spec.minSeparation - 1e-3f);
            Assert.LessOrEqual(separation, spec.maxSeparation + 1e-3f);
        }
    }
}
