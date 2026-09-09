#if UNITY_EDITOR
using System.Collections;
using Cameras;
using System.Reflection;
using Game.Play;
using Game.Services;
using Game.Sessions;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Utils;
using Game.Services.Units;
using Game.Services.Projectiles;
using Game.Services.Objectives;

namespace Tests.PlayMode
{
    /// <summary>
    /// While the hangar screen is open the player's commander must be disabled — Fire1 shares
    /// mouse 0 with UI clicks, so an enabled commander turns every hangar button press into a
    /// weapon shot on the live ship behind the screen. Launch must restore it. The gate lives in
    /// <see cref="GameSessionHost.RunHangar"/>, so the flow is driven there.
    /// </summary>
    // Real PlayerRig cameras: URP render loop cannot create RTs under -nographics.
    [Category("RequiresGraphics")]
    public class HangarInputGatePlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string HangarScreenPath = "Assets/Prefabs/UI/HangarScreen.prefab";
        private const string CatalogPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        private GameObject servicesGo;
        private GameObject hostGo;
        private PlayerRig rig;
        private ObserverCam observer;
        private UnitService unitService;

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(true);
            var screen = Object.FindFirstObjectByType<HangarScreen>();
            if (screen) DestroyTestObject(screen.gameObject);
            if (EventSystem.current) DestroyTestObject(EventSystem.current.gameObject);
            if (rig) rig.Teardown();
            unitService?.Clear();
            DestroyTestObject(hostGo);
            DestroyTestObject(rig ? rig.gameObject : null);
            DestroyTestObject(observer ? observer.gameObject : null);
            DestroyTestObject(servicesGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator RunHangar_GatesPlayerInput_UntilLaunch()
        {
            GameSettings.SetPresentationEnabled(true);

            servicesGo = new GameObject("TestServices");
            unitService = servicesGo.AddComponent<UnitService>();
            var objectiveService = servicesGo.AddComponent<ObjectiveService>();
            unitService.SetProjectiles(new ProjectileService(servicesGo.transform));

            observer = TestAssets.NewObserverCam();
            var rigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "PlayerRig prefab loads");
            rig = Object.Instantiate(rigPrefab);
            yield return rig.Build(unitService, objectiveService, presentationEnabled: true, observer,
                new SessionFrame(Vector2.zero), onPlayerDeath: null);
            Assert.IsNotNull(rig.Player, "rig built a player");
            Assert.IsNotNull(rig.Player.Commander, "player has a commander");
            Assert.IsTrue(rig.Player.Commander.enabled, "test premise: commander starts enabled");

            // Supply screen + catalog to an inactive host (Awake/state-machine never runs) and drive the flow coroutine on the active rig.
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameSessionHost>();
            SetPrivate(host, "hangarScreenPrefab", AssetDatabase.LoadAssetAtPath<HangarScreen>(HangarScreenPath));
            SetPrivate(host, "loadoutCatalog", AssetDatabase.LoadAssetAtPath<LoadoutConfig>(CatalogPath));

            var finished = false;
            IEnumerator Run()
            {
                yield return host.RunHangar(rig);
                finished = true;
            }
            rig.StartCoroutine(Run());
            yield return null;

            var screen = Object.FindFirstObjectByType<HangarScreen>();
            Assert.IsNotNull(screen, "interactive path instantiated the hangar screen");
            Assert.IsFalse(rig.Player.Commander.enabled,
                "player input is disconnected while the hangar screen is open");

            var launchButton = new SerializedObject(screen)
                .FindProperty("launchButton").objectReferenceValue as Button;
            Assert.IsNotNull(launchButton, "hangar screen has a launch button");
            launchButton.onClick.Invoke();

            yield return null;
            yield return null;

            Assert.IsTrue(finished, "RunHangar completed after Launch");
            Assert.IsTrue(rig.Player.Commander.enabled, "player input is restored after launch");
            Assert.IsTrue(screen == null, "hangar screen was destroyed on launch");
        }

        private static void SetPrivate(object target, string field, Object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
#endif
