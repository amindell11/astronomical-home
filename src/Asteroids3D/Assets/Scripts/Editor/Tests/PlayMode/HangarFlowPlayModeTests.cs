using System.Collections;
using NUnit.Framework;
using Player;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// Guards the hangar's non-interactive path: when presentation is off (headless/RL) the
    /// <see cref="PlayerRig.RunHangar"/> step must apply the standing loadout and finish on its own —
    /// never instantiate the screen or block waiting for a Launch click.
    /// </summary>
    public class HangarFlowPlayModeTests : PlayModeWorldFixture
    {
        private GameObject rigGo;

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(true);
            DestroyTestObject(rigGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator RunHangar_HeadlessOrNoPlayer_CompletesWithoutScreen()
        {
            GameSettings.SetPresentationEnabled(false);

            // A bare rig: no player was built, no screen prefab assigned — both gate conditions hold.
            rigGo = new GameObject("TestRig");
            var rig = rigGo.AddComponent<PlayerRig>();

            var finished = false;
            IEnumerator Run()
            {
                yield return rig.RunHangar();
                finished = true;
            }
            rig.StartCoroutine(Run());

            // One frame must be enough — the pass-through path never waits on input.
            yield return null;
            yield return null;

            Assert.IsTrue(finished, "RunHangar completed without waiting for a Launch click");
            Assert.IsNull(Object.FindFirstObjectByType<UI.HangarScreen>(),
                "no hangar screen was instantiated on the non-interactive path");
        }
    }
}
