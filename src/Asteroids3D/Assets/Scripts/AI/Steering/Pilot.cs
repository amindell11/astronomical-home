using AI.Steering;
using Ships.Movement;
using UnityEngine;

namespace AI.Steering
{
    public static class Pilot
    {
        public readonly struct Input
        {
            public readonly Kinematics kin;
            public readonly Vector2 desiredVel;
            public readonly Vector2 desiredAccel;
            public readonly float   maxSpeed;
            public readonly SteeringTuning tuning;
            public readonly bool lockRotation;
            public readonly bool useTiltedHeading;

            public Input(Kinematics k, Vector2 desiredVelocity, Vector2 desiredAcceleration, float max, SteeringTuning tuning, bool lockRotation = false, bool useTiltedHeading = true)
            {
                kin = k;
                desiredVel = desiredVelocity;
                desiredAccel = desiredAcceleration;    
                maxSpeed = max;
                this.tuning = tuning;
                this.lockRotation = lockRotation;
                this.useTiltedHeading = useTiltedHeading;
            }

            public Input(Kinematics k, Vector2 desiredVelocity, Vector2 desiredAcceleration, float max)
                : this(k, desiredVelocity, desiredAcceleration, max, SteeringTuning.Default, false, true) {}
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

        public static Output Compute(Input i)
        {
            var curPos  = i.kin.Pos;
            var curVel  = i.kin.Vel;
            var forward = i.kin.Forward;

            var tuning = i.tuning;
        
            var desiredAcceleration = i.desiredAccel;

            var shipRight   = new Vector2(forward.y, -forward.x);
            var forwardComponent  = Vector2.Dot(desiredAcceleration, forward);
            var strafeComponent   = Vector2.Dot(desiredAcceleration, shipRight);

            var thrust = (forwardComponent >= 0f)
                ? forwardComponent / tuning.ForwardAcc
                : forwardComponent / tuning.ReverseAcc;

            var strafe = strafeComponent / tuning.StrafeAcc;

            if (desiredAcceleration.magnitude < tuning.DeadZone)
            {
                thrust = 0f;
                strafe = 0f;
            }
            else
            {
                thrust = Mathf.Clamp(thrust, -1f, 1f);
                strafe = Mathf.Clamp(strafe, -1f, 1f);
            }

            var rotTargetDeg = i.kin.Yaw;

            if (i.lockRotation || !(i.desiredVel.sqrMagnitude > 0.01f)) return new Output(thrust, strafe, rotTargetDeg);
            var targetDir = i.useTiltedHeading ? ComputeTiltedHeading(i.desiredVel, strafe, tuning) : i.desiredVel.normalized;
            rotTargetDeg = Vector2.SignedAngle(Vector2.up, targetDir);
            if (rotTargetDeg < 0f) rotTargetDeg += 360f;
            return new Output(thrust, strafe, rotTargetDeg);
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

        private static void ComputeBoost(Vector2 desiredDir, Vector2 currentForward, SteeringTuning tuning,
            out float thrustCmd, out float strafeCmd, out float rotTargetDeg)
        {
            var phi = Mathf.Atan2(tuning.StrafeAcc, tuning.ForwardAcc);

            var dirRight = Rotate(desiredDir, +phi).normalized;
            var dirLeft  = Rotate(desiredDir, -phi).normalized;

            var deltaRight = Mathf.Abs(Vector2.SignedAngle(currentForward, dirRight));
            var deltaLeft  = Mathf.Abs(Vector2.SignedAngle(currentForward, dirLeft));

            Vector2 chosenDir;
            if (deltaRight <= deltaLeft)
            {
                chosenDir  = dirRight;
                strafeCmd  = 1f;
            }
            else
            {
                chosenDir  = dirLeft;
                strafeCmd  = -1f;
            }

            thrustCmd = 1f;

            rotTargetDeg = Vector2.SignedAngle(Vector2.up, chosenDir);
            if (rotTargetDeg < 0f) rotTargetDeg += 360f;
        }

        private static Vector2 Rotate(Vector2 v, float angleRad)
        {
            var c = Mathf.Cos(angleRad);
            var s = Mathf.Sin(angleRad);
            return new Vector2(c * v.x - s * v.y,
                s * v.x + c * v.y);
        }
    }
}
