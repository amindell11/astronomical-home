using AI.Navigation.MPC;
using Movement;
using Unity.Mathematics;
using UnityEngine;

namespace AI.Navigation.Aim
{
    /// <summary>The aim servo's law: the Navigator's yaw controller that turns the ship onto the aim reference within the engine's yaw torque. It commands a yaw rate of the reference's rate plus a braking-limited approach on the heading error, then torques toward that rate with angular-drag compensation.</summary>
    public static class AimServo
    {
        private const float TwoPi = 2f * math.PI;

        /// <summary>Normalized yaw torque in [-1, 1], the <see cref="Ships.Command.PilotCommand.yawTorque"/> scale.</summary>
        public static float YawTorque(in Kinematics self, in AimReference reference, in Dynamics dynamics,
            MpcSettings settings)
        {
            var error = reference.heading - self.yaw * Mathf.Deg2Rad;
            error -= TwoPi * math.round(error / TwoPi);
            var accel = dynamics.yawTorque / dynamics.yawInertia * settings.aimServoAccelFraction;
            var approach = math.min(math.sqrt(2f * accel * math.abs(error)), settings.aimServoKp * math.abs(error));
            var targetRate = math.clamp(reference.rate + math.sign(error) * approach,
                -dynamics.maxYawRate, dynamics.maxYawRate);
            var rateError = targetRate - self.yawRate * Mathf.Deg2Rad;
            var torque = dynamics.yawInertia * (dynamics.angularDrag * targetRate + rateError / settings.aimServoTau);
            return math.clamp(torque / dynamics.yawTorque, -1f, 1f);
        }
    }
}
