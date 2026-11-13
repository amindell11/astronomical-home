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
    public class PauseScreenPlayModeTests
    {
        private readonly PlayModeInputTestFixture input = new();
        private GameObject root;
        private PauseScreen screen;

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

        private PauseScreen CreateScreen() =>
            PauseScreen.Create(root.transform, TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf"));

        private string Text(string path) => screen.transform.Find(path).GetComponent<Text>().text;

        [UnityTest]
        public IEnumerator Create_ShowsPausedAndBothCaptions()
        {
            screen = CreateScreen();
            yield return null;

            Assert.AreEqual("PAUSED", Text("Title"));
            Assert.AreEqual("Rajdhani-Medium", screen.transform.Find("Title").GetComponent<Text>().font.name);
            Assert.AreEqual("RESUME", Text("Resume/Option"));
            Assert.AreEqual("QUIT RUN", Text("QuitRun/Option"));
            Assert.AreEqual("ESC", Text("Resume/Key"));
            Assert.AreEqual("Q", Text("QuitRun/Key"));
            Assert.IsFalse(screen.Choice.HasValue, "unanswered until a choice");
        }

        [UnityTest]
        public IEnumerator Click_EachOption_SetsItsChoice()
        {
            screen = CreateScreen();
            yield return null;

            var resume = screen.transform.Find("Resume").GetComponent<Button>();
            Assert.AreEqual(Navigation.Mode.None, resume.navigation.mode);
            resume.onClick.Invoke();
            Assert.AreEqual(PauseScreen.Option.Resume, screen.Choice);
            screen.transform.Find("QuitRun").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(PauseScreen.Option.Resume, screen.Choice, "the first answer stands");

            Object.Destroy(screen.gameObject);
            screen = CreateScreen();
            yield return null;
            screen.transform.Find("QuitRun").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(PauseScreen.Option.QuitRun, screen.Choice);
        }

        [UnityTest]
        public IEnumerator Keys_EscapeResumes_QQuits()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            screen = CreateScreen();
            yield return null;

            input.Press(keyboard.escapeKey);
            yield return null;
            input.Release(keyboard.escapeKey);
            Assert.AreEqual(PauseScreen.Option.Resume, screen.Choice);

            Object.Destroy(screen.gameObject);
            screen = CreateScreen();
            yield return null;

            input.Press(keyboard.qKey);
            yield return null;
            input.Release(keyboard.qKey);
            Assert.AreEqual(PauseScreen.Option.QuitRun, screen.Choice);
        }
    }
}
#endif
