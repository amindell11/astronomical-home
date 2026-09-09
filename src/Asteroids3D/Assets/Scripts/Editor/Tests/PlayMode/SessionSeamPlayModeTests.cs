#if UNITY_EDITOR
using System.Collections;
using Cameras;
using Game.Play;
using Game.Sessions;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// The "unplug the game" proof: a <see cref="Session"/> composes with NO host above it and no
    /// state machine, a <see cref="PlayerRig"/> builds against its services and applies the standing
    /// loadout, and both tear down — exactly what a headless/RL driver would do.
    /// </summary>
    [Category("RequiresGraphics")]
    public class SessionSeamPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";

        private GameObject hostGo;
        private PlayerRig rigInstance;
        private ObserverCam observer;

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(true);
            DestroyTestObject(hostGo);
            DestroyTestObject(rigInstance ? rigInstance.gameObject : null);
            DestroyTestObject(observer ? observer.gameObject : null);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Session_ComposesAndTearsDownWithoutAHost()
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "PlayerRig prefab loads");
            rigInstance = Object.Instantiate(rigPrefab);
            observer = TestAssets.NewObserverCam();
            Assert.IsNotNull(observer, "observer camera prefab loads");

            hostGo = new GameObject("SessionRoot");
            var session = TestSession.Create(hostGo, new SessionProfile
            {
                sectorEntry = null,
                presentation = false
            });

            yield return session.Compose();

            Assert.IsNotNull(session.Units, "Compose must populate the session's services");

            yield return rigInstance.Build(session.Units, session.Objectives, presentationEnabled: false,
                observer, session.Frame, onPlayerDeath: null);

            Assert.IsNotNull(rigInstance.Player, "the rig builds the player against the session's services");

            Assert.DoesNotThrow(() => rigInstance.ApplyLoadout(),
                "ApplyLoadout must install the standing loadout without throwing");

            rigInstance.Teardown();
            Assert.IsNull(rigInstance.Player, "the rig's teardown drops its player");

            yield return session.Teardown();

            Assert.IsNull(session.Units, "Teardown must clear the session's services");
        }
    }
}
#endif
