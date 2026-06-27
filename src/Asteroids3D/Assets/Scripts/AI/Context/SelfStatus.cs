using Ships.Command;
using Movement;
using UnityEngine;

namespace AI.Context
{
    /// <summary>
    /// Live introspection of the AI's own ship: kinematics and durability,
    /// read straight from the ship each access. The "self" half of the world model.
    /// </summary>
    public class SelfStatus
    {
        private readonly Ships.Ship ship;

        public SelfStatus(Ships.Ship ship)
        {
            this.ship = ship;
        }
        public Vector3 Pos3D => ship.transform.position;
        public State State => ship?.CurrentState ?? default(State);
        public Kinematics Kin => State.kinematics;
        public Vector2 Pos => Kin.pos;
        public Vector2 Vel => Kin.vel;
        public Vector2 Forward => Kin.Forward;
        public float Yaw => Kin.yaw;
        public float SpeedPct => Kin.Speed / (ship?.settings.maxSpeed ?? 1f);
        public float ShieldPct => State.shieldPct;
        public float HealthPct => State.healthPct;
    }
}
