#if UNITY_EDITOR
using System.Collections;
using Game.Services;
using NUnit.Framework;
using Player;
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
    /// weapon shot on the live ship behind the screen. Launch must restore it.
    /// </summary>
    [Category("RequiresGraphics")]
    public class HangarInputGatePlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";

        private GameObject servicesGo;
        private PlayerRig rig;
        private GameServices services;

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(true);
            var screen = Object.FindFirstObjectByType<HangarScreen>();
            if (screen) DestroyTestObject(screen.gameObject);
            if (EventSystem.current) DestroyTestObject(EventSystem.current.gameObject);
            if (rig) rig.Teardown();
            services?.ClearAll();
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
            services = new GameServices(
                unitService: unitService,
                environmentService: new EnvironmentService(),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService());

            var rigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "PlayerRig prefab loads");
            rig = Object.Instantiate(rigPrefab);
            yield return rig.Build(services, buildPlayer: true);
            Assert.IsNotNull(rig.Player, "rig built a player");
            Assert.IsNotNull(rig.Player.Commander, "player has a commander");
            Assert.IsTrue(rig.Player.Commander.enabled, "test premise: commander starts enabled");

            var finished = false;
            IEnumerator Run()
            {
                yield return rig.RunHangar();
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
    }
}
#endif
