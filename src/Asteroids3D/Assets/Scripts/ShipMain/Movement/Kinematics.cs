using Game;
using UnityEngine;

namespace ShipMain.Movement
{
    /// <summary>
    /// Immutable snapshot of the ship motion state that is passed through the
    /// guidance pipeline (AIShipInput → PathPlanner → VelocityPilot).
    /// Extracted into Shared assembly so both Game.Core and Game.AI can reference it.
    /// </summary>
    public readonly struct Kinematics
    {
        public readonly Vector2 Pos; // plane-space position
        public readonly Vector2 Vel; // plane-space velocity
        public readonly float Yaw; // yaw in degrees
        public readonly float YawRate; // yaw rate in degrees per second
        public readonly float Bank; // bank in degrees

        public float Speed => Vel.magnitude;
        public float LocalVel => Vector2.Dot(Vel, Forward);
        public Vector2 Forward => new Vector2(-Mathf.Sin(Yaw * Mathf.Deg2Rad), Mathf.Cos(Yaw * Mathf.Deg2Rad));
        public Vector3 WorldVel => GamePlane.PlanePointToWorld(Vel);

        public Kinematics(Vector2 pos, Vector2 vel, float yaw, float yawRate, float bank)
        {
            this.Pos = pos;
            this.Vel = vel;
            this.Yaw = yaw;
            this.YawRate = yawRate;
            this.Bank = bank;
        }
    }
}