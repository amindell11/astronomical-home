using System;
using AI;
using Capture;
using Capture.Recording;
using Game;
using Game.Player;
using Game.Preview.Fixtures;
using Ships;
using Substrate.Sessions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode.Common
{

/// <summary>
/// Centralized asset loading for PlayMode tests.
/// Reduces duplication of AssetDatabase.LoadAssetAtPath calls across test suites.
/// </summary>
public static class TestAssets
{
    // Standard asset paths
    private const string CrimsonPrefabPath = "Assets/Prefabs/Ships/Crimson.prefab";
    private const string TestPilotMpcPath = "Assets/Prefabs/Pilots/TestPilotMPC.prefab";
    private const string PlayerPilotPath = "Assets/Prefabs/Pilots/PlayerPilot.prefab";
    private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
    private const string TrialRunPath = "Assets/Settings/Game/SurvivalTrialRun.asset";
    private const string GameHostPath = "Assets/Prefabs/Game/GameHost.prefab";
    private const string PreviewFixturesPath = "Assets/Settings/Game/Preview/PreviewFixtures.asset";

    public static Ship LoadCrimsonPrefab() => Load<Ship>(CrimsonPrefabPath);

    public static AICommander LoadTestPilotMpc() => Load<AICommander>(TestPilotMpcPath);

    public static PlayerCommander LoadPlayerPilot() => Load<PlayerCommander>(PlayerPilotPath);

    // Unlinked copy of the resolved row, so a shorter test run still composes Replay.
    public static PreviewFixture HudFixture(string name)
    {
        var row = FixtureRow(name);
        return new PreviewFixture { layout = row.prefab, link = PreviewFixture.Unlinked, values = row };
    }

    public static SectorEntry FixtureRow(string layout) =>
        Fixture(layout).Resolve(Load<GameHost>(GameHostPath).sessionProfile.run);

    public static PreviewFixture Fixture(string layout)
    {
        var fixtures = Load<PreviewFixtures>(PreviewFixturesPath);
        if (!fixtures)
            throw new InvalidOperationException($"'{PreviewFixturesPath}' is not a {nameof(PreviewFixtures)} asset.");
        return fixtures.Fixture(layout);
    }

    /// <summary>The shipped run's row at <paramref name="index"/>, the way a test flies the trial layout.</summary>
    public static SectorEntry TrialRow(int index) => Load<SectorRun>(TrialRunPath).Sectors[index];

    public static T Load<T>(string assetPath) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<T>(assetPath);
#else
        return null;
#endif
    }

    /// <summary>A live observer camera built from the authored prefab the game host spawns.</summary>
    public static global::Cameras.ObserverCam NewObserverCam()
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<global::Cameras.ObserverCam>(ObserverCamPrefabPath);
        return prefab ? UnityEngine.Object.Instantiate(prefab) : null;
#else
        return null;
#endif
    }

    public static string CaptureCommit()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "-captureCommit") return args[i + 1];
        throw new InvalidOperationException("Graphics capture tests require the launcher's -captureCommit.");
    }

    /// <summary>The Editor-owned capture module, resolved by name because the test assemblies do not reference its assembly.</summary>
    public static IEpisodeCapture NewNativeCapture() => (IEpisodeCapture)ScriptableObject.CreateInstance(
        Type.GetType("Capture.GameView.GameViewEpisodeCapture, Capture.GameView.Editor",
            throwOnError: true));
}

} // namespace Tests.PlayMode.Common
