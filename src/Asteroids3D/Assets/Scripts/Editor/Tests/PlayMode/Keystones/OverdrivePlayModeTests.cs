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
        public IEnumerator ResetDuringOverdrive_RestoresTheBaseCap()
        {
            valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            valis.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            valis.Movement.Drive(new PilotCommand { thrust = 1f, boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(valis.Rigidbody.maxLinearVelocity, Is.GreaterThan(valis.Stats.maxSpeed), "overdrive raised the cap");

            valis.ResetShip();

            Assert.That(valis.Rigidbody.maxLinearVelocity, Is.EqualTo(valis.Stats.maxSpeed), "an in-place respawn starts at the base cap");
        }
    }
}
