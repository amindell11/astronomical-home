using UnityEngine;

namespace Ships.Movement
{
    /// <summary>
    /// The last physics step's applied thrust, published for presentation. Force is ship-right and
    /// ship-forward; yaw is about the plane normal and roll about the forward axis. 1 on an axis is
    /// the ship's own full authority there.
    /// </summary>
    public readonly struct AppliedThrust
    {
        // ship frame: x = ship-right, y = ship-forward; 1 = the ship's full authority on that axis
        public readonly Vector2 Force;

        // torque about the ship's local +Z (the plane normal); 1 = full yaw torque
        public readonly float Yaw;

        // torque about the ship's local +Y (its forward axis); 1 = full bank torque
        public readonly float Roll;

        public AppliedThrust(Vector2 force, float yaw, float roll) => (Force, Yaw, Roll) = (force, yaw, roll);
    }
}
