using NUnit.Framework;
using Ships;
using Ships.Movement;
using UnityEngine;

namespace Tests.EditMode
{
    public class ActuatorEditModeTests
    {
        private Settings CreateTestSettings()
        {
            var s = ScriptableObject.CreateInstance<Settings>();
            s.forwardAccel = 1000f;
            s.reverseAccel = 500f;
            s.maxStrafeForce = 800f;
            s.minStrafeForce = 400f;
            s.maxSpeed = 20f;
            s.rotationThrust = 300f;
            s.yawDeadZone = 0f;
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
            float yawTorque = 0f,
            float targetAngle = 0f,
            bool rotateToTarget = false)
        {
            return new Command
            {
                Thrust = thrust,
                Strafe = strafe,
                Boost = boost,
                YawTorque = yawTorque,
                TargetAngle = targetAngle,
                RotateToTarget = rotateToTarget,
            };
        }

        private static Kinematics CreateKinematics(Vector2 pos, Vector2 vel, float yawDeg)
        {
            return new Kinematics(pos, vel, yawDeg, 0f, 0f);
        }

        [Test]
        public void Outputs_AreZero_WhenSettingsNotProvided()
        {
            var actuator = new FlightComputer();
            var kin = CreateKinematics(Vector2.zero, Vector2.zero, 0f);
            actuator.SetCommand(CreateCommand(thrust: 1f));

            var outs = actuator.Process(kin);
            Assert.AreEqual(Outputs.Zero.Thrust, outs.Thrust);
            Assert.AreEqual(Outputs.Zero.Strafe, outs.Strafe);
            Assert.AreEqual(Outputs.Zero.Boost, outs.Boost);
            Assert.AreEqual(Outputs.Zero.YawTorque, outs.YawTorque);
            Assert.AreEqual(Outputs.Zero.Bank, outs.Bank);
        }

        [Test]
        public void State_IsStored_WhenSettersAreCalled()
        {
            var actuator = new FlightComputer();
            var settings = CreateTestSettings();
            actuator.PopulateSettings(settings);

            var cmd = CreateCommand(thrust: 0.75f, strafe: 0.5f, yawTorque: 0.25f);
            actuator.SetCommand(cmd);
            var kin = CreateKinematics(Vector2.zero, Vector2.zero, 0f);
            actuator.SetKinematics(kin);

            Assert.AreEqual(cmd, actuator.CurrentCommand);
            Assert.AreEqual(kin, actuator.Kinematics);
        }

        [Test]
        public void GetOutputs_Computes_Forces_From_Command_And_Kinematics()
        {
            var actuator = new FlightComputer();
            var settings = CreateTestSettings();
            actuator.PopulateSettings(settings);

            var kin = CreateKinematics(Vector2.zero, Vector2.zero, 0f); // Forward = (0,1)
            actuator.SetCommand(CreateCommand(thrust: 1f, strafe: 1f, yawTorque: 0.5f));

            var outs = actuator.Process(kin);

            Assert.Greater(outs.Thrust.magnitude, 0f);
            Assert.Greater(outs.Strafe.magnitude, 0f);
            Assert.AreEqual(0f, outs.Boost.magnitude); // boost not requested in this test
            Assert.Greater(outs.YawTorque, 0f);
        }
    }
}

