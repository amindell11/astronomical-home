using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Step 0 characterization of the ship <b>sim contract</b> — the movement/kinematics behavior that
    /// the visual-layer decoupling program (light/shadow strip + visual-rig split, and the later
    /// per-subsystem PRs) must leave <b>byte-for-byte unchanged</b>.
    ///
    /// These tests exist so "no functionality changed" is provable rather than asserted: they run
    /// against the current build to lock behavior, and must keep passing after presentation is moved
    /// off the sim ship. Damage / death / respawn is already covered by
    /// <see cref="ShipRespawnDamagePlayModeTests"/> and is deliberately not duplicated here.
    ///
    /// The ship is created <b>without a commander</b> so the piloting command is fully deterministic
    /// (we push <see cref="PilotCommand"/> straight into <see cref="MovementController.Drive"/> each
    /// physics step, bypassing the AI). We use the Ship_1 prefab specifically because it carries the
    /// full presentation footprint being refactored.
    /// </summary>
    [Category("Integration")]
    [Category("Movement")]
    public class ShipSimInvariancePlayModeTests : PlayModeWorldFixture
    {
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";

        // Physics settle margin before recording a start state: KinematicsPoller (order -100) fills the
        // snapshot and MovementController (order 50) snaps the orientation onto the game plane.
        private const int SettleSteps = 3;

        private Ship ship;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            ship = CreateUnpilotedShip(Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(ship, "Ship_1 test ship failed to instantiate");
#else
            Assert.Ignore("ShipSimInvariancePlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        /// <summary>With no piloting input a ship that starts at rest stays at rest.</summary>
        [UnityTest]
        public IEnumerator NoInput_ShipStaysAtRest()
        {
            yield return Settle();
            var start = ship.Movement.Kinematics;

            for (var i = 0; i < 60; i++)
            {
                ship.Movement.Drive(default);
                yield return new WaitForFixedUpdate();
            }

            var kin = ship.Movement.Kinematics;
            Assert.Less(kin.Speed, 0.5f, "Speed should stay ~0 with no input");
            Assert.Less((kin.pos - start.pos).magnitude, 0.5f, "Position should not drift with no input");
        }

        /// <summary>
        /// Forward thrust accelerates the ship from rest and produces net travel. The thrust vector is
        /// recomputed from the live heading each step (<see cref="Forces"/>), so we characterize the
        /// resulting motion (displacement magnitude, and the initial-heading decomposition for the
        /// record) rather than assuming a perfectly straight path.
        /// </summary>
        [UnityTest]
        public IEnumerator ForwardThrust_AcceleratesShipFromRest()
        {
            yield return Settle();
            var start = ship.Movement.Kinematics;
            var forward = start.Forward;

            for (var i = 0; i < 90; i++)
            {
                ship.Movement.Drive(new PilotCommand { thrust = 1f });
                yield return new WaitForFixedUpdate();
            }

            var kin = ship.Movement.Kinematics;
            var disp = kin.pos - start.pos;
            var along = Vector2.Dot(disp, forward);
            var lateral = (disp - along * forward).magnitude;
            Debug.Log($"[ShipSimInvariance] ForwardThrust — |disp|:{disp.magnitude:F3} along:{along:F3} " +
                      $"lateral:{lateral:F3} yawΔ:{Mathf.DeltaAngle(start.yaw, kin.yaw):F3} speed:{kin.Speed:F3}");

            Assert.Greater(kin.Speed, 1f, "Thrust should accelerate the ship");
            Assert.Greater(disp.magnitude, 2f, "Ship should travel a meaningful distance under thrust");
        }

        /// <summary>Speed is capped at the configured maximum, never exceeding it under sustained thrust.</summary>
        [UnityTest]
        public IEnumerator SustainedThrust_SpeedNeverExceedsMax()
        {
            yield return Settle();
            var maxSpeed = ship.Stats.maxSpeed;

            for (var i = 0; i < 200; i++)
            {
                ship.Movement.Drive(new PilotCommand { thrust = 1f });
                yield return new WaitForFixedUpdate();
                Assert.LessOrEqual(ship.Movement.Kinematics.Speed, maxSpeed * 1.05f,
                    $"Speed exceeded max at step {i}");
            }

            Assert.Greater(ship.Movement.Kinematics.Speed, 1f, "Ship should be moving after sustained thrust");
        }

        /// <summary>Yaw input rotates the ship while translating it only negligibly (it starts at rest).</summary>
        [UnityTest]
        public IEnumerator YawInput_RotatesShip_WithNegligibleTranslation()
        {
            yield return Settle();
            var start = ship.Movement.Kinematics;

            for (var i = 0; i < 60; i++)
            {
                ship.Movement.Drive(new PilotCommand { yawTorque = 1f });
                yield return new WaitForFixedUpdate();
            }

            var kin = ship.Movement.Kinematics;
            var translation = (kin.pos - start.pos).magnitude;
            Debug.Log($"[ShipSimInvariance] YawInput — yawΔ:{Mathf.DeltaAngle(start.yaw, kin.yaw):F3} " +
                      $"translation:{translation:F3} speed:{kin.Speed:F3}");

            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(kin.yaw, start.yaw)), 5f, "Yaw input should rotate the ship");
            Assert.Less(translation, 1.5f, "Pure yaw should not meaningfully translate the ship");
        }

        /// <summary>
        /// The sim is deterministic: two identical ships fed an identical command sequence produce the
        /// same trajectory (relative to each ship's own start). This guards against nondeterminism
        /// creeping in and makes cross-commit golden comparison meaningful for the refactor.
        /// </summary>
        [UnityTest]
        public IEnumerator IdenticalDriveSequence_IsDeterministic()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Requires the Unity Editor.");
            yield break;
#else
            // Spawn a second ship offset from the first — far enough that their colliders never touch
            // (~22x the ship radius), but NOT far from the origin: this test compares the two ships'
            // displacements within a tight 1e-3 tolerance, and float32 precision degrades with world
            // distance (ULP ~1.7e-4 at 1000+ units), which would swamp the determinism signal with
            // position-precision noise rather than measuring real nondeterminism.
            var shipB = CreateUnpilotedShip(new Vector3(30f, 0f, 0f), Quaternion.identity);
            Assert.IsNotNull(shipB, "Second ship failed to instantiate");

            try
            {
                for (var i = 0; i < SettleSteps; i++) yield return new WaitForFixedUpdate();

                var startA = ship.Movement.Kinematics;
                var startB = shipB.Movement.Kinematics;

                const int steps = 60;
                for (var i = 0; i < steps; i++)
                {
                    var cmd = ScriptedCommand(i);
                    ship.Movement.Drive(cmd);
                    shipB.Movement.Drive(cmd);
                    yield return new WaitForFixedUpdate();

                    var a = ship.Movement.Kinematics;
                    var b = shipB.Movement.Kinematics;
                    var dispA = a.pos - startA.pos;
                    var dispB = b.pos - startB.pos;

                    Assert.Less((dispA - dispB).magnitude, 1e-3f, $"Displacement diverged at step {i}");
                    Assert.Less(Mathf.Abs(Mathf.DeltaAngle(a.yaw - startA.yaw, b.yaw - startB.yaw)), 1e-2f,
                        $"Yaw diverged at step {i}");
                }
            }
            finally
            {
                ShipTestFactory.DestroyShip(shipB);
            }
#endif
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static PilotCommand ScriptedCommand(int i) => new()
        {
            thrust = 0.5f + 0.5f * Mathf.Sin(i * 0.3f),
            strafe = 0.3f * Mathf.Sin(i * 0.5f),
            yawTorque = Mathf.Cos(i * 0.2f),
        };

        /// <summary>Step a few physics frames so the kinematics snapshot and plane orientation settle.</summary>
        private static IEnumerator Settle()
        {
            for (var i = 0; i < SettleSteps; i++) yield return new WaitForFixedUpdate();
        }

        private static Ship CreateUnpilotedShip(Vector3 position, Quaternion rotation)
        {
#if UNITY_EDITOR
            var prefab = TestAssets.LoadShipPrefab(Ship1Path);
            if (prefab == null) return null;

            // Null commander → deterministic manual piloting via MovementController.Drive.
            return Factory.CreateShip(prefab, null, 0, 0, position, rotation);
#else
            return null;
#endif
        }
    }
}
