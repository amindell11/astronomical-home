using System;
using AI;
using Capture;
using Ships;
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
    private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";

    public static Ship LoadCrimsonPrefab() => Load<Ship>(CrimsonPrefabPath);

    public static AICommander LoadTestPilotMpc() => Load<AICommander>(TestPilotMpcPath);

    public static T Load<T>(string assetPath) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<T>(assetPath);
#else
        return null;
#endif
    }

    /// <summary>A live observer camera built from the authored prefab the game host spawns.</summary>
    public static Cameras.ObserverCam NewObserverCam()
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<Cameras.ObserverCam>(ObserverCamPrefabPath);
        return prefab ? UnityEngine.Object.Instantiate(prefab) : null;
#else
        return null;
#endif
    }

    /// <summary>The Editor-owned capture module, resolved by name because the test assemblies do not reference its assembly.</summary>
    public static IEpisodeCapture NewNativeCapture() => (IEpisodeCapture)ScriptableObject.CreateInstance(
        Type.GetType("Capture.GameView.GameViewEpisodeCapture, Capture.GameView.Editor",
            throwOnError: true));
}

} // namespace Tests.PlayMode.Common
