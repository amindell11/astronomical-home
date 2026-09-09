#if UNITY_EDITOR
using System.Collections;
using Game.Sessions;
using NUnit.Framework;
using Player;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// The "unplug the game" proof: a <see cref="Session"/> composes, applies the standing loadout
    /// through its rig, and tears down with NO host above it and no state machine — exactly what a
    /// headless/RL driver would do.
    /// </summary>
    [Category("RequiresGraphics")]
    public class SessionSeamPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";

        private GameObject hostGo;
        private SessionRig rigInstance;

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(true);
            DestroyTestObject(hostGo);
            DestroyTestObject(rigInstance ? rigInstance.gameObject : null);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Session_ComposesAndTearsDownWithoutAHost()
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<SessionRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "SessionRig prefab loads");
            rigInstance = Object.Instantiate(rigPrefab);

            hostGo = new GameObject("SessionRoot");
            var session = TestSession.Create(hostGo, new SessionProfile
            {
                sectorEntry = null,
                buildPlayer = true,
                presentation = false
            }, rigInstance);

            yield return session.Compose();

            Assert.IsNotNull(session.Services, "Compose must populate the session's services");
            Assert.IsNotNull(session.Rig.Player, "Compose must build the player (buildPlayer = true)");

            Assert.DoesNotThrow(() => session.Rig.ApplyLoadout(),
                "ApplyLoadout must install the standing loadout without throwing");

            yield return session.Teardown();

            Assert.IsNull(session.Services, "Teardown must clear the session's services");
            Assert.IsNull(session.Rig.Player, "Teardown must drop the rig's player");
        }
    }
}
#endif
