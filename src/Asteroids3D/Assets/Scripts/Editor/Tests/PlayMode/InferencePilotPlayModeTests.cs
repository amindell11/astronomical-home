#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Tests.PlayMode.Common;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using RL.Episodes.Compositions;
using RL.Runtime;

namespace Tests.PlayMode
{
    /// <summary>Proves the gameplay composition end-to-end: an InferenceBrain installed on a production-spawned ship self-hosts its agent, acquires the tracked enemy, and receives decisions at the trained cadence.</summary>
    [Category("AI")]
    public class InferencePilotPlayModeTests : AIIntegrationFixture
    {
        // Counted in fixed steps: cold init stalls the main thread, so contention cannot spend this budget.
        private const int WarmUpStepBudget = 250;

        [UnitySetUp]
        public IEnumerator WarmAcademyAndModel()
        {
            // [UnitySetUp] runs before [SetUp], so the warm-up brackets its own throwaway world.
            SetUp();
            try
            {
                var started = Time.realtimeSinceStartup;
                var brain = InstallPilotAgainstEnemy();
                var steps = 0;
                while (!HasDecided(brain))
                {
                    if (steps++ >= WarmUpStepBudget)
                        Assert.Fail($"Warm-up pilot received no decision within {WarmUpStepBudget} fixed steps");
                    yield return new WaitForFixedUpdate();
                }
                Debug.Log($"[InferencePilot warm-up] cold init to first decision: {Time.realtimeSinceStartup - started:F3}s realtime over {steps} fixed steps");
            }
            finally
            {
                TearDown();
            }
        }

        [UnityTest]
        public IEnumerator InferenceBrain_SelfHosts_AndDecidesAtTrainedCadence()
        {
            var brain = InstallPilotAgainstEnemy();

            yield return AsyncAssert.WaitUntil(
                () => HasDecided(brain),
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

        private InferenceBrain InstallPilotAgainstEnemy()
        {
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(ShipAgentFactory.SmokeFixturePath);
            Assert.IsNotNull(model, "Smoke fixture missing");
            var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
            CreateAIShip(new Vector3(15f, 0f, 0f), team: 1);
            var brain = cmdrA.InstallBrain<InferenceBrain>();
            brain.ConfigureModel(model, 120f);
            return brain;
        }

        private static bool HasDecided(InferenceBrain brain) => brain.Agent && brain.Agent.DecisionsReceived >= 1;
    }
}
#endif
