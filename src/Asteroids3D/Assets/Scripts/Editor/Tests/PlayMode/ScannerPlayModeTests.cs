using System.Collections;
using AI;
using Asteroids.Fields;
using Game;
using NUnit.Framework;
using Ships;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{

[Category("AI")]
public class ScannerPlayModeTests : PlayModeWorldFixture
{
    private Ship ship;
    private AICommander cmdr;
    private UpdatingAsteroidField field;

    private const float ScanTimeoutSec = 3f;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();

#if UNITY_EDITOR
        ship = ShipTestFactory.CreateDefaultShip();
        cmdr = ship.Commander as AICommander;

        // Scout.Initialize() (and therefore obstacleScanner) is gated on a registry being
        // present. Supply a stub so AI systems fully initialise without a real game world.
        cmdr.SetRegistry(new StubShipRegistry());
#else
        Assert.Ignore("ScannerPlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
    }

    [TearDown]
    public override void TearDown()
    {
        ShipTestFactory.DestroyShip(ship);
        if (field)
        {
            field.DespawnAll();
            Object.DestroyImmediate(field.gameObject);
        }
        base.TearDown();
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator ObstacleScanner_DetectsNearbyObstacle_WithinTimeout()
    {
        var obstacle = TestSceneBuilder.CreateObstacle(new Vector3(0, 5, 0), new Vector3(5, 5, 1));

        yield return AsyncAssert.WaitUntil(
            () => cmdr.Scout.ObstacleScan.count > 0,
            ScanTimeoutSec,
            $"Scanner should detect nearby obstacle within {ScanTimeoutSec}s",
            useFixedUpdate: true);

        Object.Destroy(obstacle);
    }

    [UnityTest]
    public IEnumerator ObstacleScanner_DetectsDeterministicFieldAsteroids()
    {
        field = AssetDatabase.LoadAssetAtPath<UpdatingAsteroidField>("Assets/Prefabs/Asteroid/AsteroidController.prefab");
        Assert.That(field, Is.Not.Null, "Missing deterministic asteroid field prefab.");

        field = Object.Instantiate(field, Vector3.zero, GamePlane.Rotation);
        field.SetPlayer(ship.transform);
        field.SetPlayerStart(new Vector2(1000f, 1000f));

        yield return AsyncAssert.WaitUntil(
            () => cmdr.Scout.ObstacleScan.count > 0,
            ScanTimeoutSec,
            $"Scanner should detect live deterministic field asteroids within {ScanTimeoutSec}s",
            useFixedUpdate: true);
    }
}

} // namespace Tests.PlayMode
