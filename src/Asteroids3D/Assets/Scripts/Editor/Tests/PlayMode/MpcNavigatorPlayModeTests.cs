using System.Collections;
using AI;
using AI.Context;
using Movement.MPC;
using Game;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using AICommander = AI.AICommander;

namespace Tests.PlayMode
{

[Category("Integration")]
[Category("Slow")]
public class MpcNavigatorPlayModeTests : PlayModeWorldFixture
{
    private Ship ship;
    private AICommander cmdr;
    private MpcNavigator mpc;

    private const float YawTimeoutSec  = 8f;
    private const float NavTimeoutSec  = 20f;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();

#if UNITY_EDITOR
        ship = ShipTestFactory.CreateDefaultShip(useMpcPilot: true);
        cmdr = ship.Commander as AICommander;
        mpc  = cmdr.Navigator as MpcNavigator;

        // Navigator.Initialize() is gated on registry != null — supply a stub so all
        // AI systems (Scout, Navigator, Gunner) are fully initialized before tests run.
        cmdr.SetRegistry(new StubShipRegistry());
        cmdr.UtilitySelector.enabled = false;
        mpc.ClearGoalMode();
        mpc.ClearWeightMultipliers();
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
        
        var deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            // Move waypoint in a circle
            float t = Time.time;
            targetPos = new Vector2(Mathf.Cos(t) * 10f, Mathf.Sin(t) * 10f);
            mpc.SetNavigationPoint(targetPos);
            yield return new WaitForFixedUpdate();
        }
        
        float dist = TestUtilities.DistanceToPlaneTarget(ship.transform, targetPos);
        Assert.That(dist, Is.LessThan(16f), "Ship should follow a moving waypoint");
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
            
            if (TestUtilities.DistanceToPlaneTarget(ship.transform, targetPos) < mpc.arriveRadius)
                break;
            
            yield return new WaitForFixedUpdate();
        }
        
        var finalDistToTarget = TestUtilities.DistanceToPlaneTarget(ship.transform, targetPos);
        Assert.That(finalDistToTarget, Is.LessThan(mpc.arriveRadius + 1f),
            "MPC should reach waypoint while avoiding obstacle");
        // Obstacle radius is 1 (half of size 2); ship should not enter it.
        Assert.That(minDistToObstacle, Is.GreaterThan(1.5f),
            "MPC should maintain clearance from obstacle center");
        
        Object.Destroy(obstacle);
        yield return null;
    }
}

/// <summary>
/// Tests for enemy state projection and dynamic lead-target facing in the MPC solver.
/// Verifies that SetEnemyState/ClearEnemyState properly control dynamic facing,
/// and that goal velocity is projected through the planning horizon.
/// </summary>
[Category("Integration")]
[Category("Slow")]
public class MpcEnemyProjectionPlayModeTests : PlayModeWorldFixture
{
    private Ship ship;
    private AICommander cmdr;
    private MpcNavigator mpc;

    private const float FacingTimeoutSec = 8f;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();

#if UNITY_EDITOR
        ship = ShipTestFactory.CreateDefaultShip(useMpcPilot: true);
        cmdr = ship.Commander as AICommander;
        mpc  = cmdr.Navigator as MpcNavigator;

