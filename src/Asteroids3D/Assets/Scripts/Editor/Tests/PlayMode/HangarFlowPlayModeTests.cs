using System.Collections;
using Game.Session;
using NUnit.Framework;
using Player;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// Guards the hangar's non-interactive path: when presentation is off (headless/RL) the driver's
    /// <see cref="GameDriver.RunHangar"/> step must apply the standing loadout and finish on its
    /// own — never instantiate the screen or block waiting for a Launch click.
    /// </summary>
    [Category("UI")]
    public class HangarFlowPlayModeTests : PlayModeWorldFixture
    {
        private GameObject driverGo;
        private GameObject rigGo;

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(true);
            DestroyTestObject(driverGo);
            DestroyTestObject(rigGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator RunHangar_HeadlessOrNoPlayer_CompletesWithoutScreen()
        {
            GameSettings.SetPresentationEnabled(false);

            // Keep the driver inactive so its Awake/state-machine never runs — the hangar flow is
            // pumped in isolation. RequireComponent adds the sibling services on AddComponent.
            driverGo = new GameObject("TestDriver");
            driverGo.SetActive(false);
            var driver = driverGo.AddComponent<GameDriver>();

            // A bare rig: no player was built, no screen prefab assigned — both gate conditions hold.
            rigGo = new GameObject("TestRig");
            var rig = rigGo.AddComponent<SessionRig>();
            var session = new GameSession { Rig = rig };

            var finished = false;
            var step = driver.RunHangar(session);
            while (step.MoveNext())
                yield return step.Current;
            finished = true;

            Assert.IsTrue(finished, "RunHangar completed without waiting for a Launch click");
            Assert.IsNull(Object.FindFirstObjectByType<UI.HangarScreen>(),
                "no hangar screen was instantiated on the non-interactive path");
        }
    }
}
