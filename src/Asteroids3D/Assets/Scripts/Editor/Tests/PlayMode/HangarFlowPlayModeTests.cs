using System.Collections;
using Game;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Guards the hangar's non-interactive path: when presentation is off (headless/RL) the host's
    /// <see cref="GameHost.RunHangar"/> step must apply the standing loadout and finish on its
    /// own — never instantiate the screen or block waiting for a Launch click.
    /// </summary>
    [Category("UI")]
    public class HangarFlowPlayModeTests : PlayModeWorldFixture
    {
        private GameObject hostGo;
        private GameObject rigGo;

        public override void TearDown()
        {
            DestroyTestObject(hostGo);
            DestroyTestObject(rigGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator RunHangar_HeadlessOrNoPlayer_CompletesWithoutScreen()
        {
            // Keep the host inactive so its Awake and flow never run — the hangar flow is
            // pumped in isolation. RequireComponent adds the sibling services on AddComponent.
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();

            // A bare rig: no player was built, no screen prefab assigned — both gate conditions hold.
            rigGo = new GameObject("TestRig");
            var rig = rigGo.AddComponent<PlayerRig>();

            var finished = false;
            var step = host.RunHangar(rig, presentationEnabled: false);
            while (step.MoveNext())
                yield return step.Current;
            finished = true;

            Assert.IsTrue(finished, "RunHangar completed without waiting for a Launch click");
            Assert.IsNull(Object.FindFirstObjectByType<UI.HangarScreen>(),
                "no hangar screen was instantiated on the non-interactive path");
        }
    }
}
