using System.Collections;
using Game;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif
using AICommander = AI.AICommander;

namespace Tests.PlayMode
{

[Category("Integration")]
[Category("Slow")]
public class NavigatorPlayModeTests : PlayModeWorldFixture
{
    private Ship ship;
    private AICommander cmdr;

    private const float NavTimeoutSec    = 25f;  // generous; navigation physics can be slow
    private const float ArriveThreshold  = 0.1f;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        
#if UNITY_EDITOR
        var settings   = AssetDatabase.LoadAssetAtPath<ShipSettings>("Assets/Settings/Ships/DefaultSettings.asset");
        var shipPrefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_2.prefab");
        var cmdrPrefab = AssetDatabase.LoadAssetAtPath<AICommander>("Assets/Prefabs/Ships/Pilots/TestPilot.prefab");
        ship = Factory.CreateShip(shipPrefab, cmdrPrefab, settings, team: 0, Vector3.zero, Quaternion.identity);
        cmdr = ship.Commander as AICommander;
#else
        Assert.Ignore("NavigatorPlayModeTests requires the Unity Editor (uses AssetDatabase).");
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
    public IEnumerator SetNavigationPoint_WaypointBecomesValid()
    {
        cmdr.Navigator.SetNavigationPoint(new Vector2(10, 10));
        Assert.That(cmdr.Navigator.CurrentWaypoint.isValid, Is.True);
        Assert.That(cmdr.Navigator.CurrentWaypoint.position,
            Is.EqualTo(new Vector2(10, 10)).Using(new Vector2EqualityComparer(0.01f)));
        yield return null;
    }
    
    [UnityTest]
    [Ignore("Legacy StandardNavigator coverage; package slated for removal")]
    public IEnumerator NavigateToWaypoint_ShipArrivesWithinTimeout()
    {
        var target = new Vector2(10, 10);
        cmdr.Navigator.SetNavigationPoint(target);

        var deadline = Time.realtimeSinceStartup + NavTimeoutSec;
        while (DistanceToPlaneTarget(target) > ArriveThreshold && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(DistanceToPlaneTarget(target),
            Is.LessThan(ArriveThreshold),
            $"Ship did not reach waypoint within {NavTimeoutSec}s. Final pos: {ship.transform.position}");
    }

    [UnityTest]
    [Ignore("Legacy StandardNavigator coverage; package slated for removal")]
    public IEnumerator AvoidObstacles_ShipReachesWaypointAroundBarrier()
    {
        var obstacle = TestSceneBuilder.CreateObstacle(new Vector3(5, 5, 0), new Vector3(1, 1, 1));
        var target   = new Vector2(10, 10);
        cmdr.Navigator.SetNavigationPoint(target, true);

        // Poll for arrival instead of a blind WaitForSeconds.
        var deadline = Time.realtimeSinceStartup + NavTimeoutSec;
        while (DistanceToPlaneTarget(target) > ArriveThreshold && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(DistanceToPlaneTarget(target),
            Is.LessThan(ArriveThreshold),
            $"Ship should navigate around obstacle and reach ({target}) within {NavTimeoutSec}s");

        Object.Destroy(obstacle);
        yield return null;
    }

    private float DistanceToPlaneTarget(Vector2 target)
    {
        var shipPos2D = GamePlane.WorldPointToPlane(ship.transform.position);
        return Vector2.Distance(shipPos2D, target);
    }
}

} // namespace Tests.PlayMode
