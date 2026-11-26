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
    public class RunFeedbackScreenPlayModeTests
    {
        private readonly PlayModeInputTestFixture input = new();
        private GameObject root;
        private RunFeedbackScreen screen;

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

        private InputField Field => screen.transform.Find("Panel/Feedback/Body/Field").GetComponent<InputField>();

        [UnityTest]
        public IEnumerator Save_TrimsTheTypedNote_AndTheFirstAnswerStands()
        {
            screen = RunFeedbackScreen.Create(root.transform, null);
            yield return null;
            Assert.IsNull(screen.Note, "unanswered until saved or skipped");
            Assert.AreEqual(RunFeedbackScreen.MaxLength, Field.characterLimit);

            Field.text = "  railgun elite felt unfair  ";
            screen.transform.Find("Panel/Save").GetComponent<Button>().onClick.Invoke();
            screen.Skip();

            Assert.AreEqual("railgun elite felt unfair", screen.Note);
        }

        [UnityTest]
        public IEnumerator Keys_EnterSaves_EscapeSkips()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            screen = RunFeedbackScreen.Create(root.transform, null);
            yield return null;
            Field.text = "too dark";
            input.Press(keyboard.enterKey);
            yield return null;
            input.Release(keyboard.enterKey);
            Assert.AreEqual("too dark", screen.Note);

            Object.DestroyImmediate(screen.gameObject);
            screen = RunFeedbackScreen.Create(root.transform, null);
            yield return null;
            Field.text = "dropped";
            input.Press(keyboard.escapeKey);
            yield return null;
            input.Release(keyboard.escapeKey);
            Assert.AreEqual(string.Empty, screen.Note, "a skip writes nothing");
        }
    }
}
#endif
