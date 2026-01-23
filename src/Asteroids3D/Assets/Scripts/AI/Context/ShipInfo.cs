using Ships;
using Ships.Control;
using Ships.Movement;
using UnityEngine;

namespace AI.Context
{
    public class ShipInfo
    {
        private readonly Ships.Ship ship;

        public ShipInfo(Ships.Ship ship)
        {
            this.ship = ship;
        }
        public Vector3 Pos3D => ship.transform.position;
        public State State => ship?.CurrentState ?? default(State);
        public Kinematics Kin => State.Kinematics;
        public Vector2 Pos => Kin.Pos;
        public Vector2 Vel => Kin.Vel;
        public Vector2 Forward => Kin.Forward;
        public float Yaw => Kin.Yaw;
        public float SpeedPct => Kin.Speed / (ship?.settings.maxSpeed ?? 1f);
        public float ShieldPct => State.ShieldPct;
        public float HealthPct => State.HealthPct;
    }
}
