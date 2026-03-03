using Ships;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>
/// Minimal IShipRegistry stub for PlayMode tests that need AI systems initialized
/// (Scout, Navigator, etc.) but don't require real ship lookup or team logic.
/// All queries return not-found / neutral results.
/// </summary>
public sealed class StubShipRegistry : IShipRegistry
{
    public bool TryGetShipId(Collider collider, out ShipId id)
    {
        id = ShipId.Invalid;
        return false;
    }

    public bool TryGetShip(ShipId id, out Ship ship)
    {
        ship = null;
        return false;
    }

    public bool TryGetShip(Collider collider, out Ship ship, ShipId? excludeId = null)
    {
        ship = null;
        return false;
    }

    public bool IsFriendly(ShipId a, ShipId b) => false;
    public bool IsHostile(ShipId a, ShipId b) => false;
    public int GetTeam(ShipId id) => -1;
}

} // namespace Tests.PlayMode.Common
