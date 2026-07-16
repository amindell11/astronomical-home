#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Game.Bootstrap;
using Game.Services;
using NUnit.Framework;
using Player;
using Ships;
using Tests.PlayMode.Common;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// While the hangar screen is open the player's commander must be disabled — Fire1 shares
    /// mouse 0 with UI clicks, so an enabled commander turns every hangar button press into a
    /// weapon shot on the live ship behind the screen. Launch must restore it. The gate now lives
    /// in the driver's <see cref="GameDriver.RunHangar"/>, so the flow is driven there.
    /// </summary>
    [Category("RequiresGraphics")]
    public class HangarInputGatePlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string HangarScreenPath = "Assets/Prefabs/UI/HangarScreen.prefab";
        private const string CatalogPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        private GameObject servicesGo;
        private GameObject driverGo;
        private SessionRig rig;
        private GameServices services;

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(true);
            var screen = Object.FindFirstObjectByType<HangarScreen>();
            if (screen) DestroyTestObject(screen.gameObject);
            if (EventSystem.current) DestroyTestObject(EventSystem.current.gameObject);
            if (rig) rig.Teardown();
            services?.ClearAll();
            DestroyTestObject(driverGo);
            DestroyTestObject(rig ? rig.gameObject : null);
            DestroyTestObject(servicesGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator RunHangar_GatesPlayerInput_UntilLaunch()
        {
            GameSettings.SetPresentationEnabled(true);

            servicesGo = new GameObject("TestServices");
            var unitService = servicesGo.AddComponent<UnitService>();
            var objectiveService = servicesGo.AddComponent<ObjectiveService>();
            var arena = Tests.Common.TestArena.On(servicesGo, unitService.Registry);
            unitService.SetArena(arena);
            var projectileService = new ProjectileService(servicesGo.transform);
            unitService.SetProjectiles(projectileService);
            services = new GameServices(
                unitService: unitService,
                projectiles: projectileService,
                environmentService: new EnvironmentService(),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService(),
                arena: arena);

            var rigPrefab = AssetDatabase.LoadAssetAtPath<SessionRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "SessionRig prefab loads");
            rig = Object.Instantiate(rigPrefab);
            yield return rig.Build(services, buildPlayer: true, onPlayerDeath: null);
            Assert.IsNotNull(rig.Player, "rig built a player");
            Assert.IsNotNull(rig.Player.Commander, "player has a commander");
            Assert.IsTrue(rig.Player.Commander.enabled, "test premise: commander starts enabled");

            // The hangar screen + catalog live on the driver now; supply them to an inactive driver
            // (its Awake/state-machine never runs) and drive the flow coroutine on the active rig.
            driverGo = new GameObject("TestDriver");
            driverGo.SetActive(false);
            var driver = driverGo.AddComponent<GameDriver>();
            SetPrivate(driver, "hangarScreenPrefab", AssetDatabase.LoadAssetAtPath<HangarScreen>(HangarScreenPath));
            SetPrivate(driver, "loadoutCatalog", AssetDatabase.LoadAssetAtPath<LoadoutConfig>(CatalogPath));

            var session = new GameSession { Rig = rig, Services = services };

            var finished = false;
            IEnumerator Run()
            {
                yield return driver.RunHangar(session);
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
