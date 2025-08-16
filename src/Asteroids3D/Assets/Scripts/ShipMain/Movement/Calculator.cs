using UnityEngine;

namespace ShipMain.Movement
{
    public static class Calculator
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

        internal static float YawTorque(Kinematics kin, float yawTorque, bool rotateToTarget, float targetAngle, float yawDeadZone, float rotationThrust)
        {
            float mag = 0;
            if (yawTorque != 0.0f)
                mag = yawTorque;
            else if (rotateToTarget)
                mag = RotationalThrustToTarget(targetAngle, kin.Yaw, yawDeadZone);
            
            return mag * rotationThrust;
        }
    
        internal static float Bank(Kinematics kin, float input, float maxBankAngle, float bankSpeed)
        {
            float targetBank = -input * maxBankAngle;
            float currentBank = kin.Bank;
            float bank = Mathf.Lerp(currentBank, targetBank, bankSpeed * Time.fixedDeltaTime);
            return bank;
        }

        internal static float RotationalThrustToTarget(float targetAngle, float yaw, float deadZone)
        {
            float diff = Mathf.DeltaAngle(yaw, targetAngle);
            if (!(Mathf.Abs(diff) > deadZone))  return 0;
        
            float ratio   = Mathf.Abs(diff) / 180f;
            float mult    = Mathf.Pow(ratio + 0.01f, 1f / 6f)   ;
            return Mathf.Sign(diff) * mult;
        }
    }
}