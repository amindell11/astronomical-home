#if UNITY_EDITOR
using System;
using System.Collections;
using AI;
using Capture;
using Capture.Recording;
using Capture.Views;
using Tests.PlayMode.Common;
using Substrate.Sessions;
using NUnit.Framework;
using Ships;
using UnityEngine;
using Substrate;

namespace Tests.PlayMode.Recording
{

/// <summary>Scenarios either use the runner's diagnostic session or compose their own preview. Film prepares a diagnostic view; preview scenarios record their existing view directly. The runner finishes and disposes an active recording when Run returns.</summary>
public abstract class CaptureScenario : IDisposable
{
    private const string CombatPilotPath = "Assets/Prefabs/Pilots/AgentPilot.prefab";

    /// <summary>The headless session the runner composed — real services and UnitService, with <see cref="SectorEntry"/>'s sector loaded when one is named.</summary>
    public Session Session { get; internal set; }

    public virtual bool NeedsSession => true;
    protected virtual CaptureRequest Request => new("results/capture", GetType().Name, Commit);
    protected virtual DiagnosticViewConfig View => new();
    private IDisposable view;
    private IDisposable pacing;
    private CaptureRecording recording;
    internal CaptureResult Result { get; set; }

    /// <summary>The sector the runner loads, with no hero, before Run. Null films in an empty session.</summary>
    public virtual SectorEntry SectorEntry => null;

    /// <summary>Which native gizmo types the footage carries. None films the game alone, with presentation visuals.</summary>
    public virtual GizmoCaptureProfile Profile => GizmoCaptureProfile.Everything;

    internal IEpisodeCapture Capture { get; set; }

    internal string Commit { get; set; }

    public abstract IEnumerator Run();

    protected IEnumerator Film(params Ship[] subjects)
    {
        pacing = CapturePacing.Locked();
        view = ((IDiagnosticViewFactory)Capture).PrepareView(View, Profile, subjects, Session.Projectiles);
        recording = Capture.Prepare(Request);
        yield return recording.Ready;
        recording.Start();
    }

    internal void Finish()
    {
        if (recording != null) Result = recording.Finish();
    }

    public void Dispose()
    {
        try { recording?.Dispose(); }
        finally
        {
            try { view?.Dispose(); }
            finally { pacing?.Dispose(); }
        }
    }

    /// <summary>Spawns a Crimson running the production policy-pilot combat brain through the session's UnitService — full game wiring, arena-root parenting, spawn-order-derived decision seed; torn down with the session. The ship senses the loaded sector's rocks, none when sector-less.</summary>
    protected (Ship ship, AICommander cmdr) SpawnCombatShip(Vector2 planePos, float rotDeg, int team)
    {
        var pilot = TestAssets.Load<AICommander>(CombatPilotPath);
        Assert.IsNotNull(pilot, "Failed to load the combat pilot prefab — check test asset paths");

        var ship = Session.Units.SpawnShip(
            TestAssets.LoadCrimsonPrefab(), pilot, team,
            Session.Frame.Place(planePos),
            GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward),
            field: Session.ActiveSector ? Session.ActiveSector.ObstacleField : null);
        Assert.IsNotNull(ship, "Failed to create scenario ship — check test asset paths");

        var cmdr = ship.GetComponentInChildren<AICommander>();
        Assert.IsNotNull(cmdr, "Scenario ship is missing an AICommander");
        return (ship, cmdr);
    }
}

}
#endif
