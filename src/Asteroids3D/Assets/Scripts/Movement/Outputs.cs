using UnityEngine;

namespace Movement
{
    public readonly struct Outputs
    {
        public readonly Vector2 thrust, strafe;
        public readonly float yawTorque, bank;

        public Outputs(Vector2 thrust, Vector2 strafe, float yawTorque, float bank)
            => (this.thrust, this.strafe, this.yawTorque, this.bank) = (thrust, strafe, yawTorque, bank);

        public static readonly Outputs Zero = new(Vector2.zero, Vector2.zero, 0, 0);
    }
}
