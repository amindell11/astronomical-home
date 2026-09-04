using System;
using Asteroids;
using Game;
using UnityEngine;

namespace AI.Strategy
{
    /// <summary>Pool-safe asteroid identity for sentence referents: component-ref + spawn-epoch stamp,
    /// re-resolvable to live plane kinematics each tick. EXPLICITLY INTERIM (wiring rule 3): an
    /// AsteroidId registry replaces this on its carded trigger — a second consumer of asteroid
    /// identity (#423). Do not copy this pattern to new systems.</summary>
    public readonly struct AsteroidRef : IEquatable<AsteroidRef>
    {
        private readonly AsteroidController asteroid;
        private readonly int spawnEpoch;

        public AsteroidRef(AsteroidController asteroid, int spawnEpoch)
        {
            this.asteroid = asteroid;
            this.spawnEpoch = spawnEpoch;
        }

        /// <summary>Stamps the controller's CURRENT spawn: the ref goes stale when the pool reuses it.</summary>
        public static AsteroidRef Of(AsteroidController asteroid) => new(asteroid, asteroid.SpawnEpoch);

        /// <summary>A seat that was never bound; distinct from a bound-then-despawned referent.</summary>
        public bool IsBound => !ReferenceEquals(asteroid, null);

        /// <summary>Live iff the component survives, is pool-active, and still hosts the same spawn.</summary>
        public bool IsLive => asteroid && asteroid.gameObject.activeInHierarchy && asteroid.SpawnEpoch == spawnEpoch;

        public bool TryResolve(out Vector2 planePos, out Vector2 planeVel)
        {
            if (!IsLive)
            {
                planePos = default;
                planeVel = default;
                return false;
            }
            planePos = GamePlane.WorldPointToPlane(asteroid.transform.position);
            planeVel = GamePlane.WorldDirToPlane(asteroid.Rb.linearVelocity);
            return true;
        }

        public bool Equals(AsteroidRef other) =>
            ReferenceEquals(asteroid, other.asteroid) && spawnEpoch == other.spawnEpoch;

        public override bool Equals(object obj) => obj is AsteroidRef other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ReferenceEquals(asteroid, null) ? 0 : asteroid.GetInstanceID(), spawnEpoch);
    }
}
