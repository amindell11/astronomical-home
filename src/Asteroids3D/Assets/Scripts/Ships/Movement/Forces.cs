using UnityEngine;

namespace Ships.Movement
{
    public struct Forces
    {
        internal static Vector2 Boost(Kinematics kin, float input, float strength)
        {
            if (input < 0f) return Vector2.zero;
            var boostForce = kin.Forward * (strength * Mathf.Clamp01(input));
            return boostForce;
        }

        internal static Vector2 Thrust(Kinematics kin, float input, float forwardAccl, float revAccl)
        {
            var mag = input >= 0 ? forwardAccl : revAccl;
            var thrust = kin.Forward * (input * mag);
            return thrust;
        }

        internal static Vector2 Strafe(Kinematics kin, float input, float maxStrafeForce, float minStrafeForce, float maxSpeed)
        {
            var speedPct = kin.Vel.magnitude / maxSpeed;
            var mag = Mathf.Lerp(maxStrafeForce, minStrafeForce, speedPct);
            var right = new Vector2(kin.Forward.y, -kin.Forward.x);
            var strafeV = right * (input * mag);
            return strafeV;
        }

        internal static float YawTorque(Kinematics kin, float input, float rotationThrust)
        {
            return input * rotationThrust;
        }

        internal static float Yaw(Kinematics kin, float yawTorque, float rotationDrag)
        {
            return kin.Yaw + yawTorque * Time.fixedDeltaTime;
        }
    
        internal static float Bank(Kinematics kin, float input, float maxBankAngle, float bankSpeed)
        {
            var targetBank = -input * maxBankAngle;
            var currentBank = kin.Bank;
            var bank = Mathf.Lerp(currentBank, targetBank, bankSpeed * Time.fixedDeltaTime);
            return bank;
        }

    }
}