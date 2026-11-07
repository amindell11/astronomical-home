using Substrate;
using UnityEngine;

namespace Movement
{
    public readonly struct Kinematics
    {
        public readonly Vector2 pos, vel;
        public readonly float yaw, yawRate, bank;

        public float Speed => vel.magnitude;
        public Vector3 WorldVel => GamePlane.PlaneDirToWorld(vel);
        public Vector2 Forward => new(-Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));

        public Kinematics(Vector2 pos, Vector2 vel, float yaw, float yawRate, float bank)
            => (this.pos, this.vel, this.yaw, this.yawRate, this.bank) = (pos, vel, yaw, yawRate, bank);
    }
}
