using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode tests for missile launch behavior.
/// Validates that missiles properly inherit shooter velocity and move forward.
/// </summary>
public class MissileLaunchPlayMode
{
    /*
    private TestServices services;
    private Missiles launcher;
    private Rigidbody shooterRb;

    [SetUp]
    public void SetUp()
    {
        var config = TestConfig.Load();
        Assert.IsNotNull(config, "TestConfig.asset not found at Assets/Tests/PlayMode/TestConfig.asset");

        services = config.CreateServices(separation: 50f);

        launcher = services.Player.GetComponentInChildren<Missiles>();
        shooterRb = services.Player.GetComponent<Rigidbody>();

        Assert.IsNotNull(launcher, "Player ship must have a MissileLauncher");
        Assert.IsNotNull(shooterRb, "Player ship must have a Rigidbody");

        launcher.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        services?.Dispose();
        services = null;
        launcher = null;
        shooterRb = null;
    }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator MissileLaunch_FromStationaryShip_MissileMovesForward()
    // {
    //     services.Player.transform.position = Vector3.zero;
    //     services.Enemy.transform.position = new Vector3(0, 50, 0);
    //     services.Player.transform.up = (services.Enemy.transform.position - services.Player.transform.position).normalized;

    //     yield return new WaitForSeconds(2.0f);
    //     Assert.AreEqual(LockState.Locked, launcher.Targeting.State, "Launcher did not lock on target.");

    //     launcher.Fire();
    //     yield return new WaitForFixedUpdate();

    //     var missile = Object.FindObjectOfType<Missile>();
    //     Assert.IsNotNull(missile, "Missile was not fired.");

    //     var firePoint = launcher.firePoint;
    //     float initialDist = Vector3.Distance(missile.transform.position, firePoint.position);

    //     yield return new WaitForFixedUpdate();

    //     float nextDist = Vector3.Distance(missile.transform.position, firePoint.position);
    //     Assert.Greater(nextDist, initialDist, "Missile should be moving away from the fire point.");
    // }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator MissileLaunch_FromFastMovingShip_VerifiesFix()
    // {
    //     services.Player.transform.position = Vector3.zero;
    //     services.Enemy.transform.position = new Vector3(0, 100, 0);
    //     services.Player.transform.up = (services.Enemy.transform.position - services.Player.transform.position).normalized;
    //     shooterRb.linearVelocity = services.Player.transform.up * 50f;

    //     yield return new WaitForSeconds(2.0f);
    //     Assert.AreEqual(LockState.Locked, launcher.Targeting.State, "Launcher did not lock on target.");

    //     launcher.Fire();
    //     yield return new WaitForFixedUpdate();
    //     yield return new WaitForSeconds(2.0f);

    //     var missile = Object.FindObjectOfType<Missile>();
    //     Assert.IsNotNull(missile, "Missile was not fired.");

    //     var missileRb = missile.GetComponent<Rigidbody>();
    //     var firePoint = launcher.firePoint;

    //     float missileInitialSpeed = 15f;
    //     Assert.GreaterOrEqual(missileRb.linearVelocity.magnitude, shooterRb.linearVelocity.magnitude,
    //         "Missile velocity should be at least the ship's velocity.");
    //     Assert.LessOrEqual(missileRb.linearVelocity.magnitude, shooterRb.linearVelocity.magnitude + missileInitialSpeed + 5f,
    //         "Missile velocity is too high.");

    //     float initialDist = Vector3.Distance(missile.transform.position, firePoint.position);
    //     yield return new WaitForFixedUpdate();
    //     float nextDist = Vector3.Distance(missile.transform.position, firePoint.position);

    //     Assert.Greater(nextDist, initialDist, "Missile should move away from the firepoint, confirming the fix.");
    // }
    */
}
