using System.Collections.Generic;
using Ships;
using UnityEngine;
using Ships.Registry;

namespace Tests.Common
{

/// <summary>
/// Minimal IShipRegistry stub for EditMode and PlayMode tests that need AI systems
/// initialized (Scout, Navigator, etc.) or a registry injected, but don't require
/// real ship lookup or team logic. Every lookup consults a map that starts empty:
/// <see cref="Ships"/> for identity, <see cref="ColliderIds"/> for colliders and
/// <see cref="Teams"/> for <see cref="IsFriendly"/>, so a test that registers nothing
/// gets not-found / neutral answers. <see cref="IsHostile"/> is always false.
/// <see cref="ShipLookups"/> counts identity resolutions, which is how per-tick
/// re-resolution is observed.
/// Lives in the assembly-neutral Tests.Common assembly so both test assemblies share it.
/// </summary>
public sealed class StubShipRegistry : IShipRegistry
{
    public readonly Dictionary<ShipId, Ship> Ships = new();
    public readonly Dictionary<Collider, ShipId> ColliderIds = new();
    public readonly Dictionary<ShipId, int> Teams = new();

    public int ShipLookups { get; private set; }

    public bool TryGetShipId(Collider collider, out ShipId id)
    {
        if (collider && ColliderIds.TryGetValue(collider, out id)) return true;
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

    public bool IsFriendly(ShipId a, ShipId b) =>
        Teams.TryGetValue(a, out var teamA) && Teams.TryGetValue(b, out var teamB) && teamA == teamB;
    public bool IsHostile(ShipId a, ShipId b) => false;
    public int GetTeam(ShipId id) => -1;
}

} // namespace Tests.Common
