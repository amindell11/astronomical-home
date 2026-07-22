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
using UnityEngine.TestTools.Utils;
using AICommander = AI.AICommander;

namespace Tests.PlayMode
{

// Closed-loop integration tests: drive the navigator with the public "go here" command and assert on the ship's emergent motion. Solver-decision behavior is covered more cheaply at the unit level in Tests.EditMode/MpcSolverTests.
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
        // Clear any goal/weight state the utility chooser's first state applied during init.
        mpc.ResetNavigation();
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
    public IEnumerator SetNavigationPoint_WaypointBecomesValid()
    {
        // Exercises base-Navigator waypoint plumbing (not MPC-specific behaviour).
        cmdr.Navigator.SetNavigationPoint(new Vector2(10, 10));
        Assert.That(cmdr.Navigator.CurrentWaypoint.isValid, Is.True);
        Assert.That(cmdr.Navigator.CurrentWaypoint.position,
            Is.EqualTo(new Vector2(10, 10)).Using(new Vector2EqualityComparer(0.01f)));
        yield return null;
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator SetVelocityReference_ShipVelocityTrendsToCommand()
    {
        // The velocity-tracker seam: a commanded planar velocity with no waypoint drives a real hull, and ShouldIdle keeps the MPC running without one.
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
        mpc.SetNavigationPoint(Vector2.zero);
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

    [UnityTest]
    public IEnumerator MpcFixedWaypoint_ShipArrivesWithinTimeout()
    {
        var targetPos = new Vector2(15, 15);
        mpc.SetNavigationPoint(targetPos);

        yield return AsyncAssert.WaitForVector2NearTarget(
            () => GamePlane.WorldPointToPlane(ship.transform.position),
            targetPos,
            mpc.arriveRadius,
            NavTimeoutSec,
            $"MPC should reach waypoint {targetPos} within {NavTimeoutSec}s",
            useFixedUpdate: true);
    }

    [UnityTest]
    public IEnumerator MpcMovingWaypoint_ShipFollowsTarget()
    {
        var targetPos = new Vector2(10, 0);
        mpc.SetNavigationPoint(targetPos);

        var elapsed = 0f;
        while (elapsed < 10f)
        {
            float t = Time.time;
            targetPos = new Vector2(Mathf.Cos(t) * 10f, Mathf.Sin(t) * 10f);
            mpc.SetNavigationPoint(targetPos);
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        float dist = TestUtilities.DistanceToPlaneTarget(ship.transform, targetPos);
        Assert.That(dist, Is.LessThan(16f), "Ship should follow a moving waypoint");
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
    public IEnumerator MpcObstacleAvoidance_ShipReachesTargetWithoutColliding()
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
        var targetPos  = new Vector2(20, 20);
        var obstaclePos2D = new Vector2(10, 10);
        mpc.SetNavigationPoint(targetPos);

        var elapsed             = 0f;
        float minDistToObstacle = float.MaxValue;

        while (elapsed < NavTimeoutSec)
        {
            var shipPos2D = GamePlane.WorldPointToPlane(ship.transform.position);
            minDistToObstacle = Mathf.Min(minDistToObstacle, Vector2.Distance(shipPos2D, obstaclePos2D));

            if (TestUtilities.DistanceToPlaneTarget(ship.transform, targetPos) < mpc.arriveRadius)
                break;

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        var finalDistToTarget = TestUtilities.DistanceToPlaneTarget(ship.transform, targetPos);
        Assert.That(finalDistToTarget, Is.LessThan(mpc.arriveRadius + 1f),
            "MPC should reach waypoint while avoiding obstacle");
        // Obstacle radius is 1 (half of size 2); ship should not enter it.
        Assert.That(minDistToObstacle, Is.GreaterThan(1.5f),
            "MPC should maintain clearance from obstacle center");

        Arena.ObstacleField = null;
        Object.Destroy(obstacle);
        yield return null;
    }

    [UnityTest]
    public IEnumerator GoalVelocity_ShipLeadsMovingWaypoint()
    {
        // Waypoint at (15,0) moving north at 5 u/s: the ship should head northeast (positive Y), not due east.
        var waypointPos = new Vector2(15f, 0f);
        var waypointVel = new Vector2(0f, 5f);

        mpc.SetNavigationPoint(waypointPos, false, waypointVel);

        float accumulatedYComponent = 0f;
        int samples = 0;
        var elapsed = 0f;

        for (var i = 0; i < 20; i++)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        while (elapsed < 4f)
        {
            var vel = GamePlane.WorldDirToPlane(ship.GetComponent<Rigidbody>().linearVelocity);
            if (vel.sqrMagnitude > 0.5f)
            {
                accumulatedYComponent += vel.normalized.y;
                samples++;
            }
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        Assert.That(samples, Is.GreaterThan(10), "Ship should have measurable velocity during test");

        var avgYComponent = accumulatedYComponent / samples;
        Assert.That(avgYComponent, Is.GreaterThan(0.05f),
            $"Ship velocity should have positive Y component (leading the moving goal). Avg Y: {avgYComponent:F3}");
    }
}

}
