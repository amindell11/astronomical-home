#if UNITY_EDITOR
using System.Collections;
using Game.RLHarness;
using NUnit.Framework;
using Tests.PlayMode.Common;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>Proves the gameplay composition end-to-end: an InferenceBrain installed on a production-spawned ship self-hosts its agent, acquires the tracked enemy, and receives decisions at the trained cadence.</summary>
    [Category("AI")]
    public class InferencePilotPlayModeTests : AIIntegrationFixture
    {
        [UnityTest]
        public IEnumerator InferenceBrain_SelfHosts_AndDecidesAtTrainedCadence()
        {
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(ShipAgentFactory.SmokeFixturePath);
            Assert.IsNotNull(model, "Smoke fixture missing");
            var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
            CreateAIShip(new Vector3(15f, 0f, 0f), team: 1);
            var brain = cmdrA.InstallBrain<InferenceBrain>();
            brain.ConfigureModel(model, 120f);

            yield return AsyncAssert.WaitUntil(
                () => brain.Agent && brain.Agent.DecisionsReceived >= 1,
                timeoutSec: 5f,
                failureMessage: "InferenceBrain never composed its agent / received a decision",
                useFixedUpdate: true);

            var before = brain.Agent.DecisionsReceived;
            for (var i = 0; i < 40; i++)
                yield return new WaitForFixedUpdate();
            var received = brain.Agent.DecisionsReceived - before;

            Assert.That(received, Is.InRange(3, 5),
                $"Expected ~1 decision per {ShipCombatPolicy.DecisionIntervalSteps} fixed steps over 40 steps, got {received}");
        }
    }
}
#endif