        cmdr.SetRegistry(new StubShipRegistry());
        cmdr.UtilitySelector.enabled = false;
        mpc.ClearGoalMode();
        mpc.ClearWeightMultipliers();
#else
        Assert.Ignore("MpcEnemyProjectionPlayModeTests requires the Unity Editor.");
#endif
    }

    [TearDown]
    public override void TearDown()
    {
        ShipTestFactory.DestroyShip(ship);
        base.TearDown();
    }

    /// <summary>
    /// When SetEnemyState provides projectile speed for a moving target,
    /// the MPC should compute a lead-target facing angle rather than aiming
    /// directly at the enemy position. The ship should face ahead of the target
    /// in the direction of its velocity.
    /// </summary>
    [UnityTest]
    [Ignore("Base-weight facing/exposure was intentionally collapsed in the MPC retune (authority moved to per-state multipliers). Redesign for amplified weights after the reward refactor.")]
    public IEnumerator EnemyState_WithProjectileSpeed_ShipFacesLeadPoint()
    {
        // Enemy is directly to the right (20, 0) moving upward at 8 units/sec.
        // Direct angle to enemy = ~90° (east).
        // Lead angle should be < 90° (northeast) because intercept is above the enemy.
        var enemyPos = new Vector2(20f, 0f);
        var enemyVel = new Vector2(0f, 8f);
        var projectileSpeed = 20f;

        // Enemy yaw doesn't affect facing — use 0
        mpc.SetNavigationPoint(enemyPos, true, enemyVel);
        mpc.SetGoalMaintainRange(20f, 5f);
        mpc.SetEnemyState(0f, 0f, projectileSpeed);

        // Wait for the ship to settle on a facing direction
        yield return AsyncAssert.WaitUntilThen(
            () => TestUtilities.AngleDeltaToTarget(ship.transform, 90f) > 5f,
            FacingTimeoutSec,
            () =>
            {
                var facing = TestUtilities.GetPlaneFacingAngle(ship.transform);
                // Lead point is at roughly (20, 8) from origin → angle ≈ 68°
                // The ship should face between 0° and 90° (northeast quadrant), not at 90°
                Assert.That(facing, Is.InRange(30f, 85f),
                    $"Ship should face toward lead intercept point (NE quadrant), not directly east. Actual: {facing:F1}°");
            },
            useFixedUpdate: true);
    }

    /// <summary>
    /// Without projectile speed, the MPC falls back to the static facing override
    /// (or no facing bias). When SetEnemyState has projectileSpeed=0, the facing
    /// should be driven by the standard heading cost toward the waypoint, not a
    /// computed intercept angle.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyState_ZeroProjectileSpeed_NoLeadFacing()
    {
        // Enemy directly to the right, moving upward.
        // With no projectile speed, MPC won't compute an intercept angle.
        // Ship should face toward the waypoint (heading cost), roughly east.
        var enemyPos = new Vector2(20f, 0f);
        var enemyVel = new Vector2(0f, 8f);

        mpc.SetNavigationPoint(enemyPos, true, enemyVel);
        mpc.SetEnemyState(0f, 0f, 0f); // No projectile speed → no dynamic facing

        yield return AsyncAssert.WaitUntilThen(
            () => TestUtilities.AngleDeltaToTarget(ship.transform, 90f) < 15f,
            FacingTimeoutSec,
            () =>
            {
                var facing = TestUtilities.GetPlaneFacingAngle(ship.transform);
                // Without projectile speed, heading cost should pull toward waypoint (≈90° east)
                var delta = Mathf.Abs(Mathf.DeltaAngle(facing, 90f));
                Assert.That(delta, Is.LessThan(25f),
                    $"Without projectile speed, ship should face toward waypoint (~90°). Actual: {facing:F1}°");
            },
            useFixedUpdate: true);
    }

    /// <summary>
    /// After calling ClearEnemyState, the dynamic facing computed from enemy
    /// projection should no longer influence the ship. It should revert to
    /// standard waypoint heading behavior.
    /// </summary>
    [UnityTest]
    [Ignore("Base-weight facing/exposure was intentionally collapsed in the MPC retune (authority moved to per-state multipliers). Redesign for amplified weights after the reward refactor.")]
    public IEnumerator ClearEnemyState_RemovesDynamicFacing()
    {
        var enemyPos = new Vector2(20f, 0f);
        var enemyVel = new Vector2(0f, 8f);

        mpc.SetNavigationPoint(enemyPos, true, enemyVel);
        mpc.SetGoalMaintainRange(20f, 5f);
        mpc.SetEnemyState(0f, 0f, 20f);

        // Let the solver run a few frames with enemy state active
        for (var i = 0; i < 30; i++)
            yield return new WaitForFixedUpdate();

        // Now clear enemy state — dynamic facing should stop
        mpc.ClearEnemyState();
        mpc.ClearGoalMode();

        // Set a waypoint directly north so heading cost pulls to 0°
        mpc.SetNavigationPoint(new Vector2(0f, 20f));

        yield return AsyncAssert.WaitUntilThen(
            () => TestUtilities.AngleDeltaToTarget(ship.transform, 0f) < 15f,
            FacingTimeoutSec,
            () =>
            {
                var facing = TestUtilities.GetPlaneFacingAngle(ship.transform);
                var delta = Mathf.Abs(Mathf.DeltaAngle(facing, 0f));
                Assert.That(delta, Is.LessThan(20f),
                    $"After ClearEnemyState, ship should face toward waypoint (~0° north). Actual: {facing:F1}°");
            },
            useFixedUpdate: true);
    }

    /// <summary>
    /// When a waypoint has velocity, the MPC should project the goal position
    /// forward over the planning horizon. A target moving away should cause the
    /// ship to lead — heading toward where the target will be, not where it is now.
    /// </summary>
    [UnityTest]
    public IEnumerator GoalVelocity_ShipLeadsMovingWaypoint()
    {
        // Waypoint at (15, 0) moving north at 5 units/sec.
        // Ship should head toward (15, N) where N > 0, i.e. northeast, not due east.
        var waypointPos = new Vector2(15f, 0f);
        var waypointVel = new Vector2(0f, 5f);

        mpc.SetNavigationPoint(waypointPos, false, waypointVel);

        // Let the ship start moving and observe its heading
        float accumulatedYComponent = 0f;
        int samples = 0;
        var deadline = Time.realtimeSinceStartup + 4f;

        // Wait a moment for the ship to start moving
        for (var i = 0; i < 20; i++)
            yield return new WaitForFixedUpdate();

        while (Time.realtimeSinceStartup < deadline)
        {
            var vel = GamePlane.WorldDirToPlane(ship.GetComponent<Rigidbody>().linearVelocity);
            if (vel.sqrMagnitude > 0.5f)
            {
                accumulatedYComponent += vel.normalized.y;
                samples++;
            }
            yield return new WaitForFixedUpdate();
        }

        Assert.That(samples, Is.GreaterThan(10), "Ship should have measurable velocity during test");

        var avgYComponent = accumulatedYComponent / samples;
        // The ship heading should have a positive Y component, indicating it's
        // leading the target northward, not flying purely east.
        Assert.That(avgYComponent, Is.GreaterThan(0.05f),
            $"Ship velocity should have positive Y component (leading the moving goal). Avg Y: {avgYComponent:F3}");
    }

    /// <summary>
    /// Verifies that enemy yaw rate is projected forward: when the enemy is
    /// turning, the exposure cost should reflect the projected yaw, not just
    /// the initial yaw. A rapidly turning enemy should produce different ship
    /// behavior than a static one at the same initial yaw.
    /// </summary>
    [UnityTest]
    [Ignore("Base-weight facing/exposure was intentionally collapsed in the MPC retune (authority moved to per-state multipliers). Redesign for amplified weights after the reward refactor.")]
    public IEnumerator EnemyYawRate_AffectsFacingBehavior()
    {
        // Place waypoint to the right. Enemy facing east initially (yaw ~90°).
        // Case A: enemy yawRate = 0 (static)
        // Case B: enemy yawRate = 180 deg/s (rapidly turning toward ship)
        // The ship should behave differently: with high yawRate, exposure cost
        // will change across the horizon, potentially affecting the chosen trajectory.
        var enemyPos = new Vector2(20f, 0f);
        var enemyVel = Vector2.zero;

        // Run with zero yaw rate, record final facing
        mpc.SetNavigationPoint(enemyPos, true, enemyVel);
        mpc.SetGoalMaintainRange(20f, 5f);
        mpc.SetEnemyState(90f, 0f, 20f);

        for (var i = 0; i < 60; i++)
            yield return new WaitForFixedUpdate();

        var facingStatic = TestUtilities.GetPlaneFacingAngle(ship.transform);

        // Reset ship position/velocity
        ship.transform.position = Vector3.zero;
        var rb = ship.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Run with high yaw rate (enemy turning fast)
        mpc.SetEnemyState(90f, 180f, 20f);

        for (var i = 0; i < 60; i++)
            yield return new WaitForFixedUpdate();

        var facingWithYawRate = TestUtilities.GetPlaneFacingAngle(ship.transform);

        // The two facing angles should differ because the yaw rate projection
        // changes the exposure cost across the horizon.
        var delta = Mathf.Abs(Mathf.DeltaAngle(facingStatic, facingWithYawRate));
        Assert.That(delta, Is.GreaterThan(2f),
            $"Enemy yaw rate should affect ship facing. Static: {facingStatic:F1}°, WithYawRate: {facingWithYawRate:F1}°, Delta: {delta:F1}°");
    }
}

} // namespace Tests.PlayMode
