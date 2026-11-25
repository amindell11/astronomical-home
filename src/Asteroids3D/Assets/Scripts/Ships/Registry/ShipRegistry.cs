using System.Collections.Generic;
using Ships;
using UnityEngine;
using Utils;

namespace Ships.Registry
{
    public class ShipRegistry : IShipRegistry
    {
        public SubscribedSet<Ship> ActiveShips { get; } = new();

        private readonly Dictionary<Collider, ShipId> colliderToId = new();
        private readonly Dictionary<ShipId, Ship> idToShip = new();
        private readonly Dictionary<ShipId, int> idToTeam = new();

        public ShipRegistry()
        {
            ActiveShips.OnAdd += RegisterShip;
            ActiveShips.OnRemove += UnregisterShip;
        }

        private void RegisterShip(Ship ship)
        {
            if (!ship) return;
            var id = new ShipId(ship.GetInstanceID());
            var colliders = ship.Colliders;

            foreach (var col in colliders)
                colliderToId[col] = id;
            
            idToShip[id] = ship;
            idToTeam[id] = ship.teamNumber;
        }

        private void UnregisterShip(Ship ship)
        {
            if (!ship) return;
            var id = new ShipId(ship.GetInstanceID());
            var colliders = ship.Colliders;

            foreach (var col in colliders)
                colliderToId.Remove(col);
            
            idToShip.Remove(id);   
            idToTeam.Remove(id);
        }

        public bool TryGetShipId(Collider collider, out ShipId id)
        {
            if (collider && colliderToId.TryGetValue(collider, out id))
                return true;
            id = ShipId.Invalid;
            return false;
        }

        public bool TryGetShip(ShipId id, out Ship ship)
        {
            return idToShip.TryGetValue(id, out ship) && ship;
        }

        public bool TryGetShip(Collider collider, out Ship ship, ShipId? excludeId = null)
        {
            if (TryGetShipId(collider, out var id))
            {
                if (excludeId.HasValue && id == excludeId.Value)
                {
                    ship = null;
                    return false;
                }
                return TryGetShip(id, out ship);
            }
            ship = null;
            return false;
        }

        public bool IsFriendly(ShipId a, ShipId b)
        {
            return GetTeam(a) == GetTeam(b);
        }

        public bool IsHostile(ShipId a, ShipId b)
        {
            return !IsFriendly(a, b);
        }

        public int GetTeam(ShipId id)
        {
            return idToTeam.GetValueOrDefault(id, -1);
        }

        public void Dispose()
        {
            ActiveShips.OnAdd -= RegisterShip;
            ActiveShips.OnRemove -= UnregisterShip;
            ActiveShips.Clear();

            colliderToId.Clear();
            idToShip.Clear();
            idToTeam.Clear();
        }
    }
}
