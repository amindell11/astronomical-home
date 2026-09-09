using Game.Services;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Sectors.Elements
{
    /// <summary>Wires a producer-owned <see cref="RespawnPolicy"/> onto a ship: on death, queue a revive (reposition + reset, NOT re-instantiate) via <c>UnitService.WaitAndRespawnShip</c>.</summary>
    public static class Respawn
    {
        /// <summary>
        /// Subscribe a revive to the ship's death; returns false when nothing was wired. FixedPoint
        /// anchors are producer-relative, snapshotted at WIRE time from <paramref name="origin"/>
        /// (default: the ship itself) so a zero point revives at the spawn position, not wherever the
        /// ship drifted to by death; FollowerRelative resolves at death time (tracking the follower).
        /// </summary>
        public static bool Wire(Ship ship, RespawnPolicy policy, IGameServices services, Vector2 worldOrigin,
            Transform origin = null)
        {
            if (!policy.Enabled || !ship || services == null || !ship.Damage) return false;

            var producerBase = GamePlane.WorldPointToPlane((origin ? origin : ship.transform).position);
            ship.Damage.OnDeath += (victim, _) =>
                services.UnitService.WaitAndRespawnShip(
                    victim, Resolve(policy, services, worldOrigin, producerBase), 0f, policy.delay);
            return true;
        }

        /// <summary>
        /// Plane-space revive position: a random point within <c>radius</c> of the anchor —
        /// FixedPoint = <paramref name="producerBase"/> + <c>point</c>; FollowerRelative = the world
        /// follower's plane position, falling back to <paramref name="worldOrigin"/>. A caller with no
        /// live producer transform (the driver's player policy) passes the world origin as the base.
        /// </summary>
        public static Vector2 Resolve(RespawnPolicy policy, IGameServices services, Vector2 worldOrigin,
            Vector2 producerBase = default)
        {
            var anchor = policy.origin == RespawnPolicy.Origin.FollowerRelative
                ? FollowerAnchor(services, worldOrigin)
                : producerBase + policy.point;
            return anchor + Random.insideUnitCircle * policy.radius;
        }

        private static Vector2 FollowerAnchor(IGameServices services, Vector2 worldOrigin)
        {
            var follower = services?.EnvironmentService?.WorldFollowerTransform;
            return follower ? GamePlane.WorldPointToPlane(follower.position) : worldOrigin;
        }
    }
}
