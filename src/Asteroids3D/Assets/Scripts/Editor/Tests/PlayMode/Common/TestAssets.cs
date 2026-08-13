using System;
using AI;
using Game.Capture;
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
    private const string Ship2PrefabPath = "Assets/Prefabs/Ships/Ship_2.prefab";
    private const string TestPilotMpcPath = "Assets/Prefabs/Pilots/TestPilotMPC.prefab";

    /// <summary>
    /// Loads the Ship_2 prefab (commonly used in tests).
    /// </summary>
    public static Ship LoadShip2Prefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Ship>(Ship2PrefabPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Loads the MPC test pilot AI commander prefab.
    /// </summary>
    public static AICommander LoadTestPilotMpc()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<AICommander>(TestPilotMpcPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Loads a ship prefab from a custom path.
    /// </summary>
    public static Ship LoadShipPrefab(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Ship>(assetPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Loads an AI commander prefab from a custom path.
    /// </summary>
    public static AICommander LoadCommanderPrefab(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<AICommander>(assetPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Loads an engine module asset from a custom path.
    /// </summary>
    public static EngineModule LoadEngineModule(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<EngineModule>(assetPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Loads a shield module asset from a custom path.
    /// </summary>
    public static ShieldModule LoadShieldModule(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<ShieldModule>(assetPath);
#else
        return null;
#endif
    }

    /// <summary>The Editor-owned capture module, resolved by name because the test assemblies do not reference its assembly.</summary>
    public static IEpisodeCapture NewNativeCapture() => (IEpisodeCapture)ScriptableObject.CreateInstance(
        Type.GetType("Game.Capture.GameView.GameViewEpisodeCapture, Game.Capture.GameView.Editor",
            throwOnError: true));
}

} // namespace Tests.PlayMode.Common
