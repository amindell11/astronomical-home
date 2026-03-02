using System.Collections;
using AI;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{

[Category("Integration")]
[Category("Slow")]
public class ScannerPlayModeTests : PlayModeWorldFixture
{
    private Ship ship;
    private AICommander cmdr;

    private const float ScanTimeoutSec = 3f;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        
#if UNITY_EDITOR
        var settings   = AssetDatabase.LoadAssetAtPath<ShipSettings>("Assets/Settings/Ships/DefaultSettings.asset");
        var shipPrefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_2.prefab");
        var cmdrPrefab = AssetDatabase.LoadAssetAtPath<AICommander>("Assets/Prefabs/Ships/Pilots/TestPilot.prefab");
        ship = Ships.Factory.CreateShip(shipPrefab, cmdrPrefab, settings, team: 0, Vector3.zero, Quaternion.identity);
        cmdr = ship.Commander as AICommander;
#else
        Assert.Ignore("ScannerPlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
    }

    [TearDown]
    public override void TearDown()
    {
        if (ship != null)
            Object.Destroy(ship.gameObject);
        base.TearDown();
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator ObstacleScanner_DetectsNearbyObstacle_WithinTimeout()
    {
        var obstacle = TestSceneBuilder.CreateObstacle(new Vector3(0, 5, 0), new Vector3(5, 5, 1));

        // Poll until the scanner registers the obstacle (exit early) instead of blind wait.
        var deadline = Time.realtimeSinceStartup + ScanTimeoutSec;
        while (cmdr.Scout.ObstacleScan.count == 0 && Time.realtimeSinceStartup < deadline)
            yield return new WaitForFixedUpdate();

        Assert.IsTrue(cmdr.Scout.ObstacleScan.count > 0,
            $"Scanner should detect nearby obstacle within {ScanTimeoutSec}s");

        Object.Destroy(obstacle);
    }
}

} // namespace Tests.PlayMode
