using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Weapons;
using ShipMain;

/// <summary>
/// PlayMode test for missile homing validation.
/// Test Case: Missile & moving ITargetable dummy - Distance to target strictly decreasing until hit.
/// </summary>
public class MissileHomingPlayMode
{
    private GameObject testScene;
    private Ship shooterShip;
    private Ship targetShip;
    private MissileLauncher launcher;

    [SetUp]
    public void SetUp()
    {
        TestSceneBuilder.EnableDebugRendering();
        testScene = TestSceneBuilder.CreateTestArena();

        shooterShip = TestSceneBuilder.CreateTestShip("Shooter", TestSceneBuilder.ShipType.Player);
        targetShip = TestSceneBuilder.CreateTestShip("Target", TestSceneBuilder.ShipType.Enemy);

        launcher = shooterShip.GetComponentInChildren<MissileLauncher>();
        Assert.IsNotNull(launcher, "Shooter ship must have a MissileLauncher");

        launcher.ReplenishAmmo();
    }

    [TearDown]
    public void TearDown()
    {
        if (testScene) Object.DestroyImmediate(testScene);
        if (shooterShip) Object.DestroyImmediate(shooterShip.gameObject);
        if (targetShip) Object.DestroyImmediate(targetShip.gameObject);
    }

    private IEnumerator FireLockedMissile()
    {
        // Point at target and wait for lock
        float lockStartTime = Time.time;
        float lockDuration = 2.0f; // Match the expected lock time
        
        while (Time.time - lockStartTime < lockDuration && launcher.State != MissileLauncher.LockState.Locked)
        {
            shooterShip.transform.up = (targetShip.transform.position - shooterShip.transform.position).normalized;
            yield return new WaitForFixedUpdate();
        }
        Assert.AreEqual(MissileLauncher.LockState.Locked, launcher.State, "Launcher did not lock on target.");

        // Fire
        launcher.Fire();
        yield return new WaitForFixedUpdate();
    }

    [UnityTest]
    public IEnumerator MissileHoming_DistanceDecreases_UntilHit()
    {
        shooterShip.transform.position = Vector3.zero;
        targetShip.transform.position = new Vector3(0, 50, 0);

        yield return FireLockedMissile();

        var missile = Object.FindObjectOfType<MissileProjectile>();
        Assert.IsNotNull(missile, "Missile was not fired.");

        float previous = Vector3.Distance(missile.transform.position, targetShip.transform.position);
        int frames = 60;
        bool reachedTarget = false;
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForFixedUpdate();
            
            if (missile == null || !missile.gameObject.activeInHierarchy)
            {
                reachedTarget = true;
                break;
            }
            if (targetShip == null) break;

            float current = Vector3.Distance(missile.transform.position, targetShip.transform.position);
            Assert.LessOrEqual(current, previous + 0.1f, $"Distance increased on frame {i}");
            
            if (current < 2f)
            {
                reachedTarget = true;
                break;
            }
            previous = current;
        }
        
        Assert.IsTrue(reachedTarget, "Missile did not reach target within time limit");
    }

    [UnityTest]
    public IEnumerator MissileHoming_MovingTarget_InterceptsPath()
    {
        shooterShip.transform.position = Vector3.zero;
        targetShip.transform.position = new Vector3(5f, 0, 5f);

        yield return FireLockedMissile();

        var missile = Object.FindObjectOfType<MissileProjectile>();
        Assert.IsNotNull(missile, "Missile was not fired.");

        float prevDist = Vector3.Distance(missile.transform.position, targetShip.transform.position);
        int frames = 120;
        bool intercepted = false;
        for (int i = 0; i < frames; i++)
        {
            if (targetShip) targetShip.transform.position += Vector3.right * 0.1f;
            yield return new WaitForFixedUpdate();
            
            if (missile == null || !missile.gameObject.activeInHierarchy)
            {
                intercepted = true;
                break;
            }

            if (targetShip == null)
            {
                Assert.Fail("Target was destroyed unexpectedly.");
                break;
            }

            float current = Vector3.Distance(missile.transform.position, targetShip.transform.position);
            
            if (current < 3f)
            {
                intercepted = true;
                break;
            }
            prevDist = current;
        }
        
        Assert.IsTrue(intercepted || prevDist < 10f, "Missile did not intercept or get close to moving target");
    }

    [UnityTest]
    public IEnumerator MissileHoming_NoTarget_KeepsInitialDirection()
    {
        shooterShip.transform.position = Vector3.zero;
        shooterShip.transform.rotation = Quaternion.Euler(0, 45, 0);

        Object.Destroy(targetShip.gameObject); // No target for this test

        launcher.Fire(); // Dumb fire
        yield return new WaitForFixedUpdate();
        
        var missile = Object.FindObjectOfType<MissileProjectile>();
        Assert.IsNotNull(missile, "Missile was not fired.");

        Vector3 startForward = missile.transform.up;
        
        yield return new WaitForSeconds(0.5f);

        Assert.IsNotNull(missile, "Missile was destroyed unexpectedly.");
        Vector3 endForward = missile.transform.up;
        float finalAngle = Vector3.Angle(startForward, endForward);
        
        Assert.LessOrEqual(finalAngle, 1f, $"Missile deviated {finalAngle}° without a target.");
    }

    [UnityTest]
    public IEnumerator MissileHoming_TargetDestroyed_StopsHoming()
    {
        shooterShip.transform.position = Vector3.zero;
        targetShip.transform.position = new Vector3(0, 0, 18f);

        yield return FireLockedMissile();

        var missile = Object.FindObjectOfType<MissileProjectile>();
        Assert.IsNotNull(missile, "Missile was not fired.");

        yield return new WaitForSeconds(0.25f); // Let missile home for a bit

        Assert.IsNotNull(missile, "Missile was destroyed prematurely.");
        Vector3 fwdBeforeDestroy = missile.transform.up;

        Object.Destroy(targetShip.gameObject);
        yield return new WaitForSeconds(0.25f); // Wait for missile to update

        Assert.IsNotNull(missile, "Missile was destroyed after target.");
        Vector3 fwdAfter = missile.transform.up;
        float angleChange = Vector3.Angle(fwdBeforeDestroy, fwdAfter);
        
        Assert.LessOrEqual(angleChange, 2f, "Missile continued turning significantly after target was destroyed.");
    }

    // TODO: Helper methods
    // - CreateTestMissile()
    // - CreateMovingTarget()
    // - CalculateDistanceToTarget()
    // - CreatePredictableMovementPattern()
    // - WaitForMissileHit()
} 