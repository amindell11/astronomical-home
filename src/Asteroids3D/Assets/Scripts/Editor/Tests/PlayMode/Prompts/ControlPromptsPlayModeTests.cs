#if UNITY_EDITOR
using System;
using System.Collections;
using Cameras;
using Game;
using Game.Player;
using NUnit.Framework;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UI.Prompts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Prompts
{
    [Category("UI")]
    public class ControlPromptsPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string KeyFontPath = "Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf";
        private const string ActionFontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";
        private const float Timeout = 2f;

        private PlayModeInputTestFixture input;
        private Keyboard keyboard;
        private Mouse mouse;
        private PlayerInputReader reader;
        private GameObject hud;
        private GameObject servicesGo;
        private UnitService units;
        private ObserverCam observer;
        private PlayerRig rig;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            input = new PlayModeInputTestFixture();
            input.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            hud = new GameObject("Prompts HUD", typeof(RectTransform), typeof(Canvas));
            reader = new PlayerInputReader(_ => Vector3.zero);
            reader.Enable();
        }

        [TearDown]
        public override void TearDown()
        {
            if (rig) rig.Teardown();
            if (units) units.Clear();
            DestroyTestObject(rig ? rig.gameObject : null);
            DestroyTestObject(observer ? observer.gameObject : null);
            DestroyTestObject(servicesGo);
            DestroyTestObject(hud);
            reader.Dispose();
            input.TearDown();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator UsingAControl_FadesItsRowWhileTheOthersStay()
        {
            var prompts = Show(() => false);
            yield return null;
            Assert.AreEqual(1f, Alpha(prompts, "Thrust"), "test premise: the rows show until used");

            input.Press(keyboard.wKey);
            yield return AsyncAssert.WaitUntil(() => Alpha(prompts, "Thrust") == 0f, Timeout, "W fades the thrust row");
            AssertShowing(prompts, "Strafe", "Aim", "Fire", "Boost");

            input.Press(mouse.leftButton);
            yield return AsyncAssert.WaitUntil(() => Alpha(prompts, "Fire") == 0f, Timeout, "a click fades the fire row");
            AssertShowing(prompts, "Strafe", "Aim", "Boost");
        }

        [UnityTest]
        public IEnumerator Dismissal_FadesEveryRemainingRow()
        {
            var dismissed = false;
            var prompts = Show(() => dismissed);
            input.Press(keyboard.wKey);
            yield return AsyncAssert.WaitUntil(() => Alpha(prompts, "Thrust") == 0f, Timeout, "W fades the thrust row");
            AssertShowing(prompts, "Strafe", "Aim", "Fire", "Boost");

            dismissed = true;
            yield return AsyncAssert.WaitUntil(() => !prompts, Timeout, "every remaining row fades and the prompts go");
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Rig_ShowsThePromptsAtTheFirstSectorItEntersOnly()
        {
            servicesGo = new GameObject("TestServices");
            units = servicesGo.AddComponent<UnitService>();
            var objectives = servicesGo.AddComponent<ObjectiveService>();
            ShipServices.Compose(units, servicesGo.transform, presentationEnabled: true);
            observer = TestAssets.NewObserverCam();
            rig = Object.Instantiate(TestAssets.Load<PlayerRig>(RigPrefabPath));
            yield return rig.Build(units, objectives, presentationEnabled: true, observer, servicesGo.transform,
                new SessionFrame(Vector2.zero), onPlayerDeath: null);

            rig.EnterSector(null);
            Assert.IsEmpty(PromptsOn(rig), "leaving a sector shows nothing");
            var (first, firstDirector) = NewPhase("First sector");
            rig.EnterSector(first);
            Assert.AreEqual(1, PromptsOn(rig).Length, "the first sector start shows the prompts");

            rig.EnterSector(null);
            rig.EnterSector(NewPhase("Second sector").module);
            yield return null;
            Assert.AreEqual(1, PromptsOn(rig).Length, "a later sector start shows no second set");

            firstDirector.Advance(41f);
            yield return AsyncAssert.WaitUntil(() => PromptsOn(rig).Length == 0, Timeout,
                "the first sector's lull after its first sector phase ends the prompts");
        }

        private ControlPrompts Show(Func<bool> dismissed) => ControlPrompts.Create(hud.transform,
            TestAssets.Load<Font>(KeyFontPath), TestAssets.Load<Font>(ActionFontPath), null, reader, dismissed);

        private static float Alpha(ControlPrompts prompts, string row) =>
            prompts.transform.Find(row).GetComponent<CanvasGroup>().alpha;

        private static void AssertShowing(ControlPrompts prompts, params string[] rows)
        {
            foreach (var row in rows) Assert.AreEqual(1f, Alpha(prompts, row), $"the {row} row still shows");
        }

        private static ControlPrompts[] PromptsOn(PlayerRig rig) =>
            rig.Overlay.GetComponentsInChildren<ControlPrompts>(true);

        private (PhaseSpineModule module, WaveDirector director) NewPhase(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(servicesGo.transform, false);
            var director = go.AddComponent<WaveDirector>();
            director.Configure(null, new[] { new SectorPhase { seconds = 40f }, new SectorPhase { seconds = 40f } }, 5f);
            director.BeginSchedule(0f);
            var module = go.AddComponent<PhaseSpineModule>();
            module.Bind(director, null, null);
            return (module, director);
        }
    }
}
#endif
