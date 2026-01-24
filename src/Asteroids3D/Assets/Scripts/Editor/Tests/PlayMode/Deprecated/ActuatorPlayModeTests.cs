using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Movement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public class ActuatorPlayModeTests
    {
        private Settings CreateTestSettings()
        {
            var s = ScriptableObject.CreateInstance<Settings>();
            s.forwardForce = 0f;
            s.reverseForce = 0f;
            s.maxStrafeForce = 0f;
            s.minStrafeForce = 0f;
            s.maxSpeed = 20f;
            s.yawTorque = 0f;
            s.maxBankAngle = 0f;
            s.bankingSpeed = 0f;
            s.boostImpulse = 10f;
            s.boostCooldown = 0.25f;
            return s;
        }

        // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
        // [UnityTest]
        // public IEnumerator Boost_Observes_Cooldown_In_PlayMode()
        // {
        //     var actuator = new FlightComputer();
        //     var settings = CreateTestSettings();
        //     actuator.PopulateSettings(settings);
        //     actuator.SetCommand(new Command { Boost = 1f });

        //     var kin = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f);

        //     // First boost should trigger
        //     var first = actuator.Process(kin);
        //     Assert.Greater(first.Boost.magnitude, 0f);

        //     // Immediately try again: should be cooled down -> zero
        //     var second = actuator.Process(kin);
        //     Assert.AreEqual(0f, second.Boost.magnitude);

        //     // Wait for cooldown
        //     yield return new WaitForSeconds(settings.boostCooldown + 0.01f);

        //     // Now boost should be available again
        //     var third = actuator.Process(kin);
        //     Assert.Greater(third.Boost.magnitude, 0f);
        // }
    }
}

