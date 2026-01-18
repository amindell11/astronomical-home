using System.Collections;
using AI;
using AI.Context;
using Game;
using NUnit.Framework;
using Ships;
using Ships.Control;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

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
        // Set weights to only care about yaw, but penalize movement to stay stationary.
        // If we leave wVel at 0, the optimizer might drift since movement is "free".
        mpc.wPos = 0;
        mpc.wVel = 1.0f; 
        mpc.wYaw = 1.0f;
        mpc.wEffort = 0.1f;
        
        var targetPos = new Vector2(10, 0);
        mpc.SetNavigationPoint(targetPos);
        
        // Wait for ship to rotate
        float startTime = Time.time;
        while (startTime + 5f > Time.time)
        {
            var toTarget = targetPos - (Vector2)ship.transform.position;
            var angle = Vector2.Angle(ship.transform.up, toTarget);
            
            if (angle < 5f) break;
            yield return new WaitForFixedUpdate();
        }

        var finalToTarget = targetPos - (Vector2)ship.transform.position;
        var finalAngle = Vector2.Angle(ship.transform.up, finalToTarget);
        
        Assert.That(finalAngle, Is.LessThan(5f), "Ship should rotate to face target");
        Assert.That(ship.transform.position.magnitude, Is.LessThan(1f), "Ship should stay mostly stationary with wPos=0");
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

    [TearDown]
    public void TearDown()
    {
        AudioListener.pause = false;
    }
}
