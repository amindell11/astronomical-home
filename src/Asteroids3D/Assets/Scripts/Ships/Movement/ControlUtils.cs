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
            var pCmd = Mathf.Sign(diff) * Mathf.Pow(ratio + 0.01f, 1f / 6f);

            var dCmd = -(yawRate / maxYawRate) * dGain;

            var cmd = (pGain * pCmd) + dCmd;
            return Mathf.Clamp(cmd, -1f, 1f);
        }
    }
}