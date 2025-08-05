using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Weapons;
using ShipMain;

public class MissileLaunchPlayMode
{
    private GameObject testScene;
    private Ship shooterShip;
    private Ship targetShip;
    private MissileLauncher launcher;
    private Rigidbody shooterRb;

    [SetUp]
    public void SetUp()
    {
        TestSceneBuilder.EnableDebugRendering();
        testScene = TestSceneBuilder.CreateTestArena();
        
        shooterShip = TestSceneBuilder.CreateTestShip("Shooter", TestSceneBuilder.ShipType.Player);
        targetShip = TestSceneBuilder.CreateTestShip("Target", TestSceneBuilder.ShipType.Enemy);
        
        launcher = shooterShip.GetComponentInChildren<MissileLauncher>();
        shooterRb = shooterShip.GetComponent<Rigidbody>();
        
        Assert.IsNotNull(launcher, "Shooter ship must have a MissileLauncher");
        Assert.IsNotNull(shooterRb, "Shooter ship must have a Rigidbody");

        // Ensure ammo is full
        launcher.ReplenishAmmo();
    }

    [TearDown]
    public void TearDown()
    {
        if (testScene) Object.DestroyImmediate(testScene);
        if (shooterShip) Object.DestroyImmediate(shooterShip.gameObject);
        if (targetShip) Object.DestroyImmediate(targetShip.gameObject);
    }
    
    [UnityTest]
    public IEnumerator MissileLaunch_FromStationaryShip_MissileMovesForward()
    {
        Debug.Log($"[TEST] Starting MissileLaunch_FromStationaryShip_MissileMovesForward");

        // 1. Position shooter and target
        shooterShip.transform.position = Vector3.zero;
        targetShip.transform.position = new Vector3(0, 50, 0); // Y is forward in this project based on `transform.up` usage
        shooterShip.transform.up = (targetShip.transform.position - shooterShip.transform.position).normalized;

        // 2. Fire sequence
        yield return new WaitForSeconds(2.0f); // Wait for auto-lock
        Assert.AreEqual(MissileLauncher.LockState.Locked, launcher.State, "Launcher did not lock on target.");
        
        launcher.Fire(); // Fire missile
        yield return new WaitForFixedUpdate();

        // 3. Find the missile and check its state
        var missile = Object.FindObjectOfType<MissileProjectile>();
        Assert.IsNotNull(missile, "Missile was not fired.");
        
        var missileRb = missile.GetComponent<Rigidbody>();
        var firePoint = launcher.firePoint;

        Debug.Log($"[TEST] Missile spawned. Ship Velocity: {shooterRb.linearVelocity.magnitude:F2}, Missile Velocity: {missileRb.linearVelocity.magnitude:F2}");
        Debug.Log($"[TEST] Missile initial position: {missile.transform.position}, Firepoint position: {firePoint.position}");
        
        // 4. Verify missile moves away from the ship
        float initialDist = Vector3.Distance(missile.transform.position, firePoint.position);
        
        yield return new WaitForFixedUpdate();

        float nextDist = Vector3.Distance(missile.transform.position, firePoint.position);
        
        Debug.Log($"[TEST] Initial distance to firepoint: {initialDist:F2}, Next distance: {nextDist:F2}");
        Assert.Greater(nextDist, initialDist, "Missile should be moving away from the fire point.");
    }

    [UnityTest]
    public IEnumerator MissileLaunch_FromFastMovingShip_VerifiesFix()
    {
        Debug.Log($"[TEST] Starting MissileLaunch_FromFastMovingShip_VerifiesFix");

        // 1. Position shooter and target and set velocity
        shooterShip.transform.position = Vector3.zero;
        targetShip.transform.position = new Vector3(0, 100, 0);
        shooterShip.transform.up = (targetShip.transform.position - shooterShip.transform.position).normalized;
        shooterRb.linearVelocity = shooterShip.transform.up * 50f; // High forward speed

        Debug.Log($"[TEST] Shooter moving with velocity: {shooterRb.linearVelocity}");

        // 2. Fire sequence
        yield return new WaitForSeconds(2.0f); // Wait for auto-lock
        Assert.AreEqual(MissileLauncher.LockState.Locked, launcher.State, "Launcher did not lock on target.");
        
        launcher.Fire();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(2.0f);

        // 3. Find the missile and check its velocity
        var missile = Object.FindObjectOfType<MissileProjectile>();
        Assert.IsNotNull(missile, "Missile was not fired.");

        var missileRb = missile.GetComponent<Rigidbody>();
        var firePoint = launcher.firePoint;

        Debug.Log($"[TEST] Missile spawned. Ship Velocity: {shooterRb.linearVelocity.magnitude:F2}, Missile Velocity: {missileRb.linearVelocity.magnitude:F2}");
        
        // 4. Check if missile velocity inherited ship's velocity.
        // With the fix, it SHOULD now be the sum.
        float missileInitialSpeed = 15f; // From MissileProjectile prefab/script
        Assert.GreaterOrEqual(missileRb.linearVelocity.magnitude, shooterRb.linearVelocity.magnitude, "Missile velocity should be at least the ship's velocity.");
        Assert.LessOrEqual(missileRb.linearVelocity.magnitude, shooterRb.linearVelocity.magnitude + missileInitialSpeed + 5f, "Missile velocity is too high.");

        // 5. Verify missile moves AWAY from the ship's firepoint
        float initialDist = Vector3.Distance(missile.transform.position, firePoint.position);
        
        yield return new WaitForFixedUpdate();
        
        // Ship keeps moving, so firepoint moves.
        float nextDist = Vector3.Distance(missile.transform.position, firePoint.position);
        
        Debug.Log($"[TEST] Initial distance to firepoint: {initialDist:F2}, Next distance: {nextDist:F2}");
        Assert.Greater(nextDist, initialDist, "Missile should move away from the firepoint, confirming the fix.");
    }
} 