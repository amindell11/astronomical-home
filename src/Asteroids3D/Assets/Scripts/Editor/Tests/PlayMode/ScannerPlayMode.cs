using System.Collections;
using AI;
using NUnit.Framework;
using Ships;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class ScannerPlayMode
{
    private Ship ship;
    private AICommander cmdr;

    [SetUp]
    public void SetUp()
    {
        AudioListener.pause = true;        
        var settings = AssetDatabase.LoadAssetAtPath<Settings>("Assets/Settings/Ships/DefaultSettings.asset");
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

    [TearDown]
    public void TearDown()
    {
        AudioListener.pause = false;
        Object.Destroy(ship.gameObject);
    }

    [UnityTest]
    public IEnumerator TestObstacleScanner()
    {
        var obstacle = TestSceneBuilder.CreateObstacle(new Vector3(0, 5, 0), new Vector3(5, 5, 1));
        yield return new WaitForSeconds(1);
        Assert.IsTrue(cmdr.Scout.Obstacles.Count > 0);
    }
}
