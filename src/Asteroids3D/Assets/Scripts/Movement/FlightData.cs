using Game;
using UnityEngine;

namespace Movement
{
    /// <summary>Ship motion state snapshot.</summary>
    public readonly struct Kinematics
    {
        public readonly Vector2 pos, vel;
        public readonly float yaw, yawRate, bank;

        public float Speed => vel.magnitude;
        public Vector2 WorldVel => GamePlane.PlaneDirToWorld(vel);
        public Vector2 Forward => new(-Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));

        public Kinematics(Vector2 pos, Vector2 vel, float yaw, float yawRate, float bank)
            => (this.pos, this.vel, this.yaw, this.yawRate, this.bank) = (pos, vel, yaw, yawRate, bank);
    }

    /// <summary>Force outputs from the flight computer.</summary>
    public readonly struct Outputs
    {
        public readonly Vector2 thrust, strafe, boost;
        public readonly float yawTorque, bank;

        public Outputs(Vector2 thrust, Vector2 strafe, Vector2 boost, float yawTorque, float bank)
            => (this.thrust, this.strafe, this.boost, this.yawTorque, this.bank) = (thrust, strafe, boost, yawTorque, bank);

        public static readonly Outputs Zero = new(Vector2.zero, Vector2.zero, Vector2.zero, 0, 0);
    }
/// <summary>Stores physical dyanmics properties of objects with thrust and rotation abilities</summary>
    public readonly struct Dynamics
    {
        public readonly float mass, maxSpeed, maxYawRate;
        public readonly float forwardAcc, reverseAcc, maxStrafeAcc, minStrafeAcc;
        public readonly float yawTorque, angularDrag, linearDrag;

        public Dynamics(float mass, float forwardAcc, float reverseAcc, float maxStrafeAcc, float minStrafeAcc, 
            float maxSpeed, float maxYawRate, float yawTorque, float angularDrag, float linearDrag)
        {
            (this.mass, this.maxSpeed, this.maxYawRate) = (mass, maxSpeed, maxYawRate);
            (this.forwardAcc, this.reverseAcc) = (forwardAcc, reverseAcc);
            (this.maxStrafeAcc, this.minStrafeAcc) = (maxStrafeAcc, minStrafeAcc);
            (this.yawTorque, this.angularDrag, this.linearDrag) = (yawTorque, angularDrag, linearDrag);
        }

        public static readonly Dynamics Default = new(
            mass: 200, forwardAcc: 8f, reverseAcc: 4f, maxStrafeAcc: 6f, minStrafeAcc: 4f,
            maxSpeed: 20f, maxYawRate: 10f, yawTorque: 1f, angularDrag: 0.1f, linearDrag: 0.1f);
    }
}
