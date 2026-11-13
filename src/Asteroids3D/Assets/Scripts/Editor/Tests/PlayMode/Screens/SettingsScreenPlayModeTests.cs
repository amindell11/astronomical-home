#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UI.Screens;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    [Category("UI")]
    public class SettingsScreenPlayModeTests
    {
        private static readonly string[] Resolutions = { "800 x 600", "1920 x 1080", "2560 x 1440" };

        private readonly PlayModeInputTestFixture input = new();
        private GameObject root;
        private SettingsScreen screen;
        private int sliderCalls;
        private float sliderSeen;
        private int toggleCalls;
        private bool toggleSeen;
        private int choiceCalls;
        private int choiceSeen;

        [SetUp]
        public void SetUp()
        {
            input.Setup();
            root = new GameObject("UI");
        }

        [TearDown]
        public void TearDown()
        {
            if (screen) Object.DestroyImmediate(screen.gameObject);
            if (root) Object.DestroyImmediate(root);
            input.TearDown();
        }

        private void Open(float slider, bool toggle, int choice)
        {
            sliderCalls = toggleCalls = choiceCalls = 0;
            screen = SettingsScreen.Create(root.transform, LoadFont(), new SettingsRow[]
            {
                SettingsRow.Slider("MASTER", slider, v =>
                {
                    sliderCalls++;
                    sliderSeen = v;
                }),
                SettingsRow.Toggle("FLIGHT ASSIST", toggle, on =>
                {
                    toggleCalls++;
                    toggleSeen = on;
                }),
                SettingsRow.Choice("RESOLUTION", Resolutions, choice, i =>
                {
                    choiceCalls++;
                    choiceSeen = i;
                })
            });
        }

        private static Font LoadFont() =>
            TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf");

        private string Text(string path) => screen.transform.Find(path).GetComponent<Text>().text;

        private RectTransform Rect(string path) => screen.transform.Find(path).GetComponent<RectTransform>();

        [UnityTest]
        public IEnumerator Create_ShowsTheRows_WithoutNotifying()
        {
            Open(.5f, false, 0);
            yield return null;

            Assert.AreEqual("SETTINGS", Text("Title"));
            Assert.AreEqual("MASTER", Text("Row0/Label"));
            Assert.AreEqual("FLIGHT ASSIST", Text("Row1/Label"));
            Assert.AreEqual("RESOLUTION", Text("Row2/Label"));
            Assert.AreEqual("50%", Text("Row0/Value"));
            Assert.AreEqual("OFF", Text("Row1/Value"));
            Assert.AreEqual("800 x 600", Text("Row2/Value"));
            Assert.AreEqual("BACK", Text("Back/Option"));
            Assert.AreEqual("ESC", Text("Back/Key"));
            Assert.AreEqual(0, sliderCalls + toggleCalls + choiceCalls, "the given values do not notify");
            Assert.IsFalse(screen.Closed);
            Assert.AreEqual(0, screen.Selected);
            Assert.IsTrue(screen.transform.Find("Row0/Marker").gameObject.activeSelf);
            Assert.IsFalse(screen.transform.Find("Row1/Marker").gameObject.activeSelf);

            Assert.AreEqual(new Vector2(0f, 250f), Rect("Title").anchoredPosition);
            Assert.AreEqual(new Vector2(0f, 180f), Rect("Row0").anchoredPosition);
            Assert.AreEqual(new Vector2(0f, 136f), Rect("Row1").anchoredPosition);
            Assert.AreEqual(new Vector2(0f, 92f), Rect("Row2").anchoredPosition);
            Assert.AreEqual(new Vector2(420f, 38f), Rect("Row0").sizeDelta);
            Assert.AreEqual(new Vector2(0f, 24f), Rect("Back").anchoredPosition);
            Assert.AreEqual(new Vector2(340f, 0f), Rect("Row0/Value").anchoredPosition);
            Assert.AreEqual(new Vector2(66f, 38f), Rect("Row0/Value").sizeDelta);
            Assert.AreEqual(new Vector2(130f, 0f), Rect("Row2/Value").anchoredPosition);
            Assert.AreEqual(new Vector2(276f, 38f), Rect("Row2/Value").sizeDelta);
            Assert.AreEqual(TextAnchor.MiddleRight, Rect("Row2/Value").GetComponent<Text>().alignment);
            Assert.IsNotNull(screen.transform.Find("Row0/Slider"));
            Assert.IsNull(screen.transform.Find("Row0/Control"));
            Assert.AreEqual(new Vector2(130f, 0f), Rect("Row1/Control").anchoredPosition);
            Assert.AreEqual(new Vector2(276f, 38f), Rect("Row1/Control").sizeDelta);
        }

        [UnityTest]
        public IEnumerator Slider_NotifiesOnceAndShowsThePercent()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Open(.5f, true, 0);
            yield return null;

            Assert.AreEqual("50%", Text("Row0/Value"));
            screen.transform.Find("Row0/Slider").GetComponent<Slider>().value = .4f;
            Assert.AreEqual(1, sliderCalls);
            Assert.AreEqual(.4f, sliderSeen, 1e-4f);
            Assert.AreEqual("40%", Text("Row0/Value"));

            input.Press(keyboard.rightArrowKey);
            yield return null;
            input.Release(keyboard.rightArrowKey);
            InputSystem.Update();
            Assert.AreEqual(2, sliderCalls);
            Assert.AreEqual(Mathf.Clamp01(.4f + .1f), sliderSeen, 1e-4f);
            Assert.AreEqual("50%", Text("Row0/Value"));

            screen.transform.Find("Row0/Slider").GetComponent<Slider>().value = 1f;
            Assert.AreEqual(3, sliderCalls);
            input.Press(keyboard.rightArrowKey);
            yield return null;
            input.Release(keyboard.rightArrowKey);
            InputSystem.Update();
            Assert.AreEqual(3, sliderCalls, "a step that cannot move does not notify");
            Assert.AreEqual("100%", Text("Row0/Value"));

            screen.transform.Find("Row0/Slider").GetComponent<Slider>().value = 0f;
            Assert.AreEqual(4, sliderCalls);
            input.Press(keyboard.leftArrowKey);
            yield return null;
            input.Release(keyboard.leftArrowKey);
            InputSystem.Update();
            Assert.AreEqual(4, sliderCalls, "a step that cannot move does not notify");
            Assert.AreEqual("0%", Text("Row0/Value"));
            Assert.AreEqual(0, toggleCalls + choiceCalls);
        }

        [UnityTest]
        public IEnumerator Toggle_FlipsFromTheControlAndFromEitherKey()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Open(1f, true, 0);
            yield return null;

            Assert.AreEqual("ON", Text("Row1/Value"));
            Assert.AreEqual(0, toggleCalls);
            screen.transform.Find("Row1/Control").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, toggleCalls);
            Assert.IsFalse(toggleSeen);
            Assert.AreEqual("OFF", Text("Row1/Value"));

            screen.Select(1);
            input.Press(keyboard.rightArrowKey);
            yield return null;
            input.Release(keyboard.rightArrowKey);
            InputSystem.Update();
            Assert.AreEqual(2, toggleCalls);
            Assert.IsTrue(toggleSeen);
            Assert.AreEqual("ON", Text("Row1/Value"));

            input.Press(keyboard.leftArrowKey);
            yield return null;
            input.Release(keyboard.leftArrowKey);
            InputSystem.Update();
            Assert.AreEqual(3, toggleCalls);
            Assert.IsFalse(toggleSeen);
            Assert.AreEqual("OFF", Text("Row1/Value"));
            Assert.AreEqual(0, sliderCalls + choiceCalls);
        }

        [UnityTest]
        public IEnumerator Choice_ClickWraps_KeysClamp()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Open(1f, true, 0);
            yield return null;

            Assert.AreEqual("800 x 600", Text("Row2/Value"));
            Assert.AreEqual(0, choiceCalls);
            screen.Select(2);

            input.Press(keyboard.leftArrowKey);
            yield return null;
            input.Release(keyboard.leftArrowKey);
            InputSystem.Update();
            Assert.AreEqual(0, choiceCalls, "left on the first option does not wrap");
            Assert.AreEqual("800 x 600", Text("Row2/Value"));

            input.Press(keyboard.rightArrowKey);
            yield return null;
            input.Release(keyboard.rightArrowKey);
            InputSystem.Update();
            Assert.AreEqual(1, choiceCalls);
            Assert.AreEqual(1, choiceSeen);
            Assert.AreEqual("1920 x 1080", Text("Row2/Value"));

            input.Press(keyboard.rightArrowKey);
            yield return null;
            input.Release(keyboard.rightArrowKey);
            InputSystem.Update();
            Assert.AreEqual(2, choiceCalls);
            Assert.AreEqual(2, choiceSeen);
            Assert.AreEqual("2560 x 1440", Text("Row2/Value"));

            input.Press(keyboard.rightArrowKey);
            yield return null;
            input.Release(keyboard.rightArrowKey);
            InputSystem.Update();
            Assert.AreEqual(2, choiceCalls, "right on the last option does not wrap");
            Assert.AreEqual("2560 x 1440", Text("Row2/Value"));

            screen.transform.Find("Row2/Control").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(3, choiceCalls);
            Assert.AreEqual(0, choiceSeen);
            Assert.AreEqual("800 x 600", Text("Row2/Value"));

            screen.transform.Find("Row2/Control").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(4, choiceCalls);
            Assert.AreEqual(1, choiceSeen);
            Assert.AreEqual("1920 x 1080", Text("Row2/Value"));
            Assert.AreEqual(0, sliderCalls + toggleCalls);
        }

        [UnityTest]
        public IEnumerator Down_MovesTheMarker()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Open(1f, true, 0);
            yield return null;

            Assert.IsTrue(screen.transform.Find("Row0/Marker").gameObject.activeSelf);
            Assert.IsFalse(screen.transform.Find("Row1/Marker").gameObject.activeSelf);

            input.Press(keyboard.downArrowKey);
            yield return null;
            input.Release(keyboard.downArrowKey);
            InputSystem.Update();

            Assert.AreEqual(1, screen.Selected);
            Assert.IsFalse(screen.transform.Find("Row0/Marker").gameObject.activeSelf);
            Assert.IsTrue(screen.transform.Find("Row1/Marker").gameObject.activeSelf);
            Assert.AreEqual(0, sliderCalls + toggleCalls + choiceCalls, "moving the selection does not change a value");
        }

        [UnityTest]
        public IEnumerator Escape_Closes()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Open(1f, true, 0);
            yield return null;

            input.Press(keyboard.escapeKey);
            yield return null;
            input.Release(keyboard.escapeKey);
            InputSystem.Update();
            Assert.IsTrue(screen.Closed);
            Assert.AreEqual(0, sliderCalls + toggleCalls + choiceCalls);
        }
    }
}
#endif
