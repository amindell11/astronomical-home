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
    public class KeystonePromptPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string KeyFontPath = "Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf";
        private const string WordFontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";
        private const float Timeout = 2f;

        private PlayModeInputTestFixture input;
        private Keyboard keyboard;
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
        public IEnumerator PressingSpace_FadesThePrompt()
        {
            var prompt = Show(() => false);
            yield return null;
            Assert.AreEqual(1f, Alpha(prompt), "test premise: the prompt shows until used");

            input.Press(keyboard.spaceKey);
            yield return AsyncAssert.WaitUntil(() => !prompt, Timeout, "Space fades the prompt and it goes");
        }

        [UnityTest]
        public IEnumerator Dismissal_FadesThePrompt()
        {
            var dismissed = false;
            var prompt = Show(() => dismissed);
            yield return null;
            Assert.AreEqual(1f, Alpha(prompt), "test premise: the prompt shows until dismissed");

            dismissed = true;
            yield return AsyncAssert.WaitUntil(() => !prompt, Timeout, "dismissal fades the prompt and it goes");
        }

        [UnityTest]
        public IEnumerator WaitingPrompt_StaysHidden_ThenShows()
        {
            var waiting = true;
            var prompt = Show(() => false, () => waiting);
            Assert.AreEqual(0f, Alpha(prompt), "a prompt that waits starts hidden");
            yield return null;
            yield return null;
            Assert.AreEqual(0f, Alpha(prompt), "a prompt stays hidden while it waits");

            waiting = false;
            yield return null;
            Assert.AreEqual(1f, Alpha(prompt), "the prompt shows once it stops waiting");
        }

        [UnityTest]
        public IEnumerator DismissedWhileWaiting_GoesUnseen()
        {
            var dismissed = false;
            var prompt = Show(() => dismissed, () => true);
            yield return null;
            Assert.AreEqual(0f, Alpha(prompt), "test premise: the prompt waits hidden");

            dismissed = true;
            yield return null;
            yield return null;
            Assert.IsFalse(prompt, "a prompt dismissed while it waits goes without ever showing");
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Rig_ShowsThePromptAtTheFirstSectorItEntersOnly()
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
            Assert.AreEqual(1, PromptsOn(rig).Length, "the first sector start shows the prompt");
            var line = PromptsOn(rig)[0].GetComponent<KeyPrompt>();
            Assert.AreEqual("SPACE", line.Key);
            Assert.AreEqual(rig.Player.Keystone.DisplayName.ToUpperInvariant(), line.Action,
                "the prompt names the keystone the player flies");

            rig.EnterSector(null);
            rig.EnterSector(NewPhase("Second sector").module);
            yield return null;
            Assert.AreEqual(1, PromptsOn(rig).Length, "a later sector start shows no second prompt");

            firstDirector.Advance(41f);
            yield return AsyncAssert.WaitUntil(() => PromptsOn(rig).Length == 0, Timeout,
                "the first sector's lull after its first sector phase ends the prompt");
        }

        private KeystonePrompt Show(Func<bool> dismissed, Func<bool> waiting = null) => KeystonePrompt.Create(hud.transform,
            TestAssets.Load<Font>(KeyFontPath), TestAssets.Load<Font>(WordFontPath), null, reader, "Blink", dismissed, waiting ?? (() => false));

        private static float Alpha(KeystonePrompt prompt) => prompt.GetComponent<CanvasGroup>().alpha;

        private static KeystonePrompt[] PromptsOn(PlayerRig rig) =>
            rig.Overlay.GetComponentsInChildren<KeystonePrompt>(true);

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
