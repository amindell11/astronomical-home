using AI;
using Combat;
using Combat.Weapons;
using Movement;
using NUnit.Framework;
using UnityEngine;
using RL.Episodes;
using RL.Reward;
using Substrate;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("AI")]
    public class RewardLayerEditModeTests
    {
        private static RewardSpec Spec => RewardSpec.Default;

        private static CombatSnapshot Snapshot(
            bool myAlive = true, bool enemyAlive = true,
            float myDistFromCenter = 0f, float enemyDistFromCenter = 0f) => new()
        {
            myAlive = myAlive,
            enemyAlive = enemyAlive,
            myDistFromCenter = myDistFromCenter,
            enemyDistFromCenter = enemyDistFromCenter,
        };

        [Test]
        public void Outcome_BaselineDeath_IsWin()
        {
            var spec = Spec;
            var verdict = EpisodeRules.Evaluate(Snapshot(enemyAlive: false), in spec);
            Assert.AreEqual(EpisodeOutcome.Win, verdict.outcome);
        }

        [Test]
        public void Outcome_AgentDeath_IsLoss()
        {
            var spec = Spec;
            var verdict = EpisodeRules.Evaluate(Snapshot(myAlive: false), in spec);
            Assert.AreEqual(EpisodeOutcome.Loss, verdict.outcome);
            Assert.IsFalse(verdict.mutualKill);
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

        private sealed class NarrowConeWeapon : WeaponComponent
        {
            public override bool InEnvelope(in TargetingContext context) =>
                context.hasLineOfSight
                && context.distanceToTarget <= 30f
                && context.angleToTarget <= 5f;

            public override Combat.Projectiles.ProjectileBase Fire(UnityEngine.Vector3 targetPoint, Substrate.Services.Projectiles.IProjectileService projectiles) => null;
        }

        [Test]
        public void Envelope_EvaluatesAtGunnerInterceptLead_NotTargetCenter()
        {
            var shooterPlanePos = new Vector2(500f, 500f);
            var shooterGo = new GameObject("EnvelopeShooter");
            try
            {
                shooterGo.transform.position = GamePlane.PlanePointToWorld(shooterPlanePos);
                var weapon = shooterGo.AddComponent<NarrowConeWeapon>();
                var pose = new Kinematics(shooterPlanePos, Vector2.zero, 0f, 0f, 0f);
                var sight = new Gunsight(weapon, () => pose);

                var targetPos = shooterPlanePos + new Vector2(-4f, 20f);
                var targetVel = new Vector2(4f, 0f);
                var lead = Gunner.AimPoint(in pose, targetPos, targetVel, projectileSpeed: 20f);

                Assert.IsFalse(sight.InEnvelope(GamePlane.PlanePointToWorld(targetPos)),
                    "A crossing target's CENTER sits outside the aim cone — center-testing must miss");
                Assert.IsTrue(sight.InEnvelope(GamePlane.PlanePointToWorld(lead)),
                    "The gunner fires at the intercept lead, which is on the nose — lead-testing must hit");
            }
            finally
            {
                Object.DestroyImmediate(shooterGo);
            }
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
