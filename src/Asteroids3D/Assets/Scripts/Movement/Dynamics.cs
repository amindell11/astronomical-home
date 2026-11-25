using System.Runtime.InteropServices;

namespace Movement
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Dynamics
    {
        public readonly float mass, maxSpeed, maxYawRate;
        public readonly float forwardAcc, reverseAcc, maxStrafeAcc, minStrafeAcc;
        public readonly float yawTorque, angularDrag, linearDrag;
        public readonly float yawInertia;
        public readonly float bankTorque, bankDamping, maxBankAngleRad;
        public readonly float shipRadius;

        public Dynamics(float mass, float forwardAcc, float reverseAcc, float maxStrafeAcc, float minStrafeAcc,
            float maxSpeed, float maxYawRate, float yawTorque, float angularDrag, float linearDrag,
            float yawInertia = 0f,
            float bankTorque = 0f, float bankDamping = 0f, float maxBankAngleRad = 0f,
            float shipRadius = 1f)
        {
            (this.mass, this.maxSpeed, this.maxYawRate) = (mass, maxSpeed, maxYawRate);
            (this.forwardAcc, this.reverseAcc) = (forwardAcc, reverseAcc);
            (this.maxStrafeAcc, this.minStrafeAcc) = (maxStrafeAcc, minStrafeAcc);
            (this.yawTorque, this.angularDrag, this.linearDrag) = (yawTorque, angularDrag, linearDrag);
            // Avoid control flow in struct constructor (Burst padding bug).
            // Callers must pass yawInertia > 0 or pass mass explicitly.
            this.yawInertia = yawInertia;
            (this.bankTorque, this.bankDamping, this.maxBankAngleRad) = (bankTorque, bankDamping, maxBankAngleRad);
            this.shipRadius = shipRadius;
        }

        public Dynamics Scaled(float speedScale, float thrustScale, float maneuverabilityScale = 1f) => new(
            mass, forwardAcc * thrustScale, reverseAcc, maxStrafeAcc * maneuverabilityScale, minStrafeAcc * maneuverabilityScale,
            maxSpeed * speedScale, maxYawRate * maneuverabilityScale, yawTorque * maneuverabilityScale, angularDrag, linearDrag,
            yawInertia, bankTorque, bankDamping, maxBankAngleRad, shipRadius);

        public static readonly Dynamics Default = new(
            mass: 200, forwardAcc: 8f, reverseAcc: 4f, maxStrafeAcc: 6f, minStrafeAcc: 4f,
            maxSpeed: 20f, maxYawRate: 10f, yawTorque: 1f, angularDrag: 0.1f, linearDrag: 0.1f,
            yawInertia: 200);
    }
}
