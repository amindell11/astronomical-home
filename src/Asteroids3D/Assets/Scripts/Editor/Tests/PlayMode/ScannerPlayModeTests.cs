using System.Collections;
using AI;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

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
        ship = ShipTestFactory.CreateDefaultShip(useMpcPilot: false);
        cmdr = ship.Commander as AICommander;
#else
        Assert.Ignore("ScannerPlayModeTests requires the Unity Editor (uses AssetDatabase).");
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
}

} // namespace Tests.PlayMode
