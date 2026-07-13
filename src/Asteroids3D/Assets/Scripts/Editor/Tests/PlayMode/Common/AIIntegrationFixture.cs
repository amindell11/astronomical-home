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

/// <summary>
/// Base fixture for AI integration tests that need multiple ships
/// interacting through the full scan → utility → nav/gunner loop.
/// Provides a real ShipRegistry so CombatTracker can acquire enemies.
/// </summary>
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

    /// <summary>
    /// Creates an AI ship at the given position and team, registers it
    /// in the shared ShipRegistry, and wires up the AICommander.
    /// </summary>
    protected (Ship ship, AICommander cmdr) CreateAIShip(Vector3 position, int team, int decisionSeed = 0)
    {
        var ship = ShipTestFactory.CreateDefaultShipAt(position, Quaternion.identity, useMpcPilot: true, team: team, decisionSeed: decisionSeed);
        Assert.IsNotNull(ship, "Failed to create AI ship — check test asset paths");

        trackedShips.Add(ship);

        // Register in the real registry so collider → ship lookups work
        registry.ActiveShips.Add(ship);

        var cmdr = ship.GetComponentInChildren<AICommander>();
        Assert.IsNotNull(cmdr, "Ship missing AICommander component");

        // Wire the arena (registry + NavField) into AI systems (triggers TryInitializeSystems)
        cmdr.SetArena(arena);

        return (ship, cmdr);
    }

    /// <summary>
    /// Resets the UtilityChooser and re-initializes with custom states.
    /// Allows tests to control which states are available for utility evaluation.
    /// </summary>
    protected void InitializeWithStates(AICommander cmdr, params AIState[] states)
    {
        cmdr.UtilityChooser.Reset();
        cmdr.UtilityChooser.Initialize(states, new SeedScope(0));
    }

    /// <summary>
    /// Deals raw damage to a ship. Shield absorbs first — to reduce health,
    /// deal more than shield capacity + desired health damage.
    /// </summary>
    protected void DealDamage(Ship target, float amount)
    {
        target.Damage.TakeDamage(amount, 0f, Vector3.zero, Vector3.zero, null);
    }
}

} // namespace Tests.PlayMode.Common
#endif
