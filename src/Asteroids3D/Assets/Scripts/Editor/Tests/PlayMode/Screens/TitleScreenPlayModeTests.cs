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

        private TitleScreen CreateScreen() => TitleScreen.Create(root.transform,
            TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Michroma.ttf"),
            TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf"));

        private Text TextAt(string path)
        {
            var node = screen.transform.Find(path);
            Assert.IsNotNull(node, $"screen has {path}");
            return node.GetComponent<Text>();
        }

        private string Text(string path) => TextAt(path).text;

        [UnityTest]
        public IEnumerator Create_ShowsTheNameWithPlayAndQuit()
        {
            screen = CreateScreen();
            yield return null;

            Assert.AreEqual("ANDROMEDA", Text("Title"));
            Assert.AreEqual("Michroma", TextAt("Title").font.name);
            Assert.AreEqual("PLAY", Text("Play/Option"));
            Assert.AreEqual("QUIT", Text("Quit/Option"));
            Assert.AreEqual("Rajdhani-Medium", TextAt("Play/Option").font.name);
            Assert.AreEqual("Rajdhani-Medium", TextAt("Quit/Option").font.name);
            Assert.IsFalse(screen.Choice.HasValue, "unanswered until a choice");
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Create_RendersEveryLetterOfTheName()
        {
            screen = CreateScreen();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var title = TextAt("Title");
            Assert.AreEqual(title.text.Length, title.cachedTextGenerator.characterCountVisible,
                "every letter of the name is laid out, none truncated away");
        }

        [UnityTest]
        public IEnumerator Click_PlayOrQuit_Answers()
        {
            screen = CreateScreen();
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
            screen = CreateScreen();
            yield return null;

            screen.transform.Find("Quit").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(TitleScreen.Option.Quit, screen.Choice);
        }

        [UnityTest]
        public IEnumerator Keys_EnterPlays_EscapeQuits()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            screen = CreateScreen();
            yield return null;

            input.Press(keyboard.enterKey);
            yield return null;
            input.Release(keyboard.enterKey);
            Assert.AreEqual(TitleScreen.Option.Play, screen.Choice);

            Object.Destroy(screen.gameObject);
            screen = CreateScreen();
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
            screen = CreateScreen();
            yield return null;

            input.Press(gamepad.buttonSouth);
            yield return null;
            input.Release(gamepad.buttonSouth);
            Assert.AreEqual(TitleScreen.Option.Play, screen.Choice);

            Object.Destroy(screen.gameObject);
            screen = CreateScreen();
            yield return null;

            input.Press(gamepad.buttonEast);
            yield return null;
            input.Release(gamepad.buttonEast);
            Assert.AreEqual(TitleScreen.Option.Quit, screen.Choice);
        }
    }
}
#endif
