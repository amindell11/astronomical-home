#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Common.SimulatedInput
{
    [TestFixture]
    [Category("Core")]
    public class PlayModeInputTestFixturePlayModeTests : PlayModeInputTestFixture
    {
        [UnityTest]
        public IEnumerator PressAfterFixedUpdate_ReachesTheSimulatedKeyboard()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return new WaitForFixedUpdate();

            Press(keyboard.fKey);
            InputSystem.Update();

            Assert.IsTrue(keyboard.fKey.isPressed);
        }

        [Test]
        public void PressAtAnyTestClockReading_ReachesTheSimulatedKeyboard()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            // The edit-to-play window predates this test, so editor uptime bounds it.
            for (var time = 0d; time < EditorApplication.timeSinceStartup; time += .25)
            {
                Press(keyboard.fKey, time);
                Assert.IsTrue(keyboard.fKey.isPressed, $"a press stamped at {time:F2}s");
                Release(keyboard.fKey, time);
            }
        }
    }
}
#endif
