using Combat.Targeting;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>The ship-owned references a kit may read: the ship its hits credit, its body, its size, and the missiles tracking it.</summary>
    public readonly struct KeystoneHost
    {
        public readonly Ship Ship;
        public readonly Rigidbody Body;
        public readonly float Radius;
        public readonly LockChannel Lock;

        public KeystoneHost(Ship ship, Rigidbody body, float radius, LockChannel @lock)
        {
            Ship = ship;
            Body = body;
            Radius = radius;
            Lock = @lock;
        }
    }
}
