using System.Collections;
using Game;
using NUnit.Framework;
using Ships;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using AICommander = AI.AICommander;

public class NavigatorPlayMode
{
    private Ship ship;
    private AICommander cmdr;
    [SetUp]
    public void SetUp()
    {    
        AudioListener.pause = true;        
        var settings = AssetDatabase.LoadAssetAtPath<ShipSettings>("Assets/Settings/Ships/DefaultSettings.asset");
        var shipPrefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_2.prefab");
        var cmdrPrefab = AssetDatabase.LoadAssetAtPath<AICommander>("Assets/Prefabs/Ships/Pilots/TestPilot.prefab");
        ship = Factory.CreateShip(
            shipPrefab,
            cmdrPrefab,
            settings,
            team: 0,
            Vector3.zero,
            Quaternion.identity);
        cmdr = ship.Commander as AICommander;
    }

    [UnityTest]
    public IEnumerator TestSetNavigationPoint()
    {
        cmdr.Navigator.SetNavigationPoint(new Vector2(10, 10));
        Assert.That(cmdr.Navigator.CurrentWaypoint.isValid, Is.True);
        Assert.That(cmdr.Navigator.CurrentWaypoint.position, Is.EqualTo(new Vector2(10, 10)).Using(new Vector2EqualityComparer(0.01f)));
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestNavigateToWaypoint()
    {
        cmdr.Navigator.SetNavigationPoint(new Vector2(10, 10));
        var startTime = Time.time;
        Debug.Log("Waiting for ship to navigate to waypoint");
        Debug.Log(ship.settings.maxSpeed);
        while(Vector2.Distance(ship.transform.position, GamePlane.PlanePointToWorld(new Vector2(10, 10))) > 0.1f)
        {
            Debug.Log("Ship position: " + ship.transform.position.ToString());
            yield return new WaitForSeconds(0.1f);
            if(Time.time - startTime > 20)
            {
                Assert.Fail("Timed out waiting for ship to navigate to waypoint");
            }
        }
        Debug.Log("Ship reached waypoint");
        Debug.Log("Ship position: " + ship.transform.position.ToString());
    }   
    [UnityTest]
    public IEnumerator TestAvoidObstacles()
    {
        var obstacle = TestSceneBuilder.CreateObstacle(new Vector3(5, 5, 0), new Vector3(1, 1, 1));
        cmdr.Navigator.SetNavigationPoint(new Vector2(10, 10), true);
        yield return new WaitForSeconds(3);
        Assert.That(Vector2.Distance(ship.transform.position, GamePlane.PlanePointToWorld(new Vector2(10, 10))), Is.LessThan(0.1f));
        Object.Destroy(obstacle);
        yield return null;
    }
    [TearDown]
    public void TearDown()
    {   
        AudioListener.pause = false;
        Object.Destroy(ship.gameObject);
    }
}