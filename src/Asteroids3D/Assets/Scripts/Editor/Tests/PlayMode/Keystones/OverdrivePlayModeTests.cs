using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Ships.Keystones.Kits;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Keystones
{
    [Category("Ships")]
    public class OverdrivePlayModeTests : PlayModeWorldFixture
    {
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private Ship valis;

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(valis);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Overdrive_FliesPastTheBaseCap_ThenBleedsBackRatherThanSnapping()
        {
            valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            valis.Initialize(0, 0, Projectiles);
            var overdrive = (Overdrive)valis.Keystone;
            var baseCap = valis.Stats.maxSpeed;
            var full = new PilotCommand { thrust = 1f };

            for (var i = 0; i < 200; i++)
            {
                valis.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(valis.Kinematics.Speed, Is.LessThanOrEqualTo(baseCap + 0.01f), "the base cap holds before overdrive");

            valis.Movement.Drive(new PilotCommand { thrust = 1f, boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(overdrive.Active, Is.True);
            Assert.That(valis.Dynamics.maxSpeed, Is.EqualTo(baseCap * overdrive.PeakSpeedScale).Within(0.01f), "the live model carries the raised cap");

            var peak = 0f;
            while (overdrive.Active)
            {
                valis.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, valis.Kinematics.Speed);
            }
            Assert.That(peak, Is.GreaterThan(baseCap * 1.15f), "overdrive flies well past the base cap");
            Assert.That(overdrive.PaidActivations, Is.EqualTo(1));
            Assert.That(valis.Dynamics.maxSpeed, Is.EqualTo(baseCap).Within(0.01f), "the model returns to base when the window closes");

            for (var i = 0; i < 10; i++)
            {
                valis.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(valis.Kinematics.Speed, Is.GreaterThan(baseCap + 1f), "0.2 s after the window the speed is still bleeding, not snapped");

            for (var i = 0; i < 150; i++)
            {
                valis.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(valis.Kinematics.Speed, Is.LessThanOrEqualTo(baseCap + 0.05f), "and settles back at the base cap");
        }

        [UnityTest]
        public IEnumerator Overdrive_RaisesTurningAndStrafe_ThenRestoresThem()
        {
            valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            valis.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            var normal = valis.Dynamics;
            valis.Movement.Drive(new PilotCommand { strafe = 1f, yawTorque = 1f, boost = 1f });
            yield return new WaitForFixedUpdate();

            Assert.That(valis.Keystone.Active, Is.True);
            Assert.That(valis.Movement.dbgStrafe.magnitude, Is.EqualTo(normal.maxStrafeAcc * 1.5f).Within(.01f));
            Assert.That(valis.Movement.dbgYaw, Is.EqualTo(normal.yawTorque * 1.5f).Within(.01f));
            Assert.That(valis.Dynamics.maxStrafeAcc, Is.EqualTo(normal.maxStrafeAcc * 1.5f));
            Assert.That(valis.Dynamics.minStrafeAcc, Is.EqualTo(normal.minStrafeAcc * 1.5f));
            Assert.That(valis.Dynamics.yawTorque, Is.EqualTo(normal.yawTorque * 1.5f));
            Assert.That(valis.Dynamics.maxYawRate, Is.EqualTo(normal.maxYawRate * 1.5f));
            Assert.That(valis.Rigidbody.maxAngularVelocity, Is.EqualTo(valis.Dynamics.maxYawRate).Within(.001f));
            Assert.That(valis.MaxYawRate * Mathf.Deg2Rad, Is.EqualTo(valis.Dynamics.maxYawRate).Within(.001f));

            while (valis.Keystone.Active) yield return new WaitForFixedUpdate();

            Assert.That(valis.Movement.dbgYaw, Is.EqualTo(normal.yawTorque).Within(.01f));
            Assert.That(valis.Dynamics.maxStrafeAcc, Is.EqualTo(normal.maxStrafeAcc));
            Assert.That(valis.Dynamics.minStrafeAcc, Is.EqualTo(normal.minStrafeAcc));
            Assert.That(valis.Dynamics.yawTorque, Is.EqualTo(normal.yawTorque));
            Assert.That(valis.Dynamics.maxYawRate, Is.EqualTo(normal.maxYawRate));
            Assert.That(valis.Rigidbody.maxAngularVelocity, Is.EqualTo(normal.maxYawRate).Within(.001f));
            Assert.That(valis.MaxYawRate * Mathf.Deg2Rad, Is.EqualTo(normal.maxYawRate).Within(.001f));
        }

        [UnityTest]
        public IEnumerator ResetDuringOverdrive_RestoresTheBaseCaps()
        {
            valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            valis.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            valis.Movement.Drive(new PilotCommand { thrust = 1f, boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(valis.Rigidbody.maxLinearVelocity, Is.GreaterThan(valis.Stats.maxSpeed), "overdrive raised the cap");
            Assert.That(valis.Rigidbody.maxAngularVelocity, Is.GreaterThan(valis.Stats.maxYawRate * Mathf.Deg2Rad));

            valis.ResetShip();

            Assert.That(valis.Rigidbody.maxLinearVelocity, Is.EqualTo(valis.Stats.maxSpeed), "an in-place respawn starts at the base cap");
            Assert.That(valis.Rigidbody.maxAngularVelocity, Is.EqualTo(valis.Stats.maxYawRate * Mathf.Deg2Rad));
            Assert.That(valis.MaxYawRate, Is.EqualTo(valis.Stats.maxYawRate));
            Assert.That(valis.Dynamics.yawTorque, Is.EqualTo(valis.Stats.yawTorque));
            Assert.That(valis.Dynamics.maxStrafeAcc, Is.EqualTo(valis.Stats.maxStrafeForce));
        }
    }
}
