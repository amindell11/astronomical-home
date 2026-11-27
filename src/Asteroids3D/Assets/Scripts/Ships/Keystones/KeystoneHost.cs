using Combat;
using Combat.Targeting;
using Substrate.Services.Projectiles;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>The ship-owned references a kit may read: the ship its hits credit, its body, its size, the missiles tracking it, and what it fires as.</summary>
    public readonly struct KeystoneHost
    {
        public readonly Ship Ship;
        public readonly Rigidbody Body;
        public readonly float Radius;
        public readonly LockChannel Lock;

        /// <summary>The ship as the shooter of any shot the kit fires; null on a host that fires nothing.</summary>
        public readonly IShooter Shooter;

        /// <summary>Tracks any shot the kit fires; null on a host that fires nothing.</summary>
        public readonly IProjectileService Projectiles;

        public KeystoneHost(Ship ship, Rigidbody body, float radius, LockChannel @lock, IShooter shooter = null,
            IProjectileService projectiles = null)
        {
            Ship = ship;
            Body = body;
            Radius = radius;
            Lock = @lock;
            Shooter = shooter;
            Projectiles = projectiles;
        }
    }
}
