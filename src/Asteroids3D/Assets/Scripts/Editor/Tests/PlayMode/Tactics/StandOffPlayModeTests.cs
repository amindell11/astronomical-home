#if UNITY_EDITOR
using System.Collections;
using AI;
using AI.Strategy;
using AI.Tactics;
using NUnit.Framework;
using RL.Opponents;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Tactics
{
    /// <summary>A tactics pilot standing off behind a target that faces away, driven off a real ship's context. PlayMode because the situation needs real ships the scout can see.</summary>
    [Category("AI")]
    public class StandOffPlayModeTests : AIIntegrationFixture
    {
        private const int AcquireSteps = 200;
        private const int RecomputeTicks = 10;

        private TacticsSettings settings;
        private GameObject brainHost;

        public override void SetUp()
        {
            base.SetUp();
            settings = ScriptableObject.CreateInstance<TacticsSettings>();
            // Both sides sit inside a cone this wide, so neither is open and a blocked lane stands off.
            settings.exposureCone = 200f;
            // Long enough that the stand off still holds once the lane clears.
            settings.minimumHold = 100f;
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(brainHost);
            Object.DestroyImmediate(settings);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator StandOff_TargetTurnedAway_HoldsFireWithAnAllyInTheLane_FiresOnceItClears()
        {
            var (target, _) = CreateAIShip(Vector3.zero, team: 0);
            yield return new WaitForFixedUpdate();
            var tail = -target.Kinematics.Forward;
            var (_, me) = CreateAIShip(GamePlane.PlanePointToWorld(target.Kinematics.pos + tail * 16f), team: 1);
            var (ally, _) = CreateAIShip(GamePlane.PlanePointToWorld(target.Kinematics.pos + tail * 8f), team: 1);
            for (var step = 0; me.context?.Combat.Enemy != target; step++)
            {
                Assert.Less(step, AcquireSteps, "the standing-off ship acquires the target");
                yield return new WaitForFixedUpdate();
            }

            brainHost = new GameObject("StandOffBrain");
            var brain = brainHost.AddComponent<ArchetypeBrain>();
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("liveTargeting").boolValue = true;
            serialized.FindProperty("tactics").objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var blocked = brain.Decide(me.context);
            Assert.AreEqual(Tactic.StandOff, brain.CurrentTactic, "an ally in the lane with no open side stands off");
            Assert.IsTrue(blocked.HasValue);
            Assert.IsFalse(blocked.Value.engagePrimary, "the target faces away, but an ally is in the lane");

            ally.gameObject.SetActive(false);
            yield return new WaitForFixedUpdate();
            yield return null;
            BrainDecision? clear = null;
            for (var tick = 0; tick < RecomputeTicks; tick++) clear = brain.Decide(me.context);

            Assert.AreEqual(Tactic.StandOff, brain.CurrentTactic);
            Assert.IsTrue(clear.HasValue);
            Assert.IsTrue(clear.Value.engagePrimary, "the target faces away and the lane is clear");
        }
    }
}
#endif
