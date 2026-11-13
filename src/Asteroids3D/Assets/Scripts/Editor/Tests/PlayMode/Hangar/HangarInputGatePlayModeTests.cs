#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cameras;
using Game;
using Game.Runs;
using Substrate.Sessions;
using NUnit.Framework;
using Ships.Loadout;
using Tests.PlayMode.Common;
using UI.Hangar;
using UI.Hangar.Displays;
using UI.Hangar.Hardware;
using UI.Hangar.Selection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Substrate.Services;
using Substrate.Services.Units;
using Substrate.Services.Objectives;

namespace Tests.PlayMode.Hangar
{
    /// <summary>
    /// While the hangar console is open the player's commander must be inactive — primary fire shares
    /// the left mouse button with UI clicks, so a live commander turns every hangar press into a
    /// weapon shot on the ship behind the console — and its HUD hidden. Launch must bring both back.
    /// A click on the console must reach the display under that glass, and the preview stays in budget.
    /// </summary>
    // Real PlayerRig cameras: URP render loop cannot create RTs under -nographics.
    [Category("UI")]
    [Category("RequiresGraphics")]
    public class HangarInputGatePlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string HangarConsolePath = "Assets/Prefabs/UI/HangarConsole.prefab";
        private const string OfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        private GameObject servicesGo;
        private GameObject hostGo;
        private GameObject eventSystemGo;
        private PlayerRig rig;
        private ObserverCam observer;
        private UnitService unitService;

        public override void TearDown()
        {
            var console = Object.FindFirstObjectByType<HangarConsole>();
            if (console) DestroyTestObject(console.gameObject);
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
            host.hangarConsolePrefab = AssetDatabase.LoadAssetAtPath<HangarConsole>(HangarConsolePath);
            host.hangarOffer = AssetDatabase.LoadAssetAtPath<ItemSubset>(OfferPath);
            host.unlocks = new ShipUnlocks(Path.Combine(Application.temporaryCachePath, "no-run-records.jsonl"));

            var finished = false;
            IEnumerator Run()
            {
                yield return host.RunHangar(rig, presentationEnabled: true, servicesGo.transform);
                finished = true;
            }
            rig.StartCoroutine(Run());
            yield return null;

            var console = Object.FindFirstObjectByType<HangarConsole>();
            Assert.IsNotNull(console, "interactive path instantiated the hangar console");
            Assert.IsFalse(rig.Player.Commander.isActiveAndEnabled,
                "player input is disconnected while the hangar console is open");
            Assert.IsFalse(HudVisible(), "the HUD is hidden while the hangar console is open");

            yield return null;
            Canvas.ForceUpdateCanvases();
            var eventSystem = new GameObject("TestEventSystem").AddComponent<EventSystem>();
            eventSystemGo = eventSystem.gameObject;
            var weapon = console.GetComponentInChildren<HangarMainDisplayView>()[HangarSlot.Weapon];
            var weaponHit = Click(eventSystem, ScreenPoint(console, ButtonOnGlass(console, weapon)));
            Assert.AreEqual(weapon.gameObject, weaponHit.gameObject, "the weapon slot is the top hit");
            Assert.AreEqual(HangarSlot.Weapon, console.Selection.Selected, "clicking the slot selects it");

            var before = console.Selection.Weapon;
            var nextHit = Click(eventSystem, ScreenPoint(console, console.NextButton.GetComponent<Renderer>().bounds.center));
            Assert.AreEqual(console.NextButton.gameObject, nextHit.gameObject, "the next arrow is the top hit");
            Assert.AreNotSame(before, console.Selection.Weapon, "the next arrow steps the selected slot");

            console.Launch();

            yield return null;
            yield return null;

            Assert.IsTrue(finished, "RunHangar completed after Launch");
            Assert.IsTrue(rig.Player.Commander.isActiveAndEnabled, "player input is restored after launch");
            Assert.IsTrue(HudVisible(), "the HUD is back after launch");
            Assert.IsTrue(console == null, "hangar console was destroyed on launch");
        }

        private static RaycastResult Click(EventSystem eventSystem, Vector2 screen)
        {
            var pointer = new PointerEventData(eventSystem)
            {
                position = screen,
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "the console receives the pointer");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            return hits[0];
        }

        private static Vector3 ButtonOnGlass(HangarConsole console, Button button)
        {
            var display = console.MainDisplay;
            var rect = (RectTransform)button.transform;
            var pixel = display.DisplayCamera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
            var uv = new Vector2(pixel.x / display.Resolution.x, pixel.y / display.Resolution.y);
            var glass = console.MainScreenGlass;
            var surface = new MeshSurface(glass.GetComponent<MeshFilter>().sharedMesh);
            return glass.transform.TransformPoint(surface.LocalPoint(uv));
        }

        private static Vector2 ScreenPoint(HangarConsole console, Vector3 world)
        {
            var viewport = console.HardwareCamera.WorldToViewportPoint(world);
            var rect = console.CompositeImage.rectTransform;
            var local = new Vector2(
                Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, viewport.x),
                Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, viewport.y));
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));
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
