using System.Collections;
using Game;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
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
        ship = ShipTestFactory.CreateDefaultShip(useMpcPilot: false);
        cmdr = ship.Commander as AICommander;
#else
        Assert.Ignore("NavigatorPlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
    }

    [TearDown]
    public override void TearDown()
    {
        ShipTestFactory.DestroyShip(ship);
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

        yield return AsyncAssert.WaitForVector2NearTarget(
            () => GamePlane.WorldPointToPlane(ship.transform.position),
            target,
            ArriveThreshold,
            NavTimeoutSec,
            $"Ship did not reach waypoint within {NavTimeoutSec}s. Final pos: {ship.transform.position}",
            useFixedUpdate: true);
    }

    [UnityTest]
    [Ignore("Legacy StandardNavigator coverage; package slated for removal")]
    public IEnumerator AvoidObstacles_ShipReachesWaypointAroundBarrier()
    {
        var obstacle = TestSceneBuilder.CreateObstacle(new Vector3(5, 5, 0), new Vector3(1, 1, 1));
        var target   = new Vector2(10, 10);
        cmdr.Navigator.SetNavigationPoint(target, true);

        yield return AsyncAssert.WaitForVector2NearTarget(
            () => GamePlane.WorldPointToPlane(ship.transform.position),
            target,
            ArriveThreshold,
            NavTimeoutSec,
            $"Ship should navigate around obstacle and reach ({target}) within {NavTimeoutSec}s",
            useFixedUpdate: true);

        Object.Destroy(obstacle);
        yield return null;
    }
}

} // namespace Tests.PlayMode
