using NUnit.Framework;
using Ships.Command;
using Ships.Loadout;
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
            Assert.AreEqual(Outputs.Zero.yawTorque, outs.yawTorque);
            Assert.AreEqual(Outputs.Zero.bank, outs.bank);
        }

        [Test]
        public void Strafe_FallsOffAgainstTheLiveCap()
        {
            var settings = CreateTestSettings();
            var atBaseCap = CreateKinematics(Vector2.zero, new Vector2(0f, settings.maxSpeed), 0f);
            var cmd = CreateCommand(strafe: 1f);

            var baseStrafe = Forces.ComputeOutputs(atBaseCap, cmd, settings).strafe.magnitude;
            var raisedStrafe = Forces.ComputeOutputs(atBaseCap, cmd, settings, speedScale: 1.5f).strafe.magnitude;

            Assert.AreEqual(settings.minStrafeForce, baseStrafe, 1e-3f, "at the cap, strafe is at its floor");
            Assert.Greater(raisedStrafe, baseStrafe, "with the cap raised, the same speed keeps more strafe authority");
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
            Assert.Greater(outs.yawTorque, 0f);
        }
    }
}
