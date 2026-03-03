using System;
using System.Collections.Generic;
using AI;
using Ships;
using Ships.Command;
using UnityEngine;
using ShipFactory = Ships.Factory;

namespace Game.Services
{
    public class UnitService : IUnitService
    {
        private readonly ShipRegistry shipRegistry;
        private readonly List<Ship> spawnedShips = new();

        public IShipRegistry Registry => shipRegistry;
        public ShipRegistry ActiveRegistry => shipRegistry;
        public event Action<Ship> OnShipSpawned;

        public UnitService()
        {
            shipRegistry = new ShipRegistry(null);
        }

        public Ship SpawnShip(
            Ship template,
            Commander commander,
            ShipSettings settings,
            int team,
            Vector3 position,
            Quaternion rotation)
        {
            if (!template)
                throw new ArgumentNullException(nameof(template));

            var ship = ShipFactory.CreateShip(
                template, commander, settings, team,
                position, rotation,
                postInitialize: WireShipDependencies);

            shipRegistry.ActiveShips.Add(ship);
            spawnedShips.Add(ship);
            OnShipSpawned?.Invoke(ship);
            return ship;
        }

        public void Clear()
        {
            foreach (var ship in spawnedShips)
            {
                if (ship != null)
                {
                    shipRegistry.ActiveShips.Remove(ship);
                    UnityEngine.Object.Destroy(ship.gameObject);
                }
            }

            spawnedShips.Clear();
            shipRegistry.Dispose();
        }

        private void WireShipDependencies(Ship ship)
        {
            if (!ship) return;
            ship.Targeting?.SetRegistry(shipRegistry);
            if (ship.Commander is AICommander aiCommander)
                aiCommander.SetRegistry(shipRegistry);
        }
    }
}
