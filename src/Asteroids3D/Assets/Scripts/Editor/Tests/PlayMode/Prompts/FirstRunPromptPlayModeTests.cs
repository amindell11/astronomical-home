#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cameras;
using Combat.Weapons;
using Game;
using Game.Player;
using NUnit.Framework;
using Ships.Command;
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
    public class FirstRunPromptPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string KeyFontPath = "Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf";
        private const string WordFontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";
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
        public IEnumerator Show_TeachesTheKeystoneAndBothWeapons()
        {
            Show(() => false);
            yield return null;

            Assert.AreEqual(3, Lines(hud).Length, "one line for the keystone and one per equipped weapon");
            Assert.AreEqual("BLINK", Line("SPACE").GetComponent<KeyPrompt>().Action);
            Assert.AreEqual("FIRE LASERS", Line("LMB").GetComponent<KeyPrompt>().Action);
            Assert.AreEqual("FIRE MISSILES", Line("RMB").GetComponent<KeyPrompt>().Action);
            foreach (var line in Lines(hud))
                Assert.AreEqual(1f, Alpha(line), "every line shows until used");
            Assert.Greater(Y(Line("LMB")), Y(Line("RMB")), "the primary line sits above the secondary line");
            Assert.Greater(Y(Line("RMB")), Y(Line("SPACE")), "the secondary line sits above the keystone line");
        }

        [UnityTest]
        public IEnumerator PressingSpace_FadesOnlyTheKeystoneLine()
        {
            Show(() => false);
            yield return null;

            input.Press(keyboard.spaceKey);
            yield return AsyncAssert.WaitUntil(() => !Line("SPACE"), Timeout, "Space fades the keystone line and it goes");
            Assert.IsTrue(Line("LMB") && Line("RMB"), "the weapon lines stay until their own inputs");
        }

        [UnityTest]
        public IEnumerator LeftClick_FadesOnlyThePrimaryLine()
        {
            Show(() => false);
            yield return null;

            input.Press(mouse.leftButton);
            yield return AsyncAssert.WaitUntil(() => !Line("LMB"), Timeout, "a left click fades the primary line and it goes");
            Assert.IsTrue(Line("SPACE") && Line("RMB"), "the other lines stay until their own inputs");
        }

        [UnityTest]
        public IEnumerator RightClick_FadesOnlyTheSecondaryLine()
        {
            Show(() => false);
            yield return null;

            input.Press(mouse.rightButton);
            yield return AsyncAssert.WaitUntil(() => !Line("RMB"), Timeout, "a right click fades the secondary line and it goes");
            Assert.IsTrue(Line("SPACE") && Line("LMB"), "the other lines stay until their own inputs");
        }

        [UnityTest]
        public IEnumerator UnarmedShip_ShowsOnlyTheKeystoneLine()
        {
            Show(() => false, weapons: new FakeWeapons());
            yield return null;

            Assert.AreEqual(1, Lines(hud).Length, "a ship with no weapons gets no fire lines");
            Assert.IsTrue(Line("SPACE"), "the keystone line still shows");
        }

        [UnityTest]
        public IEnumerator Dismissal_FadesEveryLine()
        {
            var dismissed = false;
            Show(() => dismissed);
            yield return null;
            Assert.AreEqual(3, Lines(hud).Length, "test premise: the lines show until dismissed");

            dismissed = true;
            yield return AsyncAssert.WaitUntil(() => Lines(hud).Length == 0, Timeout, "dismissal fades every line and they go");
        }

        [UnityTest]
        public IEnumerator WaitingLines_StayHidden_ThenShow()
        {
            var waiting = true;
            Show(() => false, () => waiting);
            foreach (var line in Lines(hud))
                Assert.AreEqual(0f, Alpha(line), "a line that waits starts hidden");
            yield return null;
            yield return null;
            foreach (var line in Lines(hud))
                Assert.AreEqual(0f, Alpha(line), "a line stays hidden while it waits");

            waiting = false;
            yield return null;
            foreach (var line in Lines(hud))
                Assert.AreEqual(1f, Alpha(line), "the lines show once they stop waiting");
        }

        [UnityTest]
        public IEnumerator DismissedWhileWaiting_GoesUnseen()
        {
            var dismissed = false;
            Show(() => dismissed, () => true);
            yield return null;
            foreach (var line in Lines(hud))
                Assert.AreEqual(0f, Alpha(line), "test premise: the lines wait hidden");

            dismissed = true;
            yield return null;
            yield return null;
            Assert.IsEmpty(Lines(hud), "lines dismissed while they wait go without ever showing");
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
            Assert.IsEmpty(Lines(rig.Overlay.gameObject), "leaving a sector shows nothing");
            var (first, firstDirector) = NewPhase("First sector");
            rig.EnterSector(first);
            var weapons = rig.Player.Weapons.ReadoutContext;
            var lineCount = 1 + weapons.Slots.Count;
            Assert.AreEqual(lineCount, Lines(rig.Overlay.gameObject).Length,
                "the first sector start shows the keystone line and one line per equipped weapon");
            Assert.AreEqual(rig.Player.Keystone.DisplayName.ToUpperInvariant(), LineOn(rig, "SPACE").Action,
                "the keystone line names the keystone the player flies");
            foreach (var slot in weapons.Slots)
                Assert.AreEqual(("FIRE " + weapons.DisplayName(slot)).ToUpperInvariant(),
                    LineOn(rig, slot == WeaponSlot.Primary ? "LMB" : "RMB").Action,
                    $"the {slot} line names the weapon the player carries");

            rig.EnterSector(null);
            rig.EnterSector(NewPhase("Second sector").module);
            yield return null;
            Assert.AreEqual(lineCount, Lines(rig.Overlay.gameObject).Length, "a later sector start shows no second set");

            firstDirector.Advance(41f);
            yield return AsyncAssert.WaitUntil(() => Lines(rig.Overlay.gameObject).Length == 0, Timeout,
                "the first sector's lull after its first sector phase ends every line");
        }

        private void Show(Func<bool> dismissed, Func<bool> waiting = null, IWeaponReadouts weapons = null) =>
            FirstRunPrompt.Show(hud.transform, TestAssets.Load<Font>(KeyFontPath), TestAssets.Load<Font>(WordFontPath),
                null, reader, "Blink", weapons ?? new FakeWeapons(WeaponSlot.Primary, WeaponSlot.Secondary), dismissed,
                waiting ?? (() => false));

        private FirstRunPrompt Line(string key) =>
            Lines(hud).FirstOrDefault(line => line.GetComponent<KeyPrompt>().Key == key);

        private static KeyPrompt LineOn(PlayerRig rig, string key) =>
            Lines(rig.Overlay.gameObject).Select(line => line.GetComponent<KeyPrompt>()).First(line => line.Key == key);

        private static FirstRunPrompt[] Lines(GameObject root) => root.GetComponentsInChildren<FirstRunPrompt>(true);

        private static float Alpha(FirstRunPrompt line) => line.GetComponent<CanvasGroup>().alpha;

        private static float Y(FirstRunPrompt line) => ((RectTransform)line.transform).anchoredPosition.y;

        private (PhaseSpineModule module, WaveDirector director) NewPhase(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(servicesGo.transform, false);
            var director = go.AddComponent<WaveDirector>();
            director.Configure(new[] { new SectorPhase { seconds = 40f }, new SectorPhase { seconds = 40f } }, 5f);
            director.BeginSchedule(0f);
            var module = go.AddComponent<PhaseSpineModule>();
            module.Bind(director, null, null);
            return (module, director);
        }

        private sealed class FakeWeapons : IWeaponReadouts
        {
            private readonly WeaponSlot[] slots;

            public FakeWeapons(params WeaponSlot[] slots) => this.slots = slots;

            public IReadOnlyList<WeaponSlot> Slots => slots;

            public string DisplayName(WeaponSlot slot) => slot == WeaponSlot.Primary ? "Lasers" : "Missiles";

            public Sprite Icon(WeaponSlot slot) => null;

            public IReadOnlyList<IWeaponReadout> Readouts(WeaponSlot slot) => Array.Empty<IWeaponReadout>();
        }
    }
}
#endif
