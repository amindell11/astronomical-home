using AI.Steering;
using Ships.Movement;
using UnityEngine;

namespace AI.Steering
{
    public class Pilot
    {
        public readonly struct Input
        {
            public readonly Kinematics kin;
            public readonly Vector2 desiredVel;
            public readonly Vector2 desiredAccel;
            public readonly float   maxSpeed;
            public readonly float?  facingTargetDeg;
            public readonly bool useTiltedHeading;

            public Input(Kinematics k, Vector2 desiredVelocity, Vector2 desiredAcceleration, float max, float? facingTarget = null, bool useTiltedHeading = true)
            {
                kin = k;
                desiredVel = desiredVelocity;
                desiredAccel = desiredAcceleration;    
                maxSpeed = max;
                this.facingTargetDeg = facingTarget;
                this.useTiltedHeading = useTiltedHeading;
            }
        }

        public readonly struct Output
        {
            public readonly float thrust;
            public readonly float strafe;
            public readonly float rotTargetDeg;

            public Output(float t, float s, float r)
            {
                thrust = t; strafe = s; rotTargetDeg = r;
            }
        }

        private readonly SteeringTuning tuning;
        private readonly float proportionalGain;
        private float smoothThrust;
        private float smoothStrafe;

        public Pilot(SteeringTuning tuning, float proportionalGain)
        {
            this.tuning = tuning;
            this.proportionalGain = proportionalGain;
            this.smoothThrust = 0f;
            this.smoothStrafe = 0f;
        }

        public Output Compute(Input i)
        {
            ComputeRawControls(i.kin.Forward, i.desiredAccel, out var rawThrust, out var rawStrafe);
            ApplySmoothing(rawThrust, rawStrafe);
            var rotTargetDeg = ComputeRotationTarget(i);
            
            return new Output(smoothThrust, smoothStrafe, rotTargetDeg);
        }

        private void ComputeRawControls(Vector2 forward, Vector2 desiredAccel, out float thrust, out float strafe)
        {
            var shipRight = new Vector2(forward.y, -forward.x);
            var forwardComponent = Vector2.Dot(desiredAccel, forward);
            var strafeComponent = Vector2.Dot(desiredAccel, shipRight);

            thrust = (forwardComponent >= 0f)
                ? forwardComponent / tuning.ForwardAcc
                : forwardComponent / tuning.ReverseAcc;

            strafe = strafeComponent / tuning.StrafeAcc;

            if (desiredAccel.magnitude < tuning.DeadZone)
            {
                thrust = 0f;
                strafe = 0f;
            }
            else
            {
                thrust = Mathf.Clamp(thrust, -1f, 1f);
                strafe = Mathf.Clamp(strafe, -1f, 1f);
            }
        }

        private void ApplySmoothing(float rawThrust, float rawStrafe)
        {
            var a = 1f - Mathf.Exp(-proportionalGain * Time.fixedDeltaTime);
            smoothThrust = Mathf.Lerp(smoothThrust, rawThrust, a);
            smoothStrafe = Mathf.Lerp(smoothStrafe, rawStrafe, a);
        }

        private float ComputeRotationTarget(Input i)
        {
            if (i.facingTargetDeg.HasValue)
                return i.facingTargetDeg.Value;

            if (!(i.desiredVel.sqrMagnitude > 0.01f))
                return i.kin.Yaw;

            var targetDir = i.useTiltedHeading 
                ? ComputeTiltedHeading(i.desiredVel, smoothStrafe, tuning) 
                : i.desiredVel.normalized;
            
            var angle = Vector2.SignedAngle(Vector2.up, targetDir);
            return angle < 0f ? angle + 360f : angle;
        }

        private static Vector2 ComputeTiltedHeading(Vector2 desiredVel, float strafeCmd, SteeringTuning tuning)
        {
            var absStrafe = Mathf.Abs(strafeCmd);
            if (absStrafe < 0.05f)
                return desiredVel.normalized;

            var maxTilt = Mathf.Atan2(tuning.StrafeAcc, tuning.ForwardAcc);
            var tilt = maxTilt * absStrafe;
            var sign = (strafeCmd >= 0f) ? +1f : -1f;

            return Rotate(desiredVel.normalized, sign * tilt).normalized;
        }

        private static Vector2 Rotate(Vector2 v, float angleRad)
        {
            var c = Mathf.Cos(angleRad);
            var s = Mathf.Sin(angleRad);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }
    }
}
