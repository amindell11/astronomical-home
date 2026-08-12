#if UNITY_EDITOR
using AI;
using Game.RLHarness;
using Movement.MPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the AgentPilot authoring: the prefab's brain must be a model-bearing InferenceBrain, and both agent-hosting pilots must author the policy-matched tracker settings the checkpoint was trained against.</summary>
    [Category("AI")]
    public class AgentPilotAuthoringEditModeTests
    {
        private const string AgentPilotPath = "Assets/Prefabs/Pilots/AgentPilot.prefab";
        private const string TestPilotPath = "Assets/Prefabs/Pilots/TestPilotMPC.prefab";

        [Test]
        public void AgentPilot_AuthorsInferenceBrainWithModel()
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(AgentPilotPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {AgentPilotPath}");

            var brain = pilot.GetComponent<InferenceBrain>();
            Assert.IsNotNull(brain, "AgentPilot must author an InferenceBrain");
            // Unity-null comparison: a broken asset ref deserializes as a fake-null wrapper Assert.IsNotNull cannot see.
            Assert.IsTrue(brain.Model != null, "InferenceBrain must reference a resolvable trained checkpoint");
        }

        [TestCase(AgentPilotPath)]
        [TestCase(TestPilotPath)]
        public void AgentHostPilot_NavigatorAuthorsPolicyMatchedTrackerSettings(string prefabPath)
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {prefabPath}");

            var settings = pilot.GetComponent<Navigator>().mpcSettings;
            Assert.IsNotNull(settings, "Agent-hosting pilots must author MpcSettings_AgentPilot");
            Assert.AreEqual(50f, settings.wVelTrack, "wVelTrack is part of the trained interface");
        }
    }
}
#endif
