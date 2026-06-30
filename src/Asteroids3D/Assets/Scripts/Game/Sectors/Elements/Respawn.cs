using Game.Services;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Sectors
{
    /// <summary>
    /// Shared helper that wires a <see cref="RespawnPolicy"/> onto a ship: on death, queue a revive
    /// (reposition + reset, NOT re-instantiate) via <c>UnitService.WaitAndRespawnShip</c>. Centralises
    /// the respawn math the sector subclasses previously inlined, so producers (spawners, adopt
    /// entries, the player field) own respawn declaratively.
    /// </summary>
    public static class Respawn
    {
        /// <summary>
        /// Subscribe a respawn to <paramref name="ship"/>'s death using <paramref name="policy"/>.
        /// Returns true if a subscription was wired (false for a disabled policy / missing ship/services).
        /// The revive position is resolved at death time (so <c>FollowerRelative</c> tracks the
        /// follower's location when the ship dies).
        /// </summary>
        public static bool Wire(Ship ship, RespawnPolicy policy, IGameServices services)
        {
            if (!policy.Enabled || !ship || services == null || !ship.Damage) return false;

            ship.Damage.OnDeath += (victim, _) =>
                services.UnitService.WaitAndRespawnShip(victim, Resolve(policy, services), 0f, policy.delay);
            return true;
        }

        /// <summary>
        /// Plane-space revive position for a policy: a random point within <c>radius</c> of the
        /// resolved anchor (the fixed point, or the world follower's plane position).
        /// </summary>
        public static Vector2 Resolve(RespawnPolicy policy, IGameServices services)
        {
            var anchor = policy.origin == RespawnPolicy.Origin.FollowerRelative
                ? FollowerAnchor(services)
                : policy.point;
            return anchor + Random.insideUnitCircle * policy.radius;
        }

        private static Vector2 FollowerAnchor(IGameServices services)
        {
            var follower = services?.EnvironmentService?.WorldFollowerTransform;
            return follower ? GamePlane.WorldPointToPlane(follower.position) : Vector2.zero;
        }
    }
}
