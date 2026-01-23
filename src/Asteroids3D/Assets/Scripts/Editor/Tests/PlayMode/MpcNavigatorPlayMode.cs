using System.Collections;
using AI;
using AI.Context;
using AI.Steering.MPC;
using Game;
using NUnit.Framework;
using Ships;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using AICommander = AI.AICommander;
using Settings = Ships.Settings;

public class MpcNavigatorPlayMode
{
    private Ship ship;
    private AICommander cmdr;
    private MpcNavigator mpc;

    [SetUp]
    public void SetUp()
    {
        AudioListener.pause = true;
        var settings = AssetDatabase.LoadAssetAtPath<Settings>("Assets/Settings/Ships/DefaultSettings.asset");
        var shipPrefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_2.prefab");
        var cmdrPrefab = AssetDatabase.LoadAssetAtPath<AICommander>("Assets/Prefabs/Ships/Pilots/TestPilotMPC.prefab");
        
        ship = Factory.CreateShip(
            shipPrefab,
            cmdrPrefab,
            settings,
            team: 0,
            Vector3.zero,
            Quaternion.identity);
        cmdr = ship.Commander as AICommander;
        mpc = cmdr.Navigator as MpcNavigator;
    }

    [UnityTest]
    public IEnumerator TestMpcYawOnly()
    {        
        // Set waypoint at origin (no movement needed) and facing override to 90 degrees
        mpc.SetNavigationPoint(Vector2.zero);
        mpc.SetFacingOverride(90f);
        
        // Wait for ship to rotate
        float startTime = Time.time;
        while (startTime + 5f > Time.time)
        {
            var facingAngle = Vector2.SignedAngle(Vector2.up, ship.transform.up);
            var angleDiff = Mathf.Abs(Mathf.DeltaAngle(facingAngle, 90f));
            
            if (angleDiff < 5f) break;
            yield return new WaitForFixedUpdate();
        }

        var finalFacing = Vector2.SignedAngle(Vector2.up, ship.transform.up);
        var finalDiff = Mathf.Abs(Mathf.DeltaAngle(finalFacing, 90f));
        
        Assert.That(finalDiff, Is.LessThan(10f), "Ship should rotate to face 90 degrees");
        Assert.That(ship.transform.position.magnitude, Is.LessThan(1f), "Ship should stay stationary at origin");
    }

    [UnityTest]
    public IEnumerator TestMpcFixedWaypoint()
    {
        var targetPos = new Vector2(15, 15);
        mpc.SetNavigationPoint(targetPos);
        
        float startTime = Time.time;
        while (Vector2.Distance(ship.transform.position, GamePlane.PlanePointToWorld(targetPos)) > mpc.arriveRadius)
        {
            yield return new WaitForFixedUpdate();
            if (Time.time - startTime > 15f)
            {
                Assert.Fail("Timed out waiting for MPC to reach waypoint");
            }
        }
        
        Assert.Pass("MPC reached waypoint");
    }

    [UnityTest]
    public IEnumerator TestMpcMovingWaypoint()
    {
        var targetPos = new Vector2(10, 0);
        mpc.SetNavigationPoint(targetPos);
        
        float startTime = Time.time;
        while (Time.time - startTime < 10f)
        {
            // Move waypoint in a circle
            targetPos = new Vector2(Mathf.Cos(Time.time) * 10f, Mathf.Sin(Time.time) * 10f);
            mpc.SetNavigationPoint(targetPos);
            
            yield return new WaitForFixedUpdate();
        }
        
        // Verify ship is somewhat close to the moving target
        float dist = Vector2.Distance(ship.transform.position, GamePlane.PlanePointToWorld(targetPos));
        Assert.That(dist, Is.LessThan(13f), "Ship should follow moving waypoint");
    }

    [UnityTest]
    public IEnumerator TestMpcObstacleAvoidance()
    {
        // Enable obstacle avoidance
        mpc.enableObstacleAvoidance = true;
        
        // Place obstacle between ship (at origin) and target
        var obstacle = TestSceneBuilder.CreateObstacle(new Vector3(10, 10, 0), new Vector3(2, 2, 2));
        var targetPos = new Vector2(20, 20);
        mpc.SetNavigationPoint(targetPos);
        
        float startTime = Time.time;
        float minDistToObstacle = float.MaxValue;
        var obstaclePos2D = new Vector2(10, 10);
        
        // Track minimum distance to obstacle while navigating
        while (Time.time - startTime < 10f)
        {
            var shipPos2D = new Vector2(ship.transform.position.x, ship.transform.position.y);
            var distToObstacle = Vector2.Distance(shipPos2D, obstaclePos2D);
            minDistToObstacle = Mathf.Min(minDistToObstacle, distToObstacle);
            
            var distToTarget = Vector2.Distance(ship.transform.position, GamePlane.PlanePointToWorld(targetPos));
            if (distToTarget < mpc.arriveRadius)
            {
                break;
            }
            
            yield return new WaitForFixedUpdate();
        }
        
        var finalDistToTarget = Vector2.Distance(ship.transform.position, GamePlane.PlanePointToWorld(targetPos));
        
        // Ship should reach target
        Assert.That(finalDistToTarget, Is.LessThan(mpc.arriveRadius + 1f), "MPC should reach waypoint while avoiding obstacle");
        
        // Ship should have maintained some distance from obstacle (not collide with it)
        // Obstacle radius is 1 (half of size 2), so ship should stay at least 1 unit away
        Assert.That(minDistToObstacle, Is.GreaterThan(1.5f), "MPC should avoid obstacle");
        
        Object.Destroy(obstacle);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        AudioListener.pause = false;
        if (ship != null)
            Object.Destroy(ship.gameObject);
    }
}
