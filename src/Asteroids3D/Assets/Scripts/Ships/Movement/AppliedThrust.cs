using UnityEngine;

namespace Ships.Movement
{
    /// <summary>
    /// The last physics step's applied thrust, published for presentation. Force is ship-right and
    /// ship-forward; yaw is about the plane normal (local +Z) and roll about the forward axis
    /// (local +Y). 1 on an axis is the ship's own full authority there.
    /// </summary>
    public readonly struct AppliedThrust
    {
        public readonly Vector2 Force;
        public readonly float Yaw;
        public readonly float Roll;

        public AppliedThrust(Vector2 force, float yaw, float roll) => (Force, Yaw, Roll) = (force, yaw, roll);
    }
}
