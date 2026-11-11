using System;
using System.Linq;
using AI;
using Capture;
using Capture.Recording;
using Game.Player;
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
    private const string PreviewFixturesPath = "Assets/Settings/Game/Preview/PreviewFixtures.asset";

    public static Ship LoadCrimsonPrefab() => Load<Ship>(CrimsonPrefabPath);

    public static AICommander LoadTestPilotMpc() => Load<AICommander>(TestPilotMpcPath);

    public static PlayerCommander LoadPlayerPilot() => Load<PlayerCommander>(PlayerPilotPath);

    // Replay appends both HUD rows to its run, so every test preview host needs them.
    public static SectorEntry HudFixture(string name) => FixtureRow(name);

    /// <summary>The preview fixtures run's row flying the fixture layout named <paramref name="layout"/>.</summary>
    public static SectorEntry FixtureRow(string layout) =>
        Load<SectorRun>(PreviewFixturesPath).Sectors.SingleOrDefault(row => row.prefab && row.prefab.name == layout)
        ?? throw new InvalidOperationException($"'{PreviewFixturesPath}' has no row flying the {layout} layout.");

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
