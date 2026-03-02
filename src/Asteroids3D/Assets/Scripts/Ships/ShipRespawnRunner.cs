using System;
using Game;
using UnityEngine;

namespace Ships
{
    public class ShipRespawnRunner : MonoBehaviour
    {
        private ShipRegistry registry;
        private Spawner spawner;
        private float enemyRespawnDelay;
        private bool isInitialized;

        public bool IsInitialized => isInitialized;

        public void Initialize(ShipSpawnerSettings settings, ShipRegistry shipRegistry, Func<Transform> worldCenterProvider)
        {
            if (isInitialized)
                return;

            registry = shipRegistry;
            spawner = new Spawner(settings, worldCenterProvider);
            enemyRespawnDelay = settings.enemyRespawnDelay;

            registry.ActiveShips.OnAdd += HandleShipAdded;
            registry.ActiveShips.OnRemove += HandleShipRemoved;

            foreach (var ship in registry.ActiveShips)
                HandleShipAdded(ship);

            isInitialized = true;
        }

        private void HandleShipAdded(Ship ship)
        {
            if (ship?.Damage == null)
                return;
            ship.Damage.OnDeath += OnShipDeath;
        }

        private void HandleShipRemoved(Ship ship)
        {
            if (ship?.Damage == null)
                return;
            ship.Damage.OnDeath -= OnShipDeath;
        }

        private void OnShipDeath(ShipId deadShipId, ShipId _killerId)
        {
            if (registry == null || spawner == null)
                return;
            if (!registry.TryGetShip(deadShipId, out var deadShip))
                return;
            StartCoroutine(spawner.WaitAndRespawnShip(enemyRespawnDelay, deadShip));
        }

        private void OnDestroy()
        {
            if (registry == null)
                return;

            registry.ActiveShips.OnAdd -= HandleShipAdded;
            registry.ActiveShips.OnRemove -= HandleShipRemoved;

            foreach (var ship in registry.ActiveShips)
                HandleShipRemoved(ship);
        }
    }
}
