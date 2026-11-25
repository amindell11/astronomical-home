using Combat.Targeting;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>The ship-owned references a kit may read: its body, its size, and the missiles tracking it.</summary>
    public readonly struct KeystoneHost
    {
        public readonly Rigidbody Body;
        public readonly float Radius;
        public readonly LockChannel Lock;

        public KeystoneHost(Rigidbody body, float radius, LockChannel @lock)
        {
            Body = body;
            Radius = radius;
            Lock = @lock;
        }
    }
}
