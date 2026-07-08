using System;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Game.Services
{
    public interface IUnitService
    {
        /// <summary>Lookup registry for collider/ID/team queries.</summary>
        IShipRegistry Registry { get; }

        /// <summary>All ships currently in play.</summary>
        ShipRegistry ActiveRegistry { get; }

        /// <summary>Spawn a ship, wire its dependencies, and register it.</summary>
        Ship SpawnShip(
            Ship template,
            Commander commander,
            int team,
            Vector3 position,
            Quaternion rotation);

        /// <summary>
        /// Take ownership of an already-instantiated ship (authored as a sector child): wire its
        /// child pilot, initialise it from its own settings/team, and register it.
        /// </summary>
        Ship AdoptShip(Ship ship);

        /// <summary>
        /// Destroy a single service-owned ship (a spawner product or adopted ship) and unregister it,
        /// dropping any queued respawn for it. Used by producer-owned teardown so sector content is
        /// cleared on restart while the session-tier player (never passed here) survives.
        /// </summary>
        void DespawnShip(Ship ship);

        /// <summary>Destroy all spawned units and clear the registry.</summary>
        void Clear();

        /// <summary>Raised when a ship is spawned through this service.</summary>
        event Action<Ship> OnShipSpawned;
        
        public void RespawnShip(ShipId ship, Vector2 pos, float rotation);
        public void WaitAndRespawnShip(ShipId ship, Vector2 pos, float rotation, float delay);

        /// <summary>Drop all queued (delayed) respawns without reviving their ships.</summary>
        public void CancelPendingRespawns();
    }
}
