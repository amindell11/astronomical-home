using System.Collections;
using AI;
using Movement.MPC;
using Game;
using NUnit.Framework;
using Ships;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using AICommander = AI.AICommander;

namespace Tests.PlayMode
{

// Closed-loop integration tests: drive the navigator with the public velocity-reference command and assert on the ship's emergent motion. Solver-decision behavior is covered more cheaply at the unit level in Tests.EditMode/MpcSolverTests.
[Category("MPC")]
public class MpcNavigatorPlayModeTests : PlayModeWorldFixture
{
    private Ship ship;
    private AICommander cmdr;
    private Navigator mpc;

    private const float YawTimeoutSec  = 8f;
    private const float NavTimeoutSec  = 20f;

    protected override bool AccelerateTime => true;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();

#if UNITY_EDITOR
        ship = ShipTestFactory.CreateDefaultShip(Projectiles);
        cmdr = ship.Commander as AICommander;
        mpc  = cmdr.Navigator as Navigator;

        // Navigator.Initialize() is gated on arena != null — supply the fixture arena so all AI systems are fully initialized before tests run.
        cmdr.SetArena(Arena);
        cmdr.Brain.enabled = false;
#else
        Assert.Ignore("MpcNavigatorPlayModeTests requires the Unity Editor (uses AssetDatabase).");
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
    public IEnumerator SetVelocityReference_ShipVelocityTrendsToCommand()
    {
        // The velocity-tracker seam: a commanded planar velocity drives a real hull toward the reference.
        var command = new Vector2(0f, 8f); // +Y, along the ship's initial nose
        mpc.SetVelocityReference(command);

        var rb = ship.GetComponent<Rigidbody>();
        var elapsed = 0f;
        var vel = Vector2.zero;
        while (elapsed < NavTimeoutSec)
        {
            vel = GamePlane.WorldDirToPlane(rb.linearVelocity);
            if (Vector2.Dot(vel, command.normalized) > 0.6f * command.magnitude) break;
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        Assert.That(Vector2.Dot(vel, command.normalized), Is.GreaterThan(0.5f * command.magnitude),
            $"Real ship velocity should trend toward the commanded reference (got {vel}).");
        Assert.That(vel.y, Is.GreaterThan(Mathf.Abs(vel.x)),
            "Velocity should track the commanded axis, not drift sideways.");
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator MpcYawOnly_ShipRotatesToFacingOverride()
    {
        // A zero reference is a valid "stop", keeping the MPC running while only the facing override acts.
        mpc.SetVelocityReference(Vector2.zero);
        mpc.SetFacingOverride(90f);

        yield return AsyncAssert.WaitUntilThen(
            () => TestUtilities.AngleDeltaToTarget(ship.transform, 90f) < 5f,
            YawTimeoutSec,
            () =>
            {
                var finalDiff = TestUtilities.AngleDeltaToTarget(ship.transform, 90f);
                Assert.That(finalDiff, Is.LessThan(10f),
                    $"Ship should rotate to face 90° within {YawTimeoutSec}s (final diff = {finalDiff:F1}°)");
                Assert.That(ship.transform.position.magnitude, Is.LessThan(1f),
                    "Ship should remain stationary while only yawing");
            },
            useFixedUpdate: true);
    }

    /// <summary>Feeds a hand-placed obstacle through the obstacle-field seam (there is no physics scan for primitive colliders).</summary>
    private sealed class StubObstacleField : AI.Scanning.IObstacleField
    {
        public Vector3 position;
        public float radius;
        public Collider collider;
        public int QueryObstacles(Vector2 centerPlane, float halfExtent, AI.Scanning.DetectedObstacle[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return 0;
            buffer[0] = new AI.Scanning.DetectedObstacle(position, radius, collider);
            return 1;
        }
    }

    [UnityTest]
    public IEnumerator MpcObstacleAvoidance_ShipTracksCommandWithoutColliding()
    {
        mpc.enableObstacleAvoidance = true;

        var obstacle   = TestSceneBuilder.CreateObstacle(new Vector3(10, 10, 0), new Vector3(2, 2, 2));
        var stubField  = new StubObstacleField
        {
            position = new Vector3(10, 10, 0),
            radius = 1f,
            collider = obstacle.GetComponent<Collider>(),
        };
        Arena.ObstacleField = stubField;

        // Commanded velocity leads straight through the obstacle; the solver must divert around it while keeping progress.
        var direction = new Vector2(1f, 1f).normalized;
        var obstaclePos2D = new Vector2(10, 10);
        mpc.SetVelocityReference(direction * 8f);

        var elapsed             = 0f;
        var progress            = 0f;
        float minDistToObstacle = float.MaxValue;

        while (elapsed < NavTimeoutSec)
        {
            var shipPos2D = GamePlane.WorldPointToPlane(ship.transform.position);
            minDistToObstacle = Mathf.Min(minDistToObstacle, Vector2.Distance(shipPos2D, obstaclePos2D));

            progress = Vector2.Dot(shipPos2D, direction);
            if (progress > 25f) break;

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        Assert.That(progress, Is.GreaterThan(18f),
            "Ship should keep tracking the commanded direction past the obstacle");
        // Obstacle radius is 1 (half of size 2); ship should not enter it.
        Assert.That(minDistToObstacle, Is.GreaterThan(1.5f),
            "MPC should maintain clearance from obstacle center");

        Arena.ObstacleField = null;
        Object.Destroy(obstacle);
        yield return null;
    }
}

}
