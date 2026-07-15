#if UNITY_EDITOR
using System.Collections;
using AI;
using Game;
using Game.Capture;
using Game.Services;
using NUnit.Framework;
using Ships;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>Base class for capture scenarios driven by CaptureScenarioPlayModeTests: set up a situation, then Step the recorder each fixed step with the subjects to frame and an optional diagnostic draw. Author scratch scenarios in repo-root scratch/capture/; promote by moving the file to Tests/PlayMode/Scenarios/.</summary>
public abstract class CaptureScenario
{
    private const string UtilityPilotPath = "Assets/Prefabs/Pilots/UtilityPilot.prefab";

    public ArenaContext Arena { get; internal set; }
    internal ShipRegistry Registry { get; set; }

    public virtual CaptureConfig Config => new() { clipName = GetType().Name };

    public abstract IEnumerator Run(CaptureRecorder recorder);

    /// <summary>Spawns a Ship2 running the production UtilityPilot combat brain, registered and wired into the scenario arena. The runner destroys all ships at teardown.</summary>
    protected (Ship ship, AICommander cmdr) SpawnUtilityShip(Vector2 planePos, float rotDeg, int team, int decisionSeed = 0)
    {
        var pilot = TestAssets.LoadCommanderPrefab(UtilityPilotPath);
        var ship = ShipTestFactory.CreateShip(TestAssets.LoadShip2Prefab(), pilot, team,
            Arena.Place(planePos),
            GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward),
            decisionSeed);
        Assert.IsNotNull(ship, "Failed to create scenario ship — check test asset paths");

        Registry.ActiveShips.Add(ship);
        ship.Targeting?.SetRegistry(Arena.Registry);
        var cmdr = ship.GetComponentInChildren<AICommander>();
        Assert.IsNotNull(cmdr, "Scenario ship is missing an AICommander");
        cmdr.SetArena(Arena);

        return (ship, cmdr);
    }
}

} // namespace Tests.PlayMode.Common
#endif
