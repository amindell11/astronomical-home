#if UNITY_EDITOR
using System.Collections;
using AI;
using Game;
using Game.Bootstrap;
using Game.Capture;
using Game.Services;
using NUnit.Framework;
using Ships;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>Base class for capture scenarios driven by CaptureScenarioPlayModeTests: set up a situation inside the runner-composed headless session, then Step the recorder each fixed step with the subjects to frame and an optional diagnostic draw. Author scratch scenarios in repo-root scratch/capture/; promote by moving the file to Tests/PlayMode/Scenarios/.</summary>
public abstract class CaptureScenario
{
    private const string CombatPilotPath = "Assets/Prefabs/Pilots/AgentPilot.prefab";

    /// <summary>The headless session the runner composed via SessionHost — real services, arena, UnitService.</summary>
    public GameSession Session { get; internal set; }

    public ArenaContext Arena => Session.Services.Arena;

    public virtual CaptureConfig Config => new() { clipName = GetType().Name };

    public abstract IEnumerator Run(CaptureRecorder recorder);

    /// <summary>Spawns a Ship2 running the production policy-pilot combat brain through the session's UnitService — full game wiring, arena-root parenting, spawn-order-derived decision seed; torn down with the session.</summary>
    protected (Ship ship, AICommander cmdr) SpawnUtilityShip(Vector2 planePos, float rotDeg, int team)
    {
        var pilot = TestAssets.LoadCommanderPrefab(CombatPilotPath);
        Assert.IsNotNull(pilot, "Failed to load the combat pilot prefab — check test asset paths");

        var ship = Session.Services.UnitService.SpawnShip(
            TestAssets.LoadShip2Prefab(), pilot, team,
            Arena.Place(planePos),
            GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward));
        Assert.IsNotNull(ship, "Failed to create scenario ship — check test asset paths");

        var cmdr = ship.GetComponentInChildren<AICommander>();
        Assert.IsNotNull(cmdr, "Scenario ship is missing an AICommander");
        return (ship, cmdr);
    }
}

} // namespace Tests.PlayMode.Common
#endif
