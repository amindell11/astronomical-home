#if UNITY_EDITOR
using System.Collections;
using Game.Player;
using NUnit.Framework;
using Ships;
using Ships.Command;
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
        private PlayModeInputTestFixture input;
        private Ship ship;

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
            DestroyTestObject(ship ? ship.gameObject : null);
            input.TearDown();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator EachFreshPress_RaisesOneRequest_AndADisabledCommanderRaisesNone()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            ship = ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero);
            var commander = ship.gameObject.AddComponent<PlayerCommander>();
            commander.Initialize(new ShipControl(ship, ship.Movement, new SeedScope(0), jump: ship));
            var requests = 0;
            ship.JumpRequested += () => requests++;
            yield return null;

            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.AreEqual(1, requests, "a press raises its request inside the input update");

            yield return null;
            yield return null;
            Assert.AreEqual(1, requests, "holding F raises nothing more");

            input.Release(keyboard.fKey);
            InputSystem.Update();
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.AreEqual(2, requests, "a fresh press raises another");

            input.Release(keyboard.fKey);
            InputSystem.Update();
            commander.enabled = false;
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.AreEqual(2, requests, "a disabled commander raises none");
        }
    }
}
#endif
