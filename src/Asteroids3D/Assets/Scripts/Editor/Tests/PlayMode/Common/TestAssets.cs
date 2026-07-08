using AI;
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
}

} // namespace Tests.PlayMode.Common
