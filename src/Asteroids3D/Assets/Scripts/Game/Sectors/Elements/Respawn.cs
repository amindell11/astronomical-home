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
        /// <para>
        /// For <c>FixedPoint</c> the anchor is <b>producer-relative</b>: the producer's plane position is
        /// snapshotted <b>now</b> (wire/spawn time) from <paramref name="origin"/> — defaulting to the
        /// ship's own transform — and <c>policy.point</c> is an offset from it, so a default (zero) point
        /// revives the ship exactly where it started rather than wherever it drifted to by death. For
        /// <c>FollowerRelative</c> the anchor is resolved at death time (tracking the world follower).
        /// </para>
        /// </summary>
        public static bool Wire(Ship ship, RespawnPolicy policy, IGameServices services, Transform origin = null)
        {
            if (!policy.Enabled || !ship || services == null || !ship.Damage) return false;

            var producerBase = GamePlane.WorldPointToPlane((origin ? origin : ship.transform).position);
            ship.Damage.OnDeath += (victim, _) =>
                services.UnitService.WaitAndRespawnShip(
                    victim, Resolve(policy, services, producerBase), 0f, policy.delay);
            return true;
        }

        /// <summary>
        /// Plane-space revive position for a policy: a random point within <c>radius</c> of the resolved
        /// anchor. For <c>FixedPoint</c> the anchor is <paramref name="producerBase"/> plus the policy's
        /// offset <c>point</c> (producer-relative). For <c>FollowerRelative</c> it is the world follower's
        /// plane position, falling back to the arena origin when no follower exists. A caller with no
        /// live producer transform (the driver's player policy) passes the arena offset as
        /// <paramref name="producerBase"/> so an authored <c>point</c> resolves arena-relative.
        /// </summary>
        public static Vector2 Resolve(RespawnPolicy policy, IGameServices services, Vector2 producerBase = default)
        {
            var anchor = policy.origin == RespawnPolicy.Origin.FollowerRelative
                ? FollowerAnchor(services)
                : producerBase + policy.point;
            return anchor + Random.insideUnitCircle * policy.radius;
        }

        private static Vector2 FollowerAnchor(IGameServices services)
        {
            var follower = services?.EnvironmentService?.WorldFollowerTransform;
            if (follower) return GamePlane.WorldPointToPlane(follower.position);
            return services?.Arena?.Offset ?? Vector2.zero;
        }
    }
}
