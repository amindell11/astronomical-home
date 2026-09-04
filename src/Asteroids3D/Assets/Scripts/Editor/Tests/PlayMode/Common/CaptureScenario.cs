#if UNITY_EDITOR
using System.Collections;
using AI;
using Game;
using Game.Session;
using Game.Capture;
using Game.Services;
using NUnit.Framework;
using Ships;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>Base class for capture scenarios driven by CaptureScenarioPlayModeTests: set up a situation inside the runner-composed headless session, Film the ships to frame, then FilmStep each fixed step. Diagnostics are native gizmos selected by <see cref="Profile"/>. Author scratch scenarios in repo-root scratch/capture/; promote by moving the file to Tests/PlayMode/Scenarios/.</summary>
public abstract class CaptureScenario
{
    private const string CombatPilotPath = "Assets/Prefabs/Pilots/AgentPilot.prefab";

    /// <summary>The headless session the runner composed via SessionHost — real services, world, UnitService.</summary>
    public GameSession Session { get; internal set; }

    public WorldHandle World => Session.World;

    public virtual CaptureConfig Config => new() { clipName = GetType().Name };

    /// <summary>Which native gizmo types the footage carries. None films the game alone, with presentation visuals.</summary>
    public virtual GizmoCaptureProfile Profile => GizmoCaptureProfile.Everything;

    /// <summary>Set by the runner, which also ends the episode when Run returns or throws.</summary>
    internal IEpisodeCapture Capture { get; set; }

    public abstract IEnumerator Run();

    /// <summary>Begins filming the given ships; they are the framed and gizmo-selected subjects.</summary>
    protected void Film(params Ship[] subjects) =>
        Capture.Begin(Config, Profile, subjects, Session.Services.Projectiles);

    /// <summary>Advances the capture one fixed step. Call once per WaitForFixedUpdate while filming.</summary>
    protected void FilmStep() => Capture.Step();

    /// <summary>Spawns a Ship2 running the production policy-pilot combat brain through the session's UnitService — full game wiring, arena-root parenting, spawn-order-derived decision seed; torn down with the session.</summary>
    protected (Ship ship, AICommander cmdr) SpawnCombatShip(Vector2 planePos, float rotDeg, int team)
    {
        var pilot = TestAssets.LoadCommanderPrefab(CombatPilotPath);
        Assert.IsNotNull(pilot, "Failed to load the combat pilot prefab — check test asset paths");

        var ship = Session.Services.UnitService.SpawnShip(
            TestAssets.LoadShip2Prefab(), pilot, team,
            World.Place(planePos),
            GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward),
            Session.World);
        Assert.IsNotNull(ship, "Failed to create scenario ship — check test asset paths");

        var cmdr = ship.GetComponentInChildren<AICommander>();
        Assert.IsNotNull(cmdr, "Scenario ship is missing an AICommander");
        return (ship, cmdr);
    }
}

} // namespace Tests.PlayMode.Common
#endif
