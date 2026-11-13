#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
        private readonly PlayModeInputTestFixture input = new();
        private readonly List<(int row, float value)> changes = new();
        private GameObject root;
        private SettingsScreen screen;

        [SetUp]
        public void SetUp()
        {
            input.Setup();
            root = new GameObject("UI");
            changes.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (screen) Object.DestroyImmediate(screen.gameObject);
            if (root) Object.DestroyImmediate(root);
            input.TearDown();
        }

        private SettingsScreen CreateScreen() => SettingsScreen.Create(root.transform,
            TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf"),
            new[]
            {
                new SettingsScreen.Slider("MASTER", 1f),
                new SettingsScreen.Slider("MUSIC", .5f),
                new SettingsScreen.Slider("EFFECTS", 0f)
            },
            (row, value) => changes.Add((row, value)));

        private string Text(string path) => screen.transform.Find(path).GetComponent<Text>().text;

        [UnityTest]
        public IEnumerator Create_ShowsLabelsAndPercents_WithoutNotifying()
        {
            screen = CreateScreen();
            yield return null;

            Assert.AreEqual("SETTINGS", Text("Title"));
            Assert.AreEqual("MASTER", Text("Row0/Label"));
            Assert.AreEqual("MUSIC", Text("Row1/Label"));
            Assert.AreEqual("EFFECTS", Text("Row2/Label"));
            Assert.AreEqual("100%", Text("Row0/Value"));
            Assert.AreEqual("50%", Text("Row1/Value"));
            Assert.AreEqual("0%", Text("Row2/Value"));
            Assert.IsEmpty(changes, "the given values do not notify");
            Assert.IsFalse(screen.Closed);
            Assert.AreEqual(0, screen.Selected);
        }

        [UnityTest]
        public IEnumerator Slider_NotifiesAndUpdatesThePercent()
        {
            screen = CreateScreen();
            yield return null;

            screen.transform.Find("Row0/Slider").GetComponent<Slider>().value = .4f;

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(0, changes[0].row);
            Assert.AreEqual(.4f, changes[0].value, 1e-4f);
            Assert.AreEqual("40%", Text("Row0/Value"));
        }

        [UnityTest]
        public IEnumerator DownThenRight_StepsTheSecondRow()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            screen = CreateScreen();
            yield return null;

            input.Press(keyboard.downArrowKey);
            yield return null;
            input.Release(keyboard.downArrowKey);
            Assert.AreEqual(1, screen.Selected);
            Assert.IsEmpty(changes, "moving the selection does not change a value");

            input.Press(keyboard.rightArrowKey);
            yield return null;
            input.Release(keyboard.rightArrowKey);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(1, changes[0].row);
            Assert.AreEqual(Mathf.Clamp01(.5f + .1f), changes[0].value, 1e-4f);
            Assert.AreEqual("60%", Text("Row1/Value"));
        }

        [UnityTest]
        public IEnumerator Escape_Closes()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            screen = CreateScreen();
            yield return null;

            input.Press(keyboard.escapeKey);
            yield return null;
            input.Release(keyboard.escapeKey);
            Assert.IsTrue(screen.Closed);
        }
    }
}
#endif
