using System.Collections;
using AI;
using AI.Context;
using Movement.MPC;
using Game;
using NUnit.Framework;
using Ships;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using AICommander = AI.AICommander;

namespace Tests.PlayMode
{

[Category("Integration")]
[Category("Slow")]
public class MpcNavigatorPlayModeTests
{
    private Ship ship;
    private AICommander cmdr;
    private MpcNavigator mpc;

    private const float YawTimeoutSec  = 8f;
    private const float NavTimeoutSec  = 20f;

    [SetUp]
    public void SetUp()
    {
#if UNITY_EDITOR
        AudioListener.pause = true;
        TestSceneBuilder.CreateTestArena();
        
        var settings   = AssetDatabase.LoadAssetAtPath<ShipSettings>("Assets/Settings/Ships/DefaultSettings.asset");
        var shipPrefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_2.prefab");
        var cmdrPrefab = AssetDatabase.LoadAssetAtPath<AICommander>("Assets/Prefabs/Ships/Pilots/TestPilotMPC.prefab");
        
        ship = Factory.CreateShip(shipPrefab, cmdrPrefab, settings, team: 0, Vector3.zero, Quaternion.identity);
        cmdr = ship.Commander as AICommander;
        mpc  = cmdr.Navigator as MpcNavigator;
#else
        Assert.Ignore("MpcNavigatorPlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
    }

    [TearDown]
    public void TearDown()
    {
        AudioListener.pause = false;
        if (ship != null)
            Object.Destroy(ship.gameObject);
        TestSceneBuilder.CleanupTestArena();
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator MpcYawOnly_ShipRotatesToFacingOverride()
    {
        mpc.SetNavigationPoint(Vector2.zero);
        mpc.SetFacingOverride(90f);
        
        var deadline = Time.realtimeSinceStartup + YawTimeoutSec;
        while (Time.realtimeSinceStartup < deadline)
        {
            var facingAngle = Vector2.SignedAngle(Vector2.up, ship.transform.up);
            if (Mathf.Abs(Mathf.DeltaAngle(facingAngle, 90f)) < 5f) break;
            yield return new WaitForFixedUpdate();
        }

        var finalFacing = Vector2.SignedAngle(Vector2.up, ship.transform.up);
        var finalDiff   = Mathf.Abs(Mathf.DeltaAngle(finalFacing, 90f));
        
        Assert.That(finalDiff, Is.LessThan(10f),
            $"Ship should rotate to face 90° within {YawTimeoutSec}s (final diff = {finalDiff:F1}°)");
        Assert.That(ship.transform.position.magnitude, Is.LessThan(1f),
            "Ship should remain stationary while only yawing");
    }

    [UnityTest]
    public IEnumerator MpcFixedWaypoint_ShipArrivesWithinTimeout()
    {
        var targetPos = new Vector2(15, 15);
        mpc.SetNavigationPoint(targetPos);
        
        var deadline = Time.realtimeSinceStartup + NavTimeoutSec;
        while (DistanceToPlaneTarget(targetPos) > mpc.arriveRadius && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(
            DistanceToPlaneTarget(targetPos),
            Is.LessThan(mpc.arriveRadius),
            $"MPC should reach waypoint {targetPos} within {NavTimeoutSec}s");
    }

    [UnityTest]
    public IEnumerator MpcMovingWaypoint_ShipFollowsTarget()
    {
        var targetPos = new Vector2(10, 0);
        mpc.SetNavigationPoint(targetPos);
        
        var deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            // Move waypoint in a circle
            float t = Time.time;
            targetPos = new Vector2(Mathf.Cos(t) * 10f, Mathf.Sin(t) * 10f);
            mpc.SetNavigationPoint(targetPos);
            yield return new WaitForFixedUpdate();
        }
        
        float dist = DistanceToPlaneTarget(targetPos);
        Assert.That(dist, Is.LessThan(14f), "Ship should follow a moving waypoint");
    }

    [UnityTest]
    public IEnumerator MpcObstacleAvoidance_ShipReachesTargetWithoutColliding()
    {
        mpc.enableObstacleAvoidance = true;
        
        var obstacle   = TestSceneBuilder.CreateObstacle(new Vector3(10, 10, 0), new Vector3(2, 2, 2));
        var targetPos  = new Vector2(20, 20);
        var obstaclePos2D = new Vector2(10, 10);
        mpc.SetNavigationPoint(targetPos);
        
        var deadline           = Time.realtimeSinceStartup + NavTimeoutSec;
        float minDistToObstacle = float.MaxValue;

        while (Time.realtimeSinceStartup < deadline)
        {
            var shipPos2D = GamePlane.WorldPointToPlane(ship.transform.position);
            minDistToObstacle = Mathf.Min(minDistToObstacle, Vector2.Distance(shipPos2D, obstaclePos2D));
            
            if (DistanceToPlaneTarget(targetPos) < mpc.arriveRadius)
                break;
            
            yield return new WaitForFixedUpdate();
        }
        
        var finalDistToTarget = DistanceToPlaneTarget(targetPos);
        Assert.That(finalDistToTarget, Is.LessThan(mpc.arriveRadius + 1f),
            "MPC should reach waypoint while avoiding obstacle");
        // Obstacle radius is 1 (half of size 2); ship should not enter it.
        Assert.That(minDistToObstacle, Is.GreaterThan(1.5f),
            "MPC should maintain clearance from obstacle center");
        
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
