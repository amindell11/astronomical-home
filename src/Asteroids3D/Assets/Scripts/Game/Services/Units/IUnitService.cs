using System;
using Ships;
using Ships.Command;
using UnityEngine;
using Ships.Registry;
using Game.Services.Projectiles;

namespace Game.Services.Units
{
    public interface IUnitService
    {
        /// <summary>Lookup registry for collider/ID/team queries.</summary>
        IShipRegistry Registry { get; }

        /// <summary>All ships currently in play.</summary>
        ShipRegistry ActiveRegistry { get; }

        /// <summary>Projectile registry ships arm their weapons with; arming throws while unset.</summary>
        void SetProjectiles(IProjectileService projectiles);

        /// <summary>Spawn a ship into <paramref name="world"/>, wire its dependencies, and register it.</summary>
        Ship SpawnShip(
            Ship template,
            Commander commander,
            int team,
            Vector3 position,
            Quaternion rotation,
            WorldHandle world);

        /// <summary>Take ownership of an already-instantiated ship (authored as a sector child): wire its child pilot, initialise it from its own settings/team, and register it into <paramref name="world"/>.</summary>
        Ship AdoptShip(Ship ship, WorldHandle world);

        /// <summary>Destroy a single service-owned ship and unregister it, dropping any queued respawn — producer-owned teardown clears sector content while the session-tier player (never passed here) survives.</summary>
        void DespawnShip(Ship ship);

        /// <summary>Destroy all spawned units and clear the registry.</summary>
        void Clear();

        /// <summary>Raised when a ship is spawned through this service.</summary>
        event Action<Ship> OnShipSpawned;
        
        public void RespawnShip(ShipId ship, Vector2 pos, float rotation);
        public void WaitAndRespawnShip(ShipId ship, Vector2 pos, float rotation, float delay);

        /// <summary>Drop all queued (delayed) respawns without reviving their ships.</summary>
        public void CancelPendingRespawns();

        /// <summary>(Re-)push world-scoped dependencies into a ship's world-facing parts; idempotent, runs at spawn/adopt, re-run after a loadout reequip swaps in parts needing wiring. Interim seam: public only because the lock sensor rides a swappable mount.</summary>
        void WireShipDependencies(Ship ship, WorldHandle world);
    }
}
