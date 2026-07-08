using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Movement;
using Movement;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Physics")]
    public class ForcesEditModeTests
    {
        private ResolvedShipStats CreateTestSettings()
        {
            return new ResolvedShipStats
            {
                forwardForce = 1000f,
                reverseForce = 500f,
                maxStrafeForce = 800f,
                minStrafeForce = 400f,
                maxSpeed = 20f,
                yawTorque = 300f,
                maxBankAngle = 45f,
                bankTorque = 5000f,
                boostImpulse = 2000f,
                boostCooldown = 1.0f,
            };
        }

        private static PilotCommand CreateCommand(
            float thrust = 0f,
            float strafe = 0f,
            float boost = 0f,
            float yawTorque = 0f)
        {
            return new PilotCommand
            {
                thrust = thrust,
                strafe = strafe,
                boost = boost,
                yawTorque = yawTorque
            };
        }

        private static Kinematics CreateKinematics(Vector2 pos, Vector2 vel, float yawDeg)
        {
            return new Kinematics(pos, vel, yawDeg, 0f, 0f);
        }

        [Test]
        [Category("Smoke")]
        public void ComputeOutputs_ReturnsZero_WhenSettingsNotProvided()
        {
            var kin = CreateKinematics(Vector2.zero, Vector2.zero, 0f);
            var cmd = CreateCommand(thrust: 1f);

            var outs = Forces.ComputeOutputs(kin, cmd, null);
            Assert.AreEqual(Outputs.Zero.thrust, outs.thrust);
            Assert.AreEqual(Outputs.Zero.strafe, outs.strafe);
            Assert.AreEqual(Outputs.Zero.boost, outs.boost);
            Assert.AreEqual(Outputs.Zero.yawTorque, outs.yawTorque);
            Assert.AreEqual(Outputs.Zero.bank, outs.bank);
        }

        [Test]
        public void ComputeOutputs_ComputesForces_FromCommandAndKinematics()
        {
            var settings = CreateTestSettings();
            var kin = CreateKinematics(Vector2.zero, Vector2.zero, 0f);
            var cmd = CreateCommand(thrust: 1f, strafe: 1f, yawTorque: 0.5f);

            var outs = Forces.ComputeOutputs(kin, cmd, settings);

            Assert.Greater(outs.thrust.magnitude, 0f);
            Assert.Greater(outs.strafe.magnitude, 0f);
            Assert.AreEqual(0f, outs.boost.magnitude);
            Assert.Greater(outs.yawTorque, 0f);
        }
    }
}
