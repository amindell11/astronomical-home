#if UNITY_EDITOR
using System.Collections;
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
    /// <summary>A hunt patrol's handback, without the hunt: a tactics pilot flown unaware on a steer, then woken
    /// and released the way the alarm does it, picks a tactic once it sees its target.</summary>
    [Category("AI")]
    public class SteerHandbackPlayModeTests : AIIntegrationFixture
    {
        private const int SteeredSteps = 50;
        private const int AcquireSteps = 200;

        private TacticsSettings settings;

        public override void SetUp()
        {
            base.SetUp();
            settings = ScriptableObject.CreateInstance<TacticsSettings>();
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(settings);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator SteeredUnaware_ThenWokenAndReleased_PicksATactic()
        {
            CreateAIShip(Vector3.zero, team: 0);
            var (_, patrol) = CreateAIShip(GamePlane.PlanePointToWorld(new Vector2(0f, 20f)), team: 1);
            var brain = patrol.InstallBrain<ArchetypeBrain>();
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("liveTargeting").boolValue = true;
            serialized.FindProperty("tactics").objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            patrol.Scout.Unaware = true;
            patrol.SteerTo(new Vector2(40f, 20f), 5f);
            for (var step = 0; step < SteeredSteps; step++) yield return new WaitForFixedUpdate();
            Assert.IsNull(brain.CurrentTactic, "a steered pilot's brain doesn't decide");

            patrol.Scout.Unaware = false;
            patrol.ClearSteer();
            for (var step = 0; !brain.CurrentTactic.HasValue; step++)
            {
                Assert.Less(step, AcquireSteps, "the released pilot picks a tactic");
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
#endif
