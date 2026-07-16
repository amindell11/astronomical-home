#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using AI.States;
using Game;
using Game.Services;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>Base fixture for multi-ship AI integration tests (full scan → utility → nav/gunner loop); provides a real ShipRegistry so CombatTracker can acquire enemies.</summary>
public abstract class AIIntegrationFixture : PlayModeWorldFixture
{
    protected ShipRegistry registry;
    private ArenaContext arena;
    private readonly List<Ship> trackedShips = new();

    public override void SetUp()
    {
        base.SetUp();
        registry = new ShipRegistry();
        arena = new ArenaContext(Vector2.zero, registry, NavField);
    }

    public override void TearDown()
    {
        foreach (var ship in trackedShips)
        {
            if (ship != null)
                Object.DestroyImmediate(ship.gameObject);
        }
        trackedShips.Clear();

        registry?.Dispose();
        registry = null;

        base.TearDown();
    }

    protected (Ship ship, AICommander cmdr) CreateAIShip(Vector3 position, int team, int decisionSeed = 0)
    {
        var ship = ShipTestFactory.CreateDefaultShipAt(position, Quaternion.identity, Projectiles, team: team, decisionSeed: decisionSeed);
        Assert.IsNotNull(ship, "Failed to create AI ship — check test asset paths");

        trackedShips.Add(ship);

        registry.ActiveShips.Add(ship);

        var cmdr = ship.GetComponentInChildren<AICommander>();
        Assert.IsNotNull(cmdr, "Ship missing AICommander component");

        // SetArena triggers TryInitializeSystems.
        cmdr.SetArena(arena);

        return (ship, cmdr);
    }

    protected void InitializeWithStates(AICommander cmdr, params AIState[] states)
    {
        cmdr.UtilityChooser.Reset();
        cmdr.UtilityChooser.Initialize(states, new SeedScope(0));
    }

    /// <summary>Deals raw damage; shield absorbs first, so exceed shield capacity to touch health.</summary>
    protected void DealDamage(Ship target, float amount)
    {
        target.Damage.TakeDamage(amount, 0f, Vector3.zero, Vector3.zero, null);
    }
}

}
#endif
