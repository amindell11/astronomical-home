using System.Collections;
using System.Collections.Generic;
using Movement;
using NUnit.Framework;
using Player;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// The actuator contract expects a live producer: MovementController latches the last pilot
    /// command across steps, and charge weapons fire on a trigger-up step. Disabling a
    /// PlayerCommander (hangar gate, teardown) must therefore push a neutral pilot command and a
    /// released trigger to every slot — otherwise the ship keeps flying/charging on stale state.
    /// </summary>
    public class PlayerCommanderReleasePlayModeTests : PlayModeWorldFixture
    {
        private sealed class RecordingPilot : IPilot
        {
            public PilotCommand? Last;
            public void Drive(in PilotCommand cmd) => Last = cmd;
        }

        private sealed class RecordingWeapons : IWeapons
        {
            public readonly Dictionary<WeaponSlot, WeaponCommand> Last = new();
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) => Last[slot] = cmd;
        }

        private sealed class StubStatus : IShipStatus
        {
            public StubStatus(Transform transform) => Transform = transform;
            public ShipId Id => default;
            public Transform Transform { get; }
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable => false;
            public float BoostCooldownRemaining => 0f;
            public float MaxSpeed => 0f;
            public float MaxYawRate => 0f;
        }

        private GameObject commanderGo;

        public override void TearDown()
        {
            DestroyTestObject(commanderGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Disable_ReleasesPilotAndWeaponCommands()
        {
            commanderGo = new GameObject("TestCommander");
            var commander = commanderGo.AddComponent<PlayerCommander>();
            var pilot = new RecordingPilot();
            var weapons = new RecordingWeapons();
            commander.Initialize(new ShipControl(
                new StubStatus(commanderGo.transform), pilot, weapons: null, weaponActuator: weapons));

            yield return new WaitForFixedUpdate();
            Assert.IsTrue(pilot.Last.HasValue, "test premise: an enabled commander pushes pilot commands");

            pilot.Last = null;
            weapons.Last.Clear();
            commander.enabled = false;

            Assert.IsTrue(pilot.Last.HasValue, "disabling pushed a final pilot command");
            var cmd = pilot.Last.Value;
            Assert.AreEqual(0f, cmd.thrust, "final pilot command has no thrust");
            Assert.AreEqual(0f, cmd.strafe, "final pilot command has no strafe");
            Assert.AreEqual(0f, cmd.yawTorque, "final pilot command has no yaw");
            Assert.AreEqual(0f, cmd.boost, "final pilot command has no boost");

            foreach (var slot in new[] { WeaponSlot.Primary, WeaponSlot.Secondary })
            {
                Assert.IsTrue(weapons.Last.TryGetValue(slot, out var release),
                    $"disabling pushed a trigger-up step to the {slot} slot");
                Assert.IsFalse(release.held, $"{slot} trigger is released");
                Assert.IsFalse(release.pressed, $"{slot} release carries no press edge");
            }
        }

        [UnityTest]
        public IEnumerator Disable_BeforeInitialize_DoesNotThrow()
        {
            commanderGo = new GameObject("TestCommander");
            var commander = commanderGo.AddComponent<PlayerCommander>();
            yield return null;

            commander.enabled = false;
        }
    }
}
