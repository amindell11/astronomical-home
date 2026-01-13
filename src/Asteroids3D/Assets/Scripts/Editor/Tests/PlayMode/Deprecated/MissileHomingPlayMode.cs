using System.Collections;
using NUnit.Framework;
using Ships;
using UnityEngine;
using UnityEngine.TestTools;
using Weapons;

/// <summary>
/// PlayMode tests for missile homing behavior.
/// Validates that missiles track targets and reduce distance until impact.
/// </summary>
public class MissileHomingPlayMode
{
    private TestServices services;
    private Missiles launcher;

    [SetUp]
    public void SetUp()
    {
        var config = TestConfig.Load();
        Assert.IsNotNull(config, "TestConfig.asset not found at Assets/Tests/PlayMode/TestConfig.asset");

        services = config.CreateServices(separation: 50f);

        launcher = services.Player.GetComponentInChildren<Missiles>();
        Assert.IsNotNull(launcher, "Player ship must have a MissileLauncher");

        launcher.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        services?.Dispose();
        services = null;
        launcher = null;
    }

    private IEnumerator FireLockedMissile()
    {
        var lockStartTime = Time.time;
        const float lockDuration = 2.0f;

        while (Time.time - lockStartTime < lockDuration && launcher.Targeting.State != LockState.Locked)
        {
            services.Player.transform.up = (services.Enemy.transform.position - services.Player.transform.position).normalized;
            yield return new WaitForFixedUpdate();
        }
        Assert.AreEqual(LockState.Locked, launcher.Targeting.State, "Launcher did not lock on target.");

        launcher.Fire();
        yield return new WaitForFixedUpdate();
    }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator MissileHoming_DistanceDecreases_UntilHit()
    // {
    //     services.Player.transform.position = Vector3.zero;
    //     services.Enemy.transform.position = new Vector3(0, 50, 0);

    //     yield return FireLockedMissile();

    //     var missile = Object.FindObjectOfType<Missile>();
    //     Assert.IsNotNull(missile, "Missile was not fired.");

    //     var previous = Vector3.Distance(missile.transform.position, services.Enemy.transform.position);
    //     var reachedTarget = false;

    //     for (int i = 0; i < 60; i++)
    //     {
    //         yield return new WaitForFixedUpdate();

    //         if (!missile || !missile.gameObject.activeInHierarchy)
    //         {
    //             reachedTarget = true;
    //             break;
    //         }
    //         if (!services.Enemy) break;

    //         float current = Vector3.Distance(missile.transform.position, services.Enemy.transform.position);
    //         Assert.LessOrEqual(current, previous + 0.1f, $"Distance increased on frame {i}");

    //         if (current < 2f)
    //         {
    //             reachedTarget = true;
    //             break;
    //         }
    //         previous = current;
    //     }

    //     Assert.IsTrue(reachedTarget, "Missile did not reach target within time limit");
    // }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator MissileHoming_MovingTarget_InterceptsPath()
    // {
    //     services.Player.transform.position = Vector3.zero;
    //     services.Enemy.transform.position = new Vector3(5f, 0, 5f);

    //     yield return FireLockedMissile();

    //     var missile = Object.FindObjectOfType<Missile>();
    //     Assert.IsNotNull(missile, "Missile was not fired.");

    //     var prevDist = Vector3.Distance(missile.transform.position, services.Enemy.transform.position);
    //     var intercepted = false;

    //     for (var i = 0; i < 120; i++)
    //     {
    //         if (services.Enemy) services.Enemy.transform.position += Vector3.right * 0.1f;
    //         yield return new WaitForFixedUpdate();

    //         if (!missile || !missile.gameObject.activeInHierarchy)
    //         {
    //             intercepted = true;
    //             break;
    //         }

    //         if (!services.Enemy)
    //         {
    //             Assert.Fail("Target was destroyed unexpectedly.");
    //             break;
    //         }

    //         var current = Vector3.Distance(missile.transform.position, services.Enemy.transform.position);
    //         if (current < 3f)
    //         {
    //             intercepted = true;
    //             break;
    //         }
    //         prevDist = current;
    //     }

    //     Assert.IsTrue(intercepted || prevDist < 10f, "Missile did not intercept or get close to moving target");
    // }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator MissileHoming_NoTarget_KeepsInitialDirection()
    // {
    //     services.Player.transform.position = Vector3.zero;
    //     services.Player.transform.rotation = Quaternion.Euler(0, 45, 0);

    //     Object.Destroy(services.Enemy.gameObject);

    //     launcher.Fire();
    //     yield return new WaitForFixedUpdate();

    //     var missile = Object.FindObjectOfType<Missile>();
    //     Assert.IsNotNull(missile, "Missile was not fired.");

    //     var startForward = missile.transform.up;
    //     yield return new WaitForSeconds(0.5f);

    //     Assert.IsNotNull(missile, "Missile was destroyed unexpectedly.");
    //     var endForward = missile.transform.up;
    //     var finalAngle = Vector3.Angle(startForward, endForward);

    //     Assert.LessOrEqual(finalAngle, 1f, $"Missile deviated {finalAngle}° without a target.");
    // }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator MissileHoming_TargetDestroyed_StopsHoming()
    // {
    //     services.Player.transform.position = Vector3.zero;
    //     services.Enemy.transform.position = new Vector3(0, 0, 18f);

    //     yield return FireLockedMissile();

    //     var missile = Object.FindObjectOfType<Missile>();
    //     Assert.IsNotNull(missile, "Missile was not fired.");

    //     yield return new WaitForSeconds(0.25f);

    //     Assert.IsNotNull(missile, "Missile was destroyed prematurely.");
    //     Vector3 fwdBeforeDestroy = missile.transform.up;

    //     Object.Destroy(services.Enemy.gameObject);
    //     yield return new WaitForSeconds(0.25f);

    //     Assert.IsNotNull(missile, "Missile was destroyed after target.");
    //     Vector3 fwdAfter = missile.transform.up;
    //     float angleChange = Vector3.Angle(fwdBeforeDestroy, fwdAfter);

    //     Assert.LessOrEqual(angleChange, 2f, "Missile continued turning significantly after target was destroyed.");
    // }
}
