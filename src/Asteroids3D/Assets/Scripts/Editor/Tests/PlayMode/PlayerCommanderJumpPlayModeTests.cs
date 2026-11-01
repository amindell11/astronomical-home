#if UNITY_EDITOR
using System.Collections;
using Game.Player;
using NUnit.Framework;
using Ships.Command;
using Tests.Common;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>F reaches the player's ship as one jump request per fresh press, raised inside the input update; a disabled commander raises none.</summary>
    [Category("Core")]
    public class PlayerCommanderJumpPlayModeTests : PlayModeWorldFixture
    {
        private sealed class IdlePilot : IPilot
        {
            public void Drive(in PilotCommand cmd) { }
        }

        private sealed class CountingJumpDrive : IJumpDrive
        {
            public int Requests;
            public void RequestJump() => Requests++;
        }

        private PlayModeInputTestFixture input;
        private GameObject commanderGo;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            input = new PlayModeInputTestFixture();
            input.Setup();
        }

        [TearDown]
        public override void TearDown()
        {
            DestroyTestObject(commanderGo);
            input.TearDown();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator EachFreshPress_RaisesOneRequest_AndADisabledCommanderRaisesNone()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var commander = Object.Instantiate(TestAssets.LoadPlayerPilot());
            commanderGo = commander.gameObject;
            var jump = new CountingJumpDrive();
            commander.Initialize(new ShipControl(new StubShipStatus { transform = commanderGo.transform },
                new IdlePilot(), new SeedScope(0), jump: jump));
            yield return null;

            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.AreEqual(1, jump.Requests, "a press raises its request inside the input update");

            yield return null;
            yield return null;
            Assert.AreEqual(1, jump.Requests, "holding F raises nothing more");

            input.Release(keyboard.fKey);
            InputSystem.Update();
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.AreEqual(2, jump.Requests, "a fresh press raises another");

            input.Release(keyboard.fKey);
            InputSystem.Update();
            commander.enabled = false;
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.AreEqual(2, jump.Requests, "a disabled commander raises none");
        }
    }
}
#endif
