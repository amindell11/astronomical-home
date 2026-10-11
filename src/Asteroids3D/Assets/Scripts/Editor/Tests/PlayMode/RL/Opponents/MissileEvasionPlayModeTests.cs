using System.Collections;
using AI;
using AI.Context;
using AI.Strategy;
using Combat.Projectiles;
using NUnit.Framework;
using RL.Opponents;
using Ships;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>An Aggressor's decisions while a live missile tracks its ship, which holds still with its nose square to the missile's approach and its enemy behind it, so the escape heading is the nose and the law points the other way.</summary>
    [Category("AI")]
    public class MissileEvasionPlayModeTests : PlayModeWorldFixture
    {
        private const float MissileRange = 20f;
        private const float EnemyRange = 30f;
        private const float WatchSeconds = 1f;

        private static readonly OpponentDraw Shape = new() { speedFraction = 0.85f, desiredRange = 10f };

        private Ship self;
        private Ship enemy;
        private Missile missile;
        private GameObject rock;
        private GameObject brainHost;
        private ArchetypeBrain brain;
        private AIContext ctx;
        private Vector2 nose;
        private int reactionStep;
        private BrainDecision evading;

        protected override bool AccelerateTime => true;

        public override void TearDown()
        {
            DestroyTestObject(missile);
            DestroyTestObject(rock);
            DestroyTestObject(brainHost);
            DestroyTestObject(self);
            DestroyTestObject(enemy);
            SimplePool<Missile>.Clear();
            base.TearDown();
        }

        private IEnumerator Spawn(bool evade)
        {
            self = ShipTestFactory.CreateDefaultShipAt(Vector3.zero, Quaternion.identity, Projectiles, team: 1);
            yield return new WaitForFixedUpdate();
            var pose = self.Kinematics;
            nose = pose.Forward;
            Assert.That(self.Dynamics.maxSpeed, Is.GreaterThan(0f), "Precondition: the ship has an engine");

            enemy = ShipTestFactory.CreateDefaultShipAt(GamePlane.PlanePointToWorld(pose.pos - EnemyRange * nose),
                Quaternion.identity, Projectiles, team: 0);

            brainHost = new GameObject("EvasionBrain");
            brain = brainHost.AddComponent<ArchetypeBrain>();
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("evadeMissiles").boolValue = evade;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var shape = Shape;
            brain.Configure(enemy, OpponentArchetype.Aggressor, in shape, jukeSeed: 0, Vector2.zero, borderRadius: 500f);
            ctx = new AIContext(self, brainHost.AddComponent<Scout>());

            var right = new Vector2(nose.y, -nose.x);
            var launchDir = GamePlane.PlaneDirToWorld(-right);
            var go = new GameObject("TestMissile");
            go.transform.SetPositionAndRotation(GamePlane.PlanePointToWorld(pose.pos + MissileRange * right),
                Quaternion.LookRotation(GamePlane.Normal, launchDir));
            go.AddComponent<Rigidbody>().useGravity = false;
            go.AddComponent<CapsuleCollider>().isTrigger = true;
            missile = go.AddComponent<Missile>();
            missile.Configure(maxDistance: 200f, maxLifetime: 10f);
            missile.Initialize(enemy);
            missile.Launch(launchDir, go.transform.position + launchDir);
            missile.SetTarget(self);
        }

        private BrainDecision Decide()
        {
            var decision = brain.Decide(ctx);
            Assert.IsTrue(decision.HasValue, "The Aggressor has a live target");
            return decision.Value;
        }

        private static float Facing(in BrainDecision decision) => decision.nav.sentence.aim.weight;

        private Vector2 Law() => RangerBrain.HoldRangeVelocity(self.Kinematics, enemy.Kinematics, Shape.desiredRange,
            Shape.speedFraction * self.Dynamics.maxSpeed);

        private void AssertLaw(in BrainDecision decision, string when)
        {
            Assert.That(Vector2.Distance(decision.nav.planarVelocity, Law()), Is.LessThan(1e-3f),
                $"Velocity left the Aggressor law {when}");
            Assert.That(Facing(decision), Is.EqualTo(1f), $"Facing hold eased {when}");
            Assert.That(decision.boost, Is.False, $"Boosted {when}");
        }

        private IEnumerator UntilEvading()
        {
            for (reactionStep = 0; reactionStep * Time.fixedDeltaTime < WatchSeconds; reactionStep++)
            {
                evading = Decide();
                if (Facing(evading) < 1f) yield break;
                AssertLaw(evading, $"at step {reactionStep}, before the pilot reacted");
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("The pilot never reacted to the tracking missile");
        }

        [UnityTest]
        public IEnumerator EvasionOn_AfterReactionDelay_BlendsTowardEscapeEasesFacingAndBoosts()
        {
            yield return Spawn(evade: true);

            yield return UntilEvading();

            var reactionDelay = new SerializedObject(brain).FindProperty("reactionDelay").floatValue;
            Assert.That(reactionStep * Time.fixedDeltaTime, Is.GreaterThanOrEqualTo(reactionDelay),
                "The pilot reacted before the reaction delay ran");
            var urgency = 1f - Facing(evading);
            var blended = Vector2.Lerp(Law(), self.Dynamics.maxSpeed * nose, urgency);
            Assert.That(Vector2.Distance(evading.nav.planarVelocity, blended), Is.LessThan(0.05f),
                "Velocity is not the law blended toward the escape by the urgency the facing hold eased by");
            Assert.That(evading.boost, Is.True, "No boost with the nose on the escape heading");
        }

        [UnityTest]
        public IEnumerator EvasionOff_TrackingMissile_LeavesTheLaw()
        {
            yield return Spawn(evade: false);

            for (var step = 0; step * Time.fixedDeltaTime < WatchSeconds; step++)
            {
                AssertLaw(Decide(), $"at step {step} with evasion off");
                yield return new WaitForFixedUpdate();
            }
            Assert.That(missile.IsTracking, Is.True, "Precondition: the missile tracked the whole watch");
        }

        [UnityTest]
        public IEnumerator LostTrack_ReturnsToTheLaw()
        {
            yield return Spawn(evade: true);
            yield return UntilEvading();

            var missilePos = missile.Kinematics.pos;
            rock = new GameObject("Rock") { layer = LayerIds.Asteroid };
            rock.transform.position = GamePlane.PlanePointToWorld((missilePos + self.Kinematics.pos) * 0.5f);
            rock.AddComponent<SphereCollider>();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(missile.IsTracking, Is.False, "Precondition: the rock broke the missile's track");
            Assert.That(self.TrackingMissiles, Is.Empty);

            var decision = Decide();
            for (var step = 0; Facing(decision) < 1f; step++)
            {
                Assert.That(step * Time.fixedDeltaTime, Is.LessThan(WatchSeconds), "The pilot kept evading a lost track");
                yield return new WaitForFixedUpdate();
                decision = Decide();
            }
            AssertLaw(decision, "after the track was lost");
        }
    }
}
