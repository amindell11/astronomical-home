using UnityEngine;

namespace Ships.Movement
{
    public class Handling
    {
        
        internal float RotationPD(float targetAngle, float yaw, float yawRate, float maxYawRate, float deadZone)
        {
            // PD-style controller: P on angle error, D on yaw rate (deg/s)
            var diff = Mathf.DeltaAngle(yaw, targetAngle);
            const float pGain = 1f;
            const float dGain = 0.75f;
            const float brakeGain = 0.85f;

            // Within deadzone: primarily brake rotational velocity for smooth settle
            if (Mathf.Abs(diff) <= deadZone)
            {
                var brake = - (yawRate / maxYawRate) * brakeGain;
                return Mathf.Clamp(brake, -1f, 1f);
            }

            // Proportional term uses existing non-linear mapping for strong authority at large errors
            var ratio = Mathf.Abs(diff) / 180f;
            var pCmd = Mathf.Sign(diff) * Mathf.Pow(ratio + 0.01f, 1f / 6f);

            // Derivative term damps based on current rotation speed
            var dCmd = -(yawRate / maxYawRate) * dGain;

            var cmd = (pGain * pCmd) + dCmd;
            return Mathf.Clamp(cmd, -1f, 1f);
        }
    }
}