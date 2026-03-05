using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI;
using Ships;
using Ships.Command;
using UnityEngine;
using Random = UnityEngine.Random;
using ShipFactory = Ships.Factory;

namespace Game.Services
{
    public class UnitService : MonoBehaviour, IUnitService
    {
        private readonly List<Ship> spawnedShips = new();
        public IShipRegistry Registry => ActiveRegistry;
        public ShipRegistry ActiveRegistry { get; } = new();

        public event Action<Ship> OnShipSpawned;

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

            ActiveRegistry.ActiveShips.Add(ship);
            spawnedShips.Add(ship);
            OnShipSpawned?.Invoke(ship);
            return ship;
        }

        public void Clear()
        {
            foreach (var ship in spawnedShips.Where(ship => ship))
            {
                ActiveRegistry.ActiveShips.Remove(ship);
                UnityEngine.Object.Destroy(ship.gameObject);
            }

            spawnedShips.Clear();
            // Do NOT call ActiveRegistry.Dispose() here — that unsubscribes the OnAdd/OnRemove
            // callbacks, which permanently breaks the registry for subsequent runs.
            // Ships are already fully unregistered via ActiveShips.Remove() above.
        }

        private void OnDestroy()
        {
            ActiveRegistry.Dispose();
        }

        private void WireShipDependencies(Ship ship)
        {
            if (!ship) return;
            ship.Targeting?.SetRegistry(ActiveRegistry);
            if (ship.Commander is AICommander aiCommander)
                aiCommander.SetRegistry(ActiveRegistry);
        }

        public void RespawnShip(ShipId id, Vector2 pos, float rotation)
        {
            if(!ActiveRegistry.TryGetShip(id, out var ship)) return;
            ship.transform.position = GamePlane.PlanePointToWorld(pos);
            ship.transform.rotation = Quaternion.AngleAxis(rotation, GamePlane.Normal);
            ship.ResetShip();
        }

        public void WaitAndRespawnShip(ShipId ship, Vector2 pos, float rotation, float delay)
        {
            StartCoroutine(WaitAndRespawn());
            return;

            IEnumerator WaitAndRespawn()
            {
                yield return new WaitForSeconds(delay);
                RespawnShip(ship, pos, rotation);
            }
        }
    }
}
