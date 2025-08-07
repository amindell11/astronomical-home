using ShipMain;
using UnityEngine;
using ShipMain.Movement;

namespace EnemyAI
{
    public static class AIPilot
    {
        // --- Tunable Parameters ---
        // ─────────────────────────────────────────────────────────────────────────────
        // NOTE: Steering/acceleration tunables have been centralised in the new
        //       SteeringConstants utility. Import the static class so we can use
        //       ForwardAcceleration, ReverseAcceleration, etc. directly.
        // ─────────────────────────────────────────────────────────────────────────────

        // Note: MaxSpeed is read from the Ship component itself.

        // --- New typed IO structs for modular pipeline ---------------------------
        public readonly struct Input
        {
            public readonly Kinematics kin;
            public readonly Vector2 desiredVel;
            public readonly Vector2 desiredAccel;
            public readonly float   maxSpeed;
            public readonly SteeringTuning tuning;
            public readonly bool lockRotation;    // if true, keep current ship heading (VelocityPilot will not command rotation)
            public readonly bool useTiltedHeading; // if false, skip tilt logic and just face desired velocity

            // Constructor that uses explicit tuning parameters (preferred)
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

            // Back-compat constructor – falls back to default tuning values.
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

        /// <summary>
        /// New modular Compute entry point that maps desired velocity to control commands.
        /// Internally reuses the legacy ComputeInputs for now so existing behaviour remains.
        /// </summary>
        public static Output Compute(Input i)
        {
            // Maps a desired world-space acceleration directly onto thrust/strafe axes.
            Vector2 curPos  = i.kin.Pos;
            Vector2 curVel  = i.kin.Vel;
            Vector2 forward = i.kin.Forward;

            float  thrust, strafe, rotTargetDeg;
            var    tuning = i.tuning;
        
            /* ------------------------------------------------------------------------
         * Translate desired acceleration → axis commands
         * ---------------------------------------------------------------------*/
            Vector2 desiredAcceleration = i.desiredAccel;

            // Project desired acceleration onto ship axes
            Vector2 shipRight   = new Vector2(forward.y, -forward.x);
            float   forwardComponent  = Vector2.Dot(desiredAcceleration, forward);
            float   strafeComponent   = Vector2.Dot(desiredAcceleration, shipRight);

            // Map to normalised commands using per-axis accel limits
            thrust = (forwardComponent >= 0f)
                ? forwardComponent / tuning.ForwardAcc
                : forwardComponent / tuning.ReverseAcc; // braking / reverse

            strafe = strafeComponent / tuning.StrafeAcc;

            // Dead-zone and clamp
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

            /* ------------------------------------------------------------------------
         * Heading control
         * ---------------------------------------------------------------------*/
            rotTargetDeg = i.kin.Yaw;  // Default to current heading

            if (!i.lockRotation && i.desiredVel.sqrMagnitude > 0.01f)
            {
                Vector2 targetDir;
                if (i.useTiltedHeading)
                {
                    // Choose a heading that re-uses the boost geometry to balance strafe & thrust
                    targetDir = ComputeTiltedHeading(i.desiredVel, strafe, tuning);
                }
                else
                {
                    targetDir = i.desiredVel.normalized;
                }
                rotTargetDeg = Vector2.SignedAngle(Vector2.up, targetDir);
                if (rotTargetDeg < 0f) rotTargetDeg += 360f;
            }
            return new Output(thrust, strafe, rotTargetDeg);
        }

        /// <summary>
        /// Computes a heading direction that intentionally tilts the ship such that the
        /// combination of available forward <paramref name="tuning.ForwardAcc"/> and strafe
        /// <paramref name="tuning.StrafeAcc"/> accelerations better matches the desired
        /// world-space velocity vector.  For small strafe commands the tilt is negligible;
        /// as the strafe demand approaches ±1 the tilt angle approaches the optimal boost
        /// angle <c>atan(StrafeAcc / ForwardAcc)</c>.
        /// </summary>
        static Vector2 ComputeTiltedHeading(Vector2 desiredVel, float strafeCmd, SteeringTuning tuning)
        {
            // If we barely need to strafe, default to facing the velocity vector.
            float absStrafe = Mathf.Abs(strafeCmd);
            if (absStrafe < 0.05f)
                return desiredVel.normalized;

            // Maximum useful tilt when using full boost geometry.
            float maxTilt = Mathf.Atan2(tuning.StrafeAcc, tuning.ForwardAcc); // radians

            // Scale tilt proportionally with how much strafe authority we are currently using.
            float tilt = maxTilt * absStrafe; // 0 → face desiredVel, 1 → full boost tilt

            // Direction of rotation depends on strafe sign (right strafe = +1 rotates +tilt).
            float sign = (strafeCmd >= 0f) ? +1f : -1f;

            return Rotate(desiredVel.normalized, sign * tilt).normalized;
        }

        /// <summary>
        /// Calculates control commands that exploit both forward and strafe axes to
        /// maximise acceleration along <paramref name="desiredDir"/>. Assumes we
        /// want full-throttle on both axes while the boost is active.
        /// </summary>
        static void ComputeBoost(Vector2 desiredDir, Vector2 currentForward, SteeringTuning tuning,
            out float thrustCmd, out float strafeCmd, out float rotTargetDeg)
        {
            // Magnitude of heading offset required to cancel lateral component when using full strafe
            float phi = Mathf.Atan2(tuning.StrafeAcc, tuning.ForwardAcc);

            // Two candidate headings: tilt right (+φ, strafe +1) or tilt left (-φ, strafe –1)
            Vector2 dirRight = Rotate(desiredDir, +phi).normalized;
            Vector2 dirLeft  = Rotate(desiredDir, -phi).normalized;

            // Compute absolute angular difference from current heading for each candidate
            float deltaRight = Mathf.Abs(Vector2.SignedAngle(currentForward, dirRight));
            float deltaLeft  = Mathf.Abs(Vector2.SignedAngle(currentForward, dirLeft));

            Vector2 chosenDir;
            if (deltaRight <= deltaLeft)
            {
                // Use right strafe
                chosenDir  = dirRight;
                strafeCmd  = 1f;
            }
            else
            {
                // Use left strafe (negative command)
                chosenDir  = dirLeft;
                strafeCmd  = -1f;
            }

            // Full forward thrust during boost
            thrustCmd = 1f;

            // Desired heading angle in degrees (Unity convention: 0° = up, CCW positive)
            rotTargetDeg = Vector2.SignedAngle(Vector2.up, chosenDir);
            if (rotTargetDeg < 0f) rotTargetDeg += 360f;
        }

        // Simple 2D vector rotation helper (radians, CCW positive)
        static Vector2 Rotate(Vector2 v, float angleRad)
        {
            float c = Mathf.Cos(angleRad);
            float s = Mathf.Sin(angleRad);
            return new Vector2(c * v.x - s * v.y,
                s * v.x + c * v.y);
        }
    }
}
