#if UNITY_EDITOR
using System.Collections;
using AI;
using AI.Pilots;
using AI.Strategy;
using AI.Tactics;
using NUnit.Framework;
using Ships;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Tactics
{
    /// <summary>A hurt ship flown off a real context. PlayMode because the situation needs real ships the scout can see.</summary>
    [Category("AI")]
    public class WoundedPlayModeTests : AIIntegrationFixture
    {
        private const int AcquireSteps = 200;

        private TacticsSettings settings;
        private GameObject brainHost;

        public override void SetUp()
        {
            base.SetUp();
            settings = ScriptableObject.CreateInstance<TacticsSettings>();
            settings.minimumHold = 0f;
            settings.hitWithin = 1000f;
            settings.woundedBelow = 100f;
            settings.shieldBack = 100f;
            settings.woundedEnds = WoundedEnds.WhenShieldIsBack;
            settings.attackRunMargin = 0f;
            settings.fallBackMargin = 0f;
        }

        public override void TearDown()
        {
            if (brainHost) Object.DestroyImmediate(brainHost);
            Object.DestroyImmediate(settings);
            base.TearDown();
        }

        private ArchetypeBrain Install(Ship target, bool liveTargeting, in OpponentDraw shape)
        {
            brainHost = new GameObject("WoundedBrain");
            var brain = brainHost.AddComponent<ArchetypeBrain>();
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("liveTargeting").boolValue = liveTargeting;
            serialized.FindProperty("tactics").objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            brain.Configure(target, OpponentArchetype.Aggressor, in shape, jukeSeed: 1,
                target.Kinematics.pos, borderRadius: 500f);
            return brain;
        }

        [UnityTest]
        public IEnumerator RetreatCount_SurvivesALiveTargetChange_AndClearsOnResetState()
        {
            settings.wounded = WoundedResponse.BreakOff;
            settings.commits = true;
            settings.retreats = 1;

            var (first, _) = CreateAIShip(Vector3.zero, team: 0);
            yield return new WaitForFixedUpdate();
            var nose = first.Kinematics.Forward;
            var origin = first.Kinematics.pos;
            var (meShip, me) = CreateAIShip(GamePlane.PlanePointToWorld(origin + nose * 8f), team: 1);
            for (var step = 0; me.context?.Combat.Enemy != first; step++)
            {
                Assert.Less(step, AcquireSteps, "the ship acquires the first target");
                yield return new WaitForFixedUpdate();
            }

            DealDamage(meShip, 5f);
            yield return new WaitForFixedUpdate();
            Assert.Less(meShip.ShieldPct, 1f, "test premise: the hit took shield");

            var shape = new OpponentDraw { speedFraction = 0.8f, desiredRange = 12f };
            var brain = Install(first, liveTargeting: true, in shape);
            brain.Decide(me.context);
            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic, "the first retreat");

            var (second, _) = CreateAIShip(GamePlane.PlanePointToWorld(origin - nose * 6f), team: 0);
            Assert.Less(Vector2.Distance(meShip.Kinematics.pos, second.Kinematics.pos), me.Scout.nearbyShipRadius,
                "test premise: the second target is inside the scout");
            first.gameObject.SetActive(false);
            for (var step = 0; me.context.Combat.Enemy != second; step++)
            {
                Assert.Less(step, AcquireSteps, "the ship acquires the second target");
                yield return new WaitForFixedUpdate();
            }
            Assert.Less(meShip.ShieldPct, 1f, "test premise: the shield is still down");
            Assert.LessOrEqual(me.context.SecondsSinceHit, settings.hitWithin);

            brain.Decide(me.context);
            Assert.AreNotEqual(Tactic.BreakOff, brain.CurrentTactic, "the spent retreat survived the target change");

            brain.ResetState();
            brain.Decide(me.context);
            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic, "ResetState clears the retreat count");
        }

        [UnityTest]
        public IEnumerator FallBack_HoldsAtTheFarEdgeOfItsOwnGunRange_AndEngagesOnAClearLaneOnly()
        {
            settings.wounded = WoundedResponse.FallBack;

            var (target, _) = CreateAIShip(Vector3.zero, team: 0);
            yield return new WaitForFixedUpdate();
            var tail = -target.Kinematics.Forward;
            var (meShip, me) = CreateAIShip(GamePlane.PlanePointToWorld(target.Kinematics.pos + tail * 16f), team: 1);
            var (ally, _) = CreateAIShip(GamePlane.PlanePointToWorld(target.Kinematics.pos + tail * 8f), team: 1);
            for (var step = 0; me.context?.Combat.Enemy != target; step++)
            {
                Assert.Less(step, AcquireSteps, "the ship acquires the target");
                yield return new WaitForFixedUpdate();
            }

            DealDamage(meShip, 5f);
            yield return new WaitForFixedUpdate();
            Assert.Less(meShip.ShieldPct, 1f, "test premise: the hit took shield");

            var shape = new OpponentDraw { speedFraction = 0.8f, desiredRange = 12f };
            var brain = Install(target, liveTargeting: false, in shape);
            var blocked = brain.Decide(me.context);

            var myRange = meShip.Weapons.Primary.FireRange;
            var gunRange = me.context.HoldRange ?? shape.desiredRange;
            var fallBackRange = Mathf.Max(myRange - settings.fallBackMargin, gunRange + settings.attackRunMargin);
            Assert.AreEqual(myRange, fallBackRange, "the attack run sits inside the gun range, so the hold is its far edge");
            var self = meShip.Kinematics;
            var them = target.Kinematics;
            var speed = shape.speedFraction * meShip.Dynamics.maxSpeed;
            Assert.AreEqual(Tactic.FallBack, brain.CurrentTactic);
            Assert.IsTrue(blocked.HasValue);
            Assert.AreEqual(RangerBrain.HoldRangeVelocity(in self, in them, fallBackRange, speed),
                blocked.Value.nav.planarVelocity);
            Assert.IsFalse(blocked.Value.engagePrimary, "an ally is in the lane");
            Assert.IsFalse(blocked.Value.engageSecondary, "an ally is in the lane");

            ally.gameObject.SetActive(false);
            yield return new WaitForFixedUpdate();
            yield return null;
            BrainDecision? clear = null;
            for (var tick = 0; tick < 10; tick++) clear = brain.Decide(me.context);

            Assert.AreEqual(Tactic.FallBack, brain.CurrentTactic);
            Assert.IsTrue(clear.HasValue);
            Assert.IsTrue(clear.Value.engagePrimary, "the lane is clear");
            Assert.IsTrue(clear.Value.engageSecondary, "the lane is clear");
        }
    }
}
#endif
