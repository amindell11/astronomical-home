#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Player;
using Movement;
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
    /// <summary>With the flight assist on, a released movement axis counter-thrusts its drift once the profile's delay has passed; V turns it off.</summary>
    [Category("Core")]
    public class PlayerCommanderAssistPlayModeTests : PlayModeWorldFixture
    {
        private sealed class RecordingPilot : IPilot
        {
            public readonly List<(float at, PilotCommand cmd)> Steps = new();
            public PilotCommand Last => Steps[Steps.Count - 1].cmd;
            public void Drive(in PilotCommand cmd) => Steps.Add((Time.time, cmd));
        }

        private const string ProfilePath = "Assets/Settings/Game/FlightAssist.asset";
        private const float Timeout = 3f;

        private PlayModeInputTestFixture input;
        private Keyboard keyboard;
        private GameObject commanderGo;
        private StubShipStatus status;
        private RecordingPilot pilot;
        private FlightAssistProfile profile;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            input = new PlayModeInputTestFixture();
            input.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            profile = TestAssets.Load<FlightAssistProfile>(ProfilePath);
            var commander = Object.Instantiate(TestAssets.LoadPlayerPilot());
            commanderGo = commander.gameObject;
            status = new StubShipStatus { transform = commanderGo.transform };
            pilot = new RecordingPilot();
            commander.Initialize(new ShipControl(status, pilot, new SeedScope(0)));
        }

        [TearDown]
        public override void TearDown()
        {
            DestroyTestObject(commanderGo);
            input.TearDown();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ReleasingW_BrakesAForwardDrift_OnlyOnceTheDelayHasPassed()
        {
            Drift(new Vector2(0f, FastDrift));
            input.Press(keyboard.wKey);
            yield return AsyncAssert.WaitUntil(() => pilot.Steps.Any(Held), Timeout, "holding W reached the pilot");

            input.Release(keyboard.wKey);
            yield return AsyncAssert.WaitUntil(
                () => pilot.Steps[pilot.Steps.Count - 1].at - LastHeldAt() > profile.delay + Time.fixedDeltaTime,
                Timeout + profile.delay, "the commander drove the pilot past the delay");

            var heldAt = LastHeldAt();
            foreach (var (at, cmd) in pilot.Steps.Where(step => step.at > heldAt))
            {
                var expected = at - heldAt < profile.delay ? 0f : -profile.strength;
                Assert.AreEqual(expected, cmd.thrust, 1e-6f, $"thrust {at - heldAt:F2}s after W was released");
            }
        }

        [UnityTest]
        public IEnumerator HoldingW_ThrustsExactlyTheInput_WhileTheReleasedStrafeBrakesASidewaysDrift()
        {
            Drift(new Vector2(FastDrift, 0f));
            input.Press(keyboard.wKey);
            yield return AsyncAssert.WaitUntil(() => pilot.Steps.Any(Held), Timeout, "holding W reached the pilot");

            Assert.AreEqual(1f, pilot.Last.thrust, "the held axis is exactly the input");
            Assert.AreEqual(-profile.strength, pilot.Last.strafe, 1e-6f, "the released strafe axis brakes the sideways drift");
        }

        [UnityTest]
        public IEnumerator AfterV_ADriftingShipWithNoKeysHeld_GetsNoThrustOrStrafe()
        {
            Drift(new Vector2(FastDrift, FastDrift));
            yield return AsyncAssert.WaitUntil(() => pilot.Steps.Count > 0, Timeout, "the commander drove the pilot");
            Assert.AreEqual(-profile.strength, pilot.Last.thrust, 1e-6f, "test premise: the assist starts on and brakes");

            input.Press(keyboard.vKey);
            yield return null;
            pilot.Steps.Clear();
            yield return AsyncAssert.WaitUntil(() => pilot.Steps.Count > 0, Timeout, "the commander drove the pilot after V");

            Assert.AreEqual(0f, pilot.Last.thrust, "no assist on thrust");
            Assert.AreEqual(0f, pilot.Last.strafe, "no assist on strafe");
        }

        // Past fullBrakeSpeed, so a braking axis spends exactly the profile's strength.
        private float FastDrift => 2f * profile.fullBrakeSpeed;

        private void Drift(Vector2 velocity) => status.kinematics = new Kinematics(Vector2.zero, velocity, 0f, 0f, 0f);

        private static bool Held((float at, PilotCommand cmd) step) => step.cmd.thrust == 1f;

        private float LastHeldAt() => pilot.Steps.Last(Held).at;
    }
}
#endif
