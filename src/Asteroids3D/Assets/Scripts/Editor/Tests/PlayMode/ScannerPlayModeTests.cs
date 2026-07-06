using System.Collections;
using AI;
using AI.Scanning;
using NUnit.Framework;
using Ships;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{

[Category("AI")]
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
        base.TearDown();
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator ObstacleScanner_DetectsNearbyObstacle_WithinTimeout()
    {
        // The obstacle scanner now queries the deterministic asteroid field, not physics colliders.
        // Inject a stub field that reports one obstacle at the ship's location so the merge path fills.
        var stub = new StubObstacleField(new DetectedObstacle(ship.transform.position, 5f, null));
        cmdr.SetObstacleFieldProvider(new StubObstacleFieldProvider(stub));

        yield return AsyncAssert.WaitUntil(
            () => cmdr.Scout.ObstacleScan.count > 0,
            ScanTimeoutSec,
            $"Scanner should detect the stub field obstacle within {ScanTimeoutSec}s",
            useFixedUpdate: true);
    }

    // Reports a single fixed obstacle whenever it falls inside the query box.
    private sealed class StubObstacleField : IObstacleField
    {
        private readonly DetectedObstacle obstacle;
        public StubObstacleField(DetectedObstacle obstacle) => this.obstacle = obstacle;

        public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return 0;
            if (Mathf.Abs(obstacle.position.x - centerPlane.x) > halfExtent
                || Mathf.Abs(obstacle.position.y - centerPlane.y) > halfExtent) return 0;
            buffer[0] = obstacle;
            return 1;
        }
    }

    private sealed class StubObstacleFieldProvider : IObstacleFieldProvider
    {
        public StubObstacleFieldProvider(IObstacleField field) => ObstacleField = field;
        public IObstacleField ObstacleField { get; }
    }
}

} // namespace Tests.PlayMode
