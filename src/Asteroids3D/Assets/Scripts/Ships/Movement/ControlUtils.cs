using UnityEngine;

namespace Ships.Movement
{
    public static class ControlUtils
    {
        
        public static float RotationPd(float targetAngle, float yaw, float yawRate, float maxYawRate, float deadZone)
        {
            var diff = Mathf.DeltaAngle(yaw, targetAngle);
            const float pGain = 1f;
            const float dGain = 0.75f;
            const float brakeGain = 0.85f;

            if (Mathf.Abs(diff) <= deadZone)
            {
                var brake = - (yawRate / maxYawRate) * brakeGain;
                return Mathf.Clamp(brake, -1f, 1f);
            }

            var ratio = Mathf.Abs(diff) / 180f;
            
            // Smooth approach: linear ramp near deadzone, power curve for large errors.
            // This prevents the 46% torque spike caused by (ratio + 0.01)^(1/6).
            float pCmd;
            const float rampRange = 0.05f; // 9 degrees
            if (ratio < rampRange)
            {
                pCmd = Mathf.Sign(diff) * (ratio / rampRange) * Mathf.Pow(rampRange, 1f / 6f);
            }
            else
            {
                pCmd = Mathf.Sign(diff) * Mathf.Pow(ratio, 1f / 6f);
            }

            var dCmd = -(yawRate / maxYawRate) * dGain;

            var cmd = (pGain * pCmd) + dCmd;
            return Mathf.Clamp(cmd, -1f, 1f);
        }
    }
}