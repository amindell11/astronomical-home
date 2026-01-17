using Ships;
using Ships.Control;
using UnityEngine;

/// <summary>
/// Configuration asset for play mode tests. References ship prefabs, commanders, and settings.
/// Create an instance via Assets > Create > Tests > TestConfig.
/// </summary>
[CreateAssetMenu(fileName = "TestConfig", menuName = "Tests/TestConfig")]
public class TestConfig : ScriptableObject
{
    [Header("Ship Prefabs")]
    [SerializeField] private Ship playerShip;
    [SerializeField] private Ship enemyShip;

    [Header("Commanders")]
    [SerializeField] private Commander playerCommander;
    [SerializeField] private Commander enemyCommander;

    [Header("Settings")]
    [SerializeField] private Settings shipSettings;

    public Ship PlayerShip => playerShip;
    public Ship EnemyShip => enemyShip;
    public Commander PlayerCommander => playerCommander;
    public Commander EnemyCommander => enemyCommander;
    public Settings ShipSettings => shipSettings;

    /// <summary>
    /// Creates a TestServices instance from this configuration.
    /// </summary>
    public TestServices CreateServices(float separation = 20f)
    {
        return TestServices.Create(
            playerShip,
            playerCommander,
            enemyShip,
            enemyCommander,
            shipSettings,
            separation);
    }

    /// <summary>
    /// Creates a TestServices instance with just the player ship.
    /// </summary>
    public TestServices CreatePlayerOnly()
    {
        return TestServices.CreateSingle(playerShip, playerCommander, shipSettings);
    }

    /// <summary>
    /// Creates a TestServices instance with just the enemy ship.
    /// </summary>
    public TestServices CreateEnemyOnly()
    {
        return TestServices.CreateSingle(enemyShip, enemyCommander, shipSettings);
    }

    private static TestConfig _cached;

    /// <summary>
    /// Loads the default test configuration.
    /// </summary>
    public static TestConfig Load()
    {
        if (_cached) return _cached;

#if UNITY_EDITOR
        _cached = UnityEditor.AssetDatabase.LoadAssetAtPath<TestConfig>(
            "Assets/Settings/Tests/TestConfig.asset");
#endif
        return _cached;
    }

    /// <summary>
    /// Clears the cached config (useful between test runs).
    /// </summary>
    public static void ClearCache() => _cached = null;
}
