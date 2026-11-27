#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat.Weapons;
using Game.Player;
using NUnit.Framework;
using Ships.Command;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UI.Prompts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Prompts
{
    [Category("UI")]
    public class FirstRunPromptPlayModeTests : PlayModeWorldFixture
    {
        private const string KeyFontPath = "Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf";
        private const string WordFontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";
        private const float Timeout = 2f;

        private PlayModeInputTestFixture input;
        private Keyboard keyboard;
        private Mouse mouse;
        private PlayerInputReader reader;
        private GameObject hud;

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
        public IEnumerator ShowMovement_TeachesThrustStrafeAndAim()
        {
            ShowMovement(() => false);
            yield return null;

            Assert.AreEqual(3, Lines(hud).Length, "one line each for thrust, strafe and aim");
            Assert.AreEqual("THRUST", Line("W/S").GetComponent<KeyPrompt>().Action);
            Assert.AreEqual("STRAFE", Line("A/D").GetComponent<KeyPrompt>().Action);
            Assert.AreEqual("AIM", Line("MOUSE").GetComponent<KeyPrompt>().Action);
            Assert.Greater(Y(Line("W/S")), Y(Line("A/D")), "thrust sits above strafe");
            Assert.Greater(Y(Line("A/D")), Y(Line("MOUSE")), "strafe sits above aim");
        }

        [UnityTest]
        public IEnumerator PressingW_FadesOnlyTheThrustLine()
        {
            ShowMovement(() => false);
            yield return null;

            input.Press(keyboard.wKey);
            yield return AsyncAssert.WaitUntil(() => !Line("W/S"), Timeout, "W fades the thrust line and it goes");
            Assert.IsTrue(Line("A/D") && Line("MOUSE"), "the other lines stay until their own inputs");
        }

        [UnityTest]
        public IEnumerator PressingD_FadesOnlyTheStrafeLine()
        {
            ShowMovement(() => false);
            yield return null;

            input.Press(keyboard.dKey);
            yield return AsyncAssert.WaitUntil(() => !Line("A/D"), Timeout, "D fades the strafe line and it goes");
            Assert.IsTrue(Line("W/S") && Line("MOUSE"), "the other lines stay until their own inputs");
        }

        [UnityTest]
        public IEnumerator MovingTheMouse_FadesOnlyTheAimLine()
        {
            input.Set(mouse.position, new Vector2(400f, 300f));
            yield return null;
            ShowMovement(() => false);
            yield return null;

            input.Set(mouse.position, new Vector2(410f, 305f));
            yield return null;
            yield return null;
            Assert.IsTrue(Line("MOUSE"), "a nudge of the mouse is not yet aiming");

            input.Set(mouse.position, new Vector2(500f, 300f));
            yield return AsyncAssert.WaitUntil(() => !Line("MOUSE"), Timeout, "moving the mouse fades the aim line and it goes");
            Assert.IsTrue(Line("W/S") && Line("A/D"), "the other lines stay until their own inputs");
        }

        private void ShowMovement(Func<bool> dismissed) =>
            FirstRunPrompt.ShowMovement(hud.transform, TestAssets.Load<Font>(KeyFontPath),
                TestAssets.Load<Font>(WordFontPath), null, reader, dismissed, () => false);

        private void Show(Func<bool> dismissed, Func<bool> waiting = null, IWeaponReadouts weapons = null) =>
            FirstRunPrompt.Show(hud.transform, TestAssets.Load<Font>(KeyFontPath), TestAssets.Load<Font>(WordFontPath),
                null, reader, "Blink", weapons ?? new FakeWeapons(WeaponSlot.Primary, WeaponSlot.Secondary), dismissed,
                waiting ?? (() => false));

        private FirstRunPrompt Line(string key) =>
            Lines(hud).FirstOrDefault(line => line.GetComponent<KeyPrompt>().Key == key);

        private static FirstRunPrompt[] Lines(GameObject root) => root.GetComponentsInChildren<FirstRunPrompt>(true);

        private static float Alpha(FirstRunPrompt line) => line.GetComponent<CanvasGroup>().alpha;

        private static float Y(FirstRunPrompt line) => ((RectTransform)line.transform).anchoredPosition.y;

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
