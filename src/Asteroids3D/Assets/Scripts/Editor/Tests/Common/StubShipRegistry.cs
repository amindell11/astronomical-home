using System.Collections.Generic;
using Ships;
using UnityEngine;

namespace Tests.Common
{

/// <summary>
/// Minimal IShipRegistry stub for EditMode and PlayMode tests that need AI systems
/// initialized (Scout, Navigator, etc.) or a registry injected, but don't require
/// real ship lookup or team logic. Collider and team queries always return
/// not-found / neutral. Identity lookup consults <see cref="Ships"/>, which starts
/// empty — so a test that registers nothing behaves exactly as before, and one that
/// needs an anchor to resolve registers it. <see cref="ShipLookups"/> counts identity
/// resolutions, which is how per-tick re-resolution is observed.
/// Lives in the assembly-neutral Tests.Common assembly so both test assemblies share it.
/// </summary>
public sealed class StubShipRegistry : IShipRegistry
{
    public readonly Dictionary<ShipId, Ship> Ships = new();

    public int ShipLookups { get; private set; }

    public bool TryGetShipId(Collider collider, out ShipId id)
    {
        id = ShipId.Invalid;
        return false;
    }

    public bool TryGetShip(ShipId id, out Ship ship)
    {
        ShipLookups++;
        return Ships.TryGetValue(id, out ship) && ship;
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

} // namespace Tests.Common
