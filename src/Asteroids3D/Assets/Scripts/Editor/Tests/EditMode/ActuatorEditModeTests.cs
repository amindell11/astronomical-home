using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Movement;
using UnityEngine;

namespace Tests.EditMode
{
    public class ForcesEditModeTests
    {
        private Settings CreateTestSettings()
        {
            var s = ScriptableObject.CreateInstance<Settings>();
            s.forwardAccel = 1000f;
            s.reverseAccel = 500f;
            s.maxStrafeForce = 800f;
            s.minStrafeForce = 400f;
            s.maxSpeed = 20f;
            s.yawTorque = 300f;
            s.maxBankAngle = 45f;
            s.bankingSpeed = 5f;
            s.boostImpulse = 2000f;
            s.boostCooldown = 1.0f;
            return s;
        }

        private static Command CreateCommand(
            float thrust = 0f,
            float strafe = 0f,
            float boost = 0f,
            float yawTorque = 0f)
        {
            return new Command
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
