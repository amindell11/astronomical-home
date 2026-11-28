#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Cameras;
using Game;
using Game.Tutorial;
using NUnit.Framework;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UI.Comms;
using UI.Prompts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Prompts
{
    [Category("UI")]
    public class FirstSectorTutorialPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const float SearchSeconds = .5f;
        private const float Timeout = 3f;

        private PlayModeInputTestFixture input;
        private Keyboard keyboard;
        private Mouse mouse;
        private GameObject servicesGo;
        private UnitService units;
        private ObserverCam observer;
        private PlayerRig rig;
        private SectorHunt hunt;
        private HuntFeelProfile feel;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            input = new PlayModeInputTestFixture();
            input.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public override void TearDown()
        {
            if (rig) rig.Teardown();
            if (units) units.Clear();
            DestroyTestObject(hunt ? hunt.gameObject : null);
            DestroyTestObject(rig ? rig.gameObject : null);
            DestroyTestObject(observer ? observer.gameObject : null);
            DestroyTestObject(servicesGo);
            if (feel) Object.Destroy(feel);
            input.TearDown();
            base.TearDown();
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Tutorial_TeachesMovementThenWeapons_ThenTheIntro_ThenTheWavesCardAtTheAlarm()
        {
            yield return BuildRig();
            yield return BuildHeldHunt();
            var tutorial = new FirstSectorTutorial(rig.Overlay, hunt, 30f, promptStepSeconds: 60f);
            yield return null;
            CollectionAssert.AreEqual(new[] { "W/S", "A/D", "MOUSE" }, Keys(), "the sector opens on the movement lines alone");

            input.Set(mouse.position, new Vector2(300f, 300f));
            yield return null;
            yield return null;
            input.Press(keyboard.wKey);
            input.Press(keyboard.dKey);
            input.Set(mouse.position, new Vector2(500f, 300f));
            yield return AsyncAssert.WaitUntil(() => Step(tutorial, () => Keys().Contains("SPACE")), Timeout,
                "using every movement key brings up the weapon and keystone lines");
            CollectionAssert.DoesNotContain(Keys(), "W/S", "the movement lines have gone");
            Assert.IsFalse(tutorial.IntroDue, "the intro waits for the weapon lines");

            input.Release(keyboard.wKey);
            input.Release(keyboard.dKey);
            input.Press(keyboard.spaceKey);
            input.Press(mouse.leftButton);
            // Two presses on one device in one frame queue two full states, and the second drops the first button.
            yield return null;
            input.Press(mouse.rightButton);
            yield return AsyncAssert.WaitUntil(() => Step(tutorial, () => tutorial.IntroDue), Timeout,
                "using every weapon key makes the intro due");
            Assert.IsEmpty(Keys(), "no prompt line is left");
            Assert.IsFalse(hunt.Alarmed, "the held hunt has not sounded");
            Assert.AreEqual(SearchSeconds, hunt.SearchSecondsLeft, 1e-3f, "the search clock waits full");

            tutorial.IntroDone();
            hunt.Resume();
            yield return AsyncAssert.WaitUntil(() => Step(tutorial, () => Cards().Contains(FirstSectorTutorial.WavesText)),
                Timeout, "the first alarm shows the waves card");

            tutorial.End();
            yield return AsyncAssert.WaitUntil(() => Cards().Length == 0, Timeout, "ending the sector clears the card");
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Tutorial_StepTimesOut_MovesOnWithoutInput()
        {
            yield return BuildRig();
            yield return BuildHeldHunt();
            var tutorial = new FirstSectorTutorial(rig.Overlay, hunt, 30f, promptStepSeconds: .5f);
            yield return null;

            yield return AsyncAssert.WaitUntil(() => Step(tutorial, () => Keys().Contains("SPACE")), Timeout,
                "a prompt step times out onto the weapon lines");
            CollectionAssert.DoesNotContain(Keys(), "W/S", "the movement lines have gone");
            CollectionAssert.DoesNotContain(Keys(), "A/D", "the movement lines have gone");
            CollectionAssert.DoesNotContain(Keys(), "MOUSE", "the movement lines have gone");

            yield return AsyncAssert.WaitUntil(() => Step(tutorial, () => tutorial.IntroDue), Timeout,
                "the weapon step times out and the intro becomes due");
            Assert.IsEmpty(Keys(), "no prompt line is left");
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Tutorial_StepClockPausesWhileTheCompanionSpeaks()
        {
            yield return BuildRig();
            yield return BuildHeldHunt();
            var tutorial = new FirstSectorTutorial(rig.Overlay, hunt, 30f, promptStepSeconds: .5f);
            yield return null;
            rig.Overlay.CommLine.Show(new CommMessage("Companion", Color.white, "Hold still."), 2f, .4f);

            var spokeUntil = Time.unscaledTime + 1f;
            yield return AsyncAssert.WaitUntil(() => Step(tutorial, () => Time.unscaledTime >= spokeUntil), Timeout,
                "a second passes while the companion speaks");
            CollectionAssert.AreEqual(new[] { "W/S", "A/D", "MOUSE" }, Keys(),
                "the movement lines stay while the companion speaks");

            rig.Overlay.CommLine.Clear();
            yield return AsyncAssert.WaitUntil(() => Step(tutorial, () => Keys().Contains("SPACE")), Timeout,
                "the step clock runs once the companion stops");
        }

        private IEnumerator BuildRig()
        {
            servicesGo = new GameObject("TestServices");
            units = servicesGo.AddComponent<UnitService>();
            var objectives = servicesGo.AddComponent<ObjectiveService>();
            ShipServices.Compose(units, servicesGo.transform, presentationEnabled: true);
            observer = TestAssets.NewObserverCam();
            rig = Object.Instantiate(TestAssets.Load<PlayerRig>(RigPrefabPath));
            yield return rig.Build(units, objectives, presentationEnabled: true, observer, servicesGo.transform,
                new SessionFrame(Vector2.zero), onPlayerDeath: null);
            // A built rig parks its ship inactive, and an inactive ship's flight controls read nothing.
            rig.Player.gameObject.SetActive(true);
        }

        private IEnumerator BuildHeldHunt()
        {
            feel = ScriptableObject.CreateInstance<HuntFeelProfile>();
            feel.alarmGraceSeconds = 1f;
            hunt = new GameObject("Hunt").AddComponent<SectorHunt>();
            hunt.Configure(feel, null, 0f);
            yield return hunt.Build(new SectorBuildContext(null, null, true, null, default, hero: rig.Player,
                sectorIndex: 0, row: new SectorEntry { searchSeconds = SearchSeconds }));
            hunt.Hold();
        }

        private static bool Step(FirstSectorTutorial tutorial, System.Func<bool> until)
        {
            tutorial.Tick();
            return until();
        }

        private string[] Keys() =>
            rig.Overlay.GetComponentsInChildren<FirstRunPrompt>(true)
                .Select(line => line.GetComponent<KeyPrompt>().Key).ToArray();

        private string[] Cards() =>
            rig.Overlay.GetComponentsInChildren<TutorialCard>(true).Select(card => card.Message).ToArray();
    }
}
#endif
