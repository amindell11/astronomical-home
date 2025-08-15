using UnityEngine;

namespace ShipMain.Movement
{
    public readonly struct Outputs
    {
        public readonly Vector2 Thrust, Strafe, Boost;
        public readonly float YawTorque, Bank;

        public Outputs(Vector2 thrust, Vector2 strafe, Vector2 boost, float yawTorque, float bank)
        {
            Thrust = thrust;
            Strafe = strafe;
            Boost = boost;
            YawTorque = yawTorque;
            Bank = bank;
        }

        public static readonly Outputs Zero = new Outputs(Vector2.zero, Vector2.zero, Vector2.zero, 0, 0);
    }
}