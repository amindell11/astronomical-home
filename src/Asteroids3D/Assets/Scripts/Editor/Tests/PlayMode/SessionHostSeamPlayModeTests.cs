#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Game;
using Game.Bootstrap;
using Game.Services;
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
    /// The "unplug the game" proof: the below-seam <see cref="SessionHost"/> primitives compose and
    /// tear down a real session with NO <see cref="GameDriver"/> and no state machine — exactly what a
    /// headless/RL driver would do. Drives the primitives directly over an explicit
    /// <see cref="GameSession"/> and asserts the session builds (services + player) and cleanly unwinds.
    /// </summary>
    [Category("RequiresGraphics")]
    public class SessionHostSeamPlayModeTests : PlayModeWorldFixture
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
        public IEnumerator Primitives_DriveASessionWithoutADriver()
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<SessionRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "SessionRig prefab loads");
            rigInstance = Object.Instantiate(rigPrefab);

            // Session root: the two sibling services + the host (RequireComponent adds the services).
            hostGo = new GameObject("SessionRoot");
            var host = hostGo.AddComponent<SessionHost>();
            SetPrivate(host, "playerRig", rigInstance);

            var session = new GameSession
            {
                Profile = new SessionProfile
                {
                    sectorEntry = null,
                    buildPlayer = true,
                    presentation = false,
                    vfx = false
                }
            };

            yield return host.ComposeSession(session);

            Assert.IsNotNull(session.Services, "ComposeSession must populate the session's services");
            Assert.IsNotNull(session.Rig, "ComposeSession must adopt the rig onto the session");
            Assert.IsNotNull(session.Rig.Player, "ComposeSession must build the player (buildPlayer = true)");

            // The seam's non-coroutine primitive — its only caller besides an RL driver.
            Assert.DoesNotThrow(() => host.ApplyLoadout(session),
                "ApplyLoadout must install the standing loadout without throwing");

            yield return host.TeardownSession(session);

            Assert.IsNull(session.Services, "TeardownSession must clear the session's services");
            Assert.IsNull(session.Rig, "TeardownSession must drop the session rig");
        }

        private static void SetPrivate(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
#endif
