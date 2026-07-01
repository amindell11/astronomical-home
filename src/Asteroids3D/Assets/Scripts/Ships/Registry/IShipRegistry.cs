using UnityEngine;

namespace Ships
{
    public interface IShipRegistry
    {
        bool TryGetShipId(Collider collider, out ShipId id);
        bool TryGetShip(ShipId id, out Ship ship);
        bool TryGetShip(Collider collider, out Ship ship, ShipId? excludeId = null);
        bool IsFriendly(ShipId a, ShipId b);
        bool IsHostile(ShipId a, ShipId b);
        int GetTeam(ShipId id);
    }
}
