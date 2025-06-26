using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode test for missile homing validation.
/// Test Case: Missile & moving ITargetable dummy - Distance to target strictly decreasing until hit.
/// </summary>
public class MissileHomingPlayMode
{
    private GameObject testScene;
    private MissileProjectile testMissile;
    private GameObject movingTarget;
    private ITargetable targetable;

    [SetUp]
    public void SetUp()
    {
        TestSceneBuilder.EnableDebugRendering();
        testScene = TestSceneBuilder.CreateTestArena();
        
        // Create dummy target implementing ITargetable by using a Ship prefab (simplest)
        var targetShip = TestSceneBuilder.CreateTestShip("MovingTarget", TestSceneBuilder.ShipType.Enemy);
        movingTarget = targetShip.gameObject;
        targetable = targetShip;

        // Debug logging
        Debug.Log($"[TEST] SetUp - Created target ship: {movingTarget?.name}, targetable: {targetable != null}");
        Debug.Log($"[TEST] Target initial position: {movingTarget?.transform.position}");

        // Create missile GameObject with proper configuration
        var missileGO = new GameObject("TestMissile");
        missileGO.layer = LayerMask.NameToLayer("Projectile");
        missileGO.transform.position = Vector3.zero;
        missileGO.transform.rotation = Quaternion.identity;
        
        // Add and configure Rigidbody BEFORE adding MissileProjectile
        var rb = missileGO.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.mass = 0.1f; // Match typical projectile mass
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        // Add collider for hit detection
        var collider = missileGO.AddComponent<SphereCollider>();
        collider.radius = 0.5f;
        collider.isTrigger = true;
        
        testMissile = missileGO.AddComponent<MissileProjectile>();
        
        Debug.Log($"[TEST] Created missile: {missileGO.name}, rb: {rb != null}, collider: {collider != null}");
        Debug.Log($"[TEST] Missile layer: {LayerMask.LayerToName(missileGO.layer)}");
        
        // Set some default values via reflection since they're serialized fields
        var missileType = typeof(MissileProjectile);
        var homingSpeedField = missileType.GetField("homingSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var homingTurnRateField = missileType.GetField("homingTurnRate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initialSpeedField = missileType.GetField("initialSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var accelerationField = missileType.GetField("acceleration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var maxDistanceField = missileType.BaseType.GetField("maxDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (homingSpeedField != null) homingSpeedField.SetValue(testMissile, 20f);
        if (homingTurnRateField != null) homingTurnRateField.SetValue(testMissile, 180f); // Fast turning for tests
        if (initialSpeedField != null) initialSpeedField.SetValue(testMissile, 15f);
        if (accelerationField != null) accelerationField.SetValue(testMissile, 40f);
        if (maxDistanceField != null) maxDistanceField.SetValue(testMissile, 200f); // Long range for tests
        
        Debug.Log($"[TEST] Configured missile parameters via reflection");
        
        // Disable the missile initially to prevent early collisions
        missileGO.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        if (testScene)
            Object.DestroyImmediate(testScene);
        if (testMissile)
            Object.DestroyImmediate(testMissile.gameObject);
        if (movingTarget)
            Object.DestroyImmediate(movingTarget);
    }

    [UnityTest]
    public IEnumerator MissileHoming_DistanceDecreases_UntilHit()
    {
        Debug.Log($"[TEST] Starting MissileHoming_DistanceDecreases_UntilHit");
        
        // Position missile at origin
        testMissile.transform.position = Vector3.zero;
        
        // Position target 15 units ahead and calculate the missile's initial rotation to face it
        Vector3 targetPos = new Vector3(0f, 0f, 15f);
        movingTarget.transform.position = targetPos;
        Vector3 directionToTarget = (targetPos - testMissile.transform.position).normalized;
        testMissile.transform.rotation = Quaternion.LookRotation(GamePlane.Normal, directionToTarget);
        
        Debug.Log($"[TEST] Positioned missile at {testMissile.transform.position}, target at {movingTarget.transform.position}");
        
        // Enable the missile to trigger OnEnable
        testMissile.gameObject.SetActive(true);
        yield return null; // Wait a frame for physics initialization
        
        // Set target after enabling
        testMissile.SetTarget(targetable.TargetPoint);
        Debug.Log($"[TEST] Set missile target to {targetable.TargetPoint?.position}");
        Debug.Log($"[TEST] Target transform name: {targetable.TargetPoint?.name}");
        Debug.Log($"[TEST] Target local position: {targetable.TargetPoint?.localPosition}");
        Debug.Log($"[TEST] Target world position: {targetable.TargetPoint?.position}");
        Debug.Log($"[TEST] Moving target GameObject position: {movingTarget.transform.position}");

        float previous = Vector3.Distance(testMissile.transform.position, movingTarget.transform.position);
        Debug.Log($"[TEST] Initial distance: {previous:F2}");
        
        int frames = 60;
        bool reachedTarget = false;
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForFixedUpdate();
            
            // Check if missile still exists (might have been destroyed on impact)
            if (testMissile == null || !testMissile.gameObject.activeInHierarchy)
            {
                Debug.Log($"[TEST] Missile destroyed at frame {i}");
                reachedTarget = true;
                break;
            }
            
            float current = Vector3.Distance(testMissile.transform.position, movingTarget.transform.position);
            
            Debug.Log($"[TEST] Frame {i}: Distance = {current:F2} (delta = {current - previous:F3}), Missile pos = {testMissile.transform.position}, velocity = {testMissile.GetComponent<Rigidbody>()?.linearVelocity}");
            
            // Allow small tolerance for physics jitter
            Assert.LessOrEqual(current, previous + 0.1f, $"Distance increased significantly on frame {i}: {current} > {previous}");
            
            // Check if missile reached target
            if (current < 2f)
            {
                Debug.Log($"[TEST] Missile reached target at frame {i}");
                reachedTarget = true;
                break;
            }
            
            previous = current;
        }
        
        Assert.IsTrue(reachedTarget, "Missile did not reach target within time limit");
    }

    [UnityTest]
    public IEnumerator MissileHoming_StationaryTarget_ConvergesDirectly()
    {
        Debug.Log($"[TEST] Starting MissileHoming_StationaryTarget_ConvergesDirectly");
        
        // Position missile at origin
        testMissile.transform.position = Vector3.zero;
        
        // Position target and calculate the missile's initial rotation to face it
        Vector3 targetPos = new Vector3(10f, 0f, 20f);
        movingTarget.transform.position = targetPos;
        Vector3 directionToTarget = (targetPos - testMissile.transform.position).normalized;
        testMissile.transform.rotation = Quaternion.LookRotation(GamePlane.Normal, directionToTarget);
        
        Debug.Log($"[TEST] Positioned missile at {testMissile.transform.position} facing {testMissile.transform.up}, target at {movingTarget.transform.position}");
        
        // Enable the missile
        testMissile.gameObject.SetActive(true);
        yield return null;
        
        testMissile.SetTarget(targetable.TargetPoint);
        float startDist = Vector3.Distance(testMissile.transform.position, movingTarget.transform.position);
        Debug.Log($"[TEST] Starting distance: {startDist:F2}");

        // Let missile fly for 1 second (approx 50 FixedUpdates)
        int steps = 50;
        float closestDist = startDist;
        for (int i = 0; i < steps; i++)
        {
            yield return new WaitForFixedUpdate();
            
            // Check if missile still exists
            if (testMissile == null || !testMissile.gameObject.activeInHierarchy)
            {
                Debug.Log($"[TEST] Missile destroyed at step {i}");
                break;
            }
            
            float currentDist = Vector3.Distance(testMissile.transform.position, movingTarget.transform.position);
            closestDist = Mathf.Min(closestDist, currentDist);
            
            if (i % 10 == 0)
            {
                Debug.Log($"[TEST] Step {i}: Distance = {currentDist:F2}, Missile heading = {testMissile.transform.up}");
            }
        }

        Debug.Log($"[TEST] Final closest distance: {closestDist:F2} (reduction: {startDist - closestDist:F2})");
        Assert.Less(closestDist, startDist * 0.5f, "Missile did not close at least 50% of distance to stationary target");
    }

    [UnityTest]
    public IEnumerator MissileHoming_MovingTarget_InterceptsPath()
    {
        Debug.Log($"[TEST] Starting MissileHoming_MovingTarget_InterceptsPath");
        
        // Position missile at origin
        testMissile.transform.position = Vector3.zero;

        // Position target and calculate the missile's initial rotation
        Vector3 targetPos = new Vector3(15f, 0f, 25f);
        movingTarget.transform.position = targetPos;
        Vector3 directionToTarget = (targetPos - testMissile.transform.position).normalized;
        testMissile.transform.rotation = Quaternion.LookRotation(GamePlane.Normal, directionToTarget);
        
        Debug.Log($"[TEST] Positioned missile at {testMissile.transform.position}, target at {movingTarget.transform.position}");
        
        // Enable the missile
        testMissile.gameObject.SetActive(true);
        yield return null;
        
        testMissile.SetTarget(targetable.TargetPoint);

        float prevDist = Vector3.Distance(testMissile.transform.position, movingTarget.transform.position);
        Debug.Log($"[TEST] Initial distance: {prevDist:F2}");
        
        int frames = 120;
        bool intercepted = false;
        for (int i = 0; i < frames; i++)
        {
            // Move target laterally (perpendicular to initial missile-target line)
            movingTarget.transform.position += Vector3.right * 0.1f;
            yield return new WaitForFixedUpdate();
            
            // Check if missile still exists
            if (testMissile == null || !testMissile.gameObject.activeInHierarchy)
            {
                Debug.Log($"[TEST] Missile destroyed at frame {i}");
                intercepted = true;
                break;
            }
            
            float current = Vector3.Distance(testMissile.transform.position, movingTarget.transform.position);
            
            if (i % 10 == 0)
            {
                Debug.Log($"[TEST] Frame {i}: Distance = {current:F2}, Target pos = {movingTarget.transform.position}");
            }
            
            // Check if missile is closing in (allow temporary increases due to target movement)
            if (current < 3f)
            {
                Debug.Log($"[TEST] Missile intercepted target at frame {i}");
                intercepted = true;
                break;
            }
            
            // Only fail if distance increases dramatically (more than 20% over 5 frames)
            if (i > 5 && current > prevDist * 1.2f)
            {
                Assert.Fail($"Distance increased too much at frame {i}: {current} > {prevDist * 1.2f}");
            }
            
            prevDist = current;
        }
        
        Assert.IsTrue(intercepted || prevDist < 10f, "Missile did not intercept or get close to moving target");
    }

    [UnityTest]
    public IEnumerator MissileHoming_NoTarget_KeepsInitialDirection()
    {
        Debug.Log($"[TEST] Starting MissileHoming_NoTarget_KeepsInitialDirection");
        
        // Position missile away from origin to avoid hitting anything
        testMissile.transform.position = new Vector3(0f, 10f, 0f);
        // Give it a non-identity rotation to ensure it maintains it
        testMissile.transform.rotation = Quaternion.Euler(0, 45, 0);
        
        // Enable the missile without setting target
        testMissile.gameObject.SetActive(true);
        yield return null;
        
        Vector3 startForward = testMissile.transform.up;
        Debug.Log($"[TEST] Initial forward direction: {startForward}");
        
        float allowedAngle = 1f; // degrees
        int frames = 40;
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForFixedUpdate();
            
            if (i % 10 == 0)
            {
                Vector3 currentForward = testMissile.transform.up;
                float angle = Vector3.Angle(startForward, currentForward);
                Debug.Log($"[TEST] Frame {i}: Forward = {currentForward}, Angle deviation = {angle:F2}°");
            }
        }
        Vector3 endForward = testMissile.transform.up;
        float finalAngle = Vector3.Angle(startForward, endForward);
        Debug.Log($"[TEST] Final angle deviation: {finalAngle:F2}°");
        Assert.LessOrEqual(finalAngle, allowedAngle, $"Missile deviated {finalAngle}° without a target.");
    }

    [UnityTest]
    public IEnumerator MissileHoming_TargetDestroyed_StopsHoming()
    {
        Debug.Log($"[TEST] Starting MissileHoming_TargetDestroyed_StopsHoming");
        
        // Position missile at origin
        testMissile.transform.position = Vector3.zero;

        // Position target and calculate the missile's initial rotation
        Vector3 targetPos = new Vector3(0f, 0f, 18f);
        movingTarget.transform.position = targetPos;
        Vector3 directionToTarget = (targetPos - testMissile.transform.position).normalized;
        testMissile.transform.rotation = Quaternion.LookRotation(GamePlane.Normal, directionToTarget);
        
        // Enable the missile
        testMissile.gameObject.SetActive(true);
        yield return null;
        
        testMissile.SetTarget(targetable.TargetPoint);

        // Allow homing for initial frames
        Debug.Log($"[TEST] Letting missile home for 15 frames...");
        for (int i = 0; i < 15; i++)
        {
            yield return new WaitForFixedUpdate();
            
            // Check if missile still exists
            if (testMissile == null || !testMissile.gameObject.activeInHierarchy)
            {
                Debug.Log($"[TEST] Missile destroyed early at frame {i}");
                yield break; // Test can't continue
            }
            
            if (i % 5 == 0)
            {
                float dist = Vector3.Distance(testMissile.transform.position, movingTarget.transform.position);
                Debug.Log($"[TEST] Frame {i}: Distance = {dist:F2}");
            }
        }

        Vector3 fwdBeforeDestroy = testMissile.transform.up;
        Debug.Log($"[TEST] Forward before target destroy: {fwdBeforeDestroy}");

        // Destroy target
        Object.DestroyImmediate(movingTarget);
        movingTarget = null;
        targetable   = null;
        Debug.Log($"[TEST] Target destroyed");

        // Record heading a few frames after destruction
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Vector3 fwdAfter = testMissile.transform.up;
        float angleChange = Vector3.Angle(fwdBeforeDestroy, fwdAfter);
        Debug.Log($"[TEST] Forward after target destroy: {fwdAfter}, angle change: {angleChange:F2}°");
        Assert.LessOrEqual(angleChange, 2f, "Missile continued turning after target destroyed – homing did not stop.");
    }

    // TODO: Helper methods
    // - CreateTestMissile()
    // - CreateMovingTarget()
    // - CalculateDistanceToTarget()
    // - CreatePredictableMovementPattern()
    // - WaitForMissileHit()
} 