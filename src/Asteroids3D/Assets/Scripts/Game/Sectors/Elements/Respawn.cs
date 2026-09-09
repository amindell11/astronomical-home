using Game.Services.Units;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Sectors.Elements
{
    /// <summary>Wires a producer-owned <see cref="RespawnPolicy"/> onto a ship: on death, queue a revive (reposition + reset, NOT re-instantiate) via <c>UnitService.WaitAndRespawnShip</c>.</summary>
    public static class Respawn
    {
        /// <summary>
        /// Subscribe a revive to the ship's death; returns false when nothing was wired. The anchor is
        /// producer-relative, snapshotted at WIRE time from <paramref name="origin"/> (default: the
        /// ship itself) so a zero point revives at the spawn position, not wherever the ship drifted
        /// to by death.
        /// </summary>
        public static bool Wire(Ship ship, RespawnPolicy policy, IUnitService units, Transform origin = null)
        {
            if (!policy.Enabled || !ship || units == null || !ship.Damage) return false;

            var producerBase = GamePlane.WorldPointToPlane((origin ? origin : ship.transform).position);
            ship.Damage.OnDeath += (victim, _) =>
                units.WaitAndRespawnShip(victim, Resolve(policy, producerBase), 0f, policy.delay);
            return true;
        }

        /// <summary>
        /// Plane-space revive position: a random point within <c>radius</c> of
        /// <paramref name="producerBase"/> + <c>point</c>. A caller with no live producer transform
        /// (the host's player policy) passes the session frame's offset as the base.
        /// </summary>
        public static Vector2 Resolve(RespawnPolicy policy, Vector2 producerBase = default) =>
            producerBase + policy.point + Random.insideUnitCircle * policy.radius;
    }
}
