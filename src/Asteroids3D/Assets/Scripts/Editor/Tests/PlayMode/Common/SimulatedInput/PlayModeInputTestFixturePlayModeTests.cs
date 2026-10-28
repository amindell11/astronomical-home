#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
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
    }
}
#endif
