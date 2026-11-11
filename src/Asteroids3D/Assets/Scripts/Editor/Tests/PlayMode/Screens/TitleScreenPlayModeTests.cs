#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
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
    public class TitleScreenPlayModeTests
    {
        private readonly PlayModeInputTestFixture input = new();
        private GameObject root;
        private TitleScreen screen;

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

        private string Text(string path)
        {
            var node = screen.transform.Find(path);
            Assert.IsNotNull(node, $"screen has {path}");
            return node.GetComponent<Text>().text;
        }

        [UnityTest]
        public IEnumerator Create_ShowsTheNameWithPlayAndQuit()
        {
            screen = TitleScreen.Create(root.transform);
            yield return null;

            Assert.AreEqual("ASTRONOMICAL: SURVIVAL", Text("Title"));
            Assert.AreEqual("PLAY", Text("Play/Option"));
            Assert.AreEqual("QUIT", Text("Quit/Option"));
            Assert.IsFalse(screen.Choice.HasValue, "unanswered until a choice");
        }

        [UnityTest]
        public IEnumerator Click_PlayOrQuit_Answers()
        {
            screen = TitleScreen.Create(root.transform);
            yield return null;

            var play = screen.transform.Find("Play").GetComponent<Button>();
            var quit = screen.transform.Find("Quit").GetComponent<Button>();
            Assert.AreEqual(Navigation.Mode.None, play.navigation.mode);
            Assert.AreEqual(Navigation.Mode.None, quit.navigation.mode);
            play.onClick.Invoke();
            Assert.AreEqual(TitleScreen.Option.Play, screen.Choice);
            quit.onClick.Invoke();
            Assert.AreEqual(TitleScreen.Option.Play, screen.Choice, "the first answer stands");

            Object.Destroy(screen.gameObject);
            screen = TitleScreen.Create(root.transform);
            yield return null;

            screen.transform.Find("Quit").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(TitleScreen.Option.Quit, screen.Choice);
        }

        [UnityTest]
        public IEnumerator Keys_EnterPlays_EscapeQuits()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            screen = TitleScreen.Create(root.transform);
            yield return null;

            input.Press(keyboard.enterKey);
            yield return null;
            input.Release(keyboard.enterKey);
            Assert.AreEqual(TitleScreen.Option.Play, screen.Choice);

            Object.Destroy(screen.gameObject);
            screen = TitleScreen.Create(root.transform);
            yield return null;

            input.Press(keyboard.escapeKey);
            yield return null;
            input.Release(keyboard.escapeKey);
            Assert.AreEqual(TitleScreen.Option.Quit, screen.Choice);
        }

        [UnityTest]
        public IEnumerator Gamepad_SouthPlays_EastQuits()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            screen = TitleScreen.Create(root.transform);
            yield return null;

            input.Press(gamepad.buttonSouth);
            yield return null;
            input.Release(gamepad.buttonSouth);
            Assert.AreEqual(TitleScreen.Option.Play, screen.Choice);

            Object.Destroy(screen.gameObject);
            screen = TitleScreen.Create(root.transform);
            yield return null;

            input.Press(gamepad.buttonEast);
            yield return null;
            input.Release(gamepad.buttonEast);
            Assert.AreEqual(TitleScreen.Option.Quit, screen.Choice);
        }
    }
}
#endif
