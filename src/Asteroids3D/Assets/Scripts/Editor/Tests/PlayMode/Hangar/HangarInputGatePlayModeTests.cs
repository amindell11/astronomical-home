#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cameras;
using Game;
using Substrate.Sessions;
using NUnit.Framework;
using Ships.Loadout;
using Tests.PlayMode.Common;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Substrate.Services;
using Substrate.Services.Units;
using Substrate.Services.Objectives;

namespace Tests.PlayMode.Hangar
{
    /// <summary>
    /// While the hangar screen is open the player's commander must be inactive — primary fire shares
    /// the left mouse button with UI clicks, so a live commander turns every hangar button press into a
    /// weapon shot on the ship behind the screen — and its HUD hidden. Launch must bring both back.
    /// The visible preview must leave option clicks reachable and stay within its rendering budget.
    /// </summary>
    // Real PlayerRig cameras: URP render loop cannot create RTs under -nographics.
    [Category("RequiresGraphics")]
    public class HangarInputGatePlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string HangarScreenPath = "Assets/Prefabs/UI/HangarScreen.prefab";
        private const string OfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        private GameObject servicesGo;
        private GameObject hostGo;
        private GameObject eventSystemGo;
        private PlayerRig rig;
        private ObserverCam observer;
        private UnitService unitService;

        public override void TearDown()
        {
            var screen = Object.FindFirstObjectByType<HangarScreen>();
            if (screen) DestroyTestObject(screen.gameObject);
            if (rig) rig.Teardown();
            if (unitService) unitService.Clear();
            DestroyTestObject(hostGo);
            DestroyTestObject(eventSystemGo);
            DestroyTestObject(rig ? rig.gameObject : null);
            DestroyTestObject(observer ? observer.gameObject : null);
            DestroyTestObject(servicesGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator RunHangar_GatesPlayerInput_UntilLaunch()
        {
            servicesGo = new GameObject("TestServices");
            unitService = servicesGo.AddComponent<UnitService>();
            var objectiveService = servicesGo.AddComponent<ObjectiveService>();
            ShipServices.Compose(unitService, servicesGo.transform, presentationEnabled: true);

            observer = TestAssets.NewObserverCam();
            var rigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "PlayerRig prefab loads");
            rig = Object.Instantiate(rigPrefab);
            yield return rig.Build(unitService, objectiveService, presentationEnabled: true, observer,
                servicesGo.transform, new SessionFrame(Vector2.zero), onPlayerDeath: null);
            Assert.IsNotNull(rig.Player, "rig built a player");
            Assert.IsNotNull(rig.Player.Commander, "player has a commander");
            Assert.IsNotNull(rig.Overlay, "rig built a HUD");

            // Host stays inactive so its flow never runs; the rig hosts the hangar coroutine.
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();
            host.hangarScreenPrefab = AssetDatabase.LoadAssetAtPath<HangarScreen>(HangarScreenPath);
            host.hangarOffer = AssetDatabase.LoadAssetAtPath<ItemSubset>(OfferPath);

            var finished = false;
            IEnumerator Run()
            {
                yield return host.RunHangar(rig, presentationEnabled: true, servicesGo.transform);
                finished = true;
            }
            rig.StartCoroutine(Run());
            yield return null;

            var screen = Object.FindFirstObjectByType<HangarScreen>();
            Assert.IsNotNull(screen, "interactive path instantiated the hangar screen");
            Assert.IsFalse(rig.Player.Commander.isActiveAndEnabled,
                "player input is disconnected while the hangar screen is open");
            Assert.IsFalse(HudVisible(), "the HUD is hidden while the hangar screen is open");

            var eventSystem = new GameObject("TestEventSystem").AddComponent<EventSystem>();
            eventSystemGo = eventSystem.gameObject;
            Canvas.ForceUpdateCanvases();
            var rows = new[] { screen.shipRow,
                screen.primaryWeaponRow, screen.secondaryWeaponRow };
            var buttons = rows.SelectMany(row => new[] { row.previousButton, row.nextButton })
                .Append(screen.launchButton);
            foreach (var button in buttons)
            {
                var rect = (RectTransform)button.transform;
                var pointer = new PointerEventData(eventSystem)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
                };
                var hits = new List<RaycastResult>();
                eventSystem.RaycastAll(pointer, hits);
                Assert.IsNotEmpty(hits, $"{button.name} receives pointer hits");
                Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(button.transform),
                    $"{button.transform.parent.name}/{button.name} is covered by {hits[0].gameObject.name}");
            }

            var launchButton = screen.launchButton;
            Assert.IsNotNull(launchButton, "hangar screen has a launch button");
            launchButton.onClick.Invoke();

            yield return null;
            yield return null;

            Assert.IsTrue(finished, "RunHangar completed after Launch");
            Assert.IsTrue(rig.Player.Commander.isActiveAndEnabled, "player input is restored after launch");
            Assert.IsTrue(HudVisible(), "the HUD is back after launch");
            Assert.IsTrue(screen == null, "hangar screen was destroyed on launch");
        }

        [Test]
        public void Preview_StaysWithin224MiB_WhenRequestedAt4K()
        {
            var stage = HangarPreviewStage.Create(null, new Vector2Int(4096, 2304), new Rect(.36f, 0f, .62f, 1f), Enumerable.Empty<Ships.Ship>());
            try
            {
                var texture = (RenderTexture)stage.Texture;
                Assert.IsTrue(texture.Create(), "preview target allocates on the graphics device");
                Assert.LessOrEqual(UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
                    224L * 1024 * 1024, "preview target exceeds its GPU allocation budget");
            }
            finally
            {
                DestroyTestObject(stage);
            }
        }

        private bool HudVisible()
        {
            var canvases = rig.Overlay.GetComponentsInChildren<Canvas>(true);
            Assert.IsNotEmpty(canvases, "test premise: the HUD has canvases");
            return canvases.All(c => c.enabled);
        }
    }
}
#endif
