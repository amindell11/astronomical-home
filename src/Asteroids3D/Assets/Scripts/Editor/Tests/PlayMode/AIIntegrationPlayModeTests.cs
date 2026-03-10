#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AI.States;
using AI.Utility;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{

[TestFixture]
[Category("Integration")]
[Category("Slow")]
public class AIIntegrationPlayModeTests : AIIntegrationFixture
{
    // ──────────────────────────────────────────────────────────
    // Scanning & Enemy Acquisition
    // ──────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Scan_DetectsEnemyShip_CombatTrackerAcquires()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        CreateAIShip(new Vector3(15f, 0f, 0f), team: 1);

        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.Context != null &&
                  cmdrA.UtilitySelector.Context.Combat.HasEnemy,
            timeoutSec: 5f,
            failureMessage: "CombatTracker did not acquire enemy within timeout",
            useFixedUpdate: true);
    }

    [UnityTest]
    public IEnumerator Scan_DistantEnemy_NotDetected()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        CreateAIShip(new Vector3(100f, 0f, 0f), team: 1);

        yield return AsyncAssert.WaitAndAssertRemainsFalse(
            () => cmdrA.UtilitySelector.Context != null &&
                  cmdrA.UtilitySelector.Context.Combat.HasEnemy,
            waitSec: 2f,
            failureMessage: "Distant enemy should not be detected",
            useFixedUpdate: true);
    }

    // ──────────────────────────────────────────────────────────
    // Full Loop: Scan → Utility → State → Nav
    // ──────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator FullLoop_EnemyDetected_AttacksAndNavigatesToEnemy()
    {
        var (shipA, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        var (shipB, _) = CreateAIShip(new Vector3(20f, 0f, 0f), team: 1);

        InitializeWithStates(cmdrA,
            new Attack(cmdrA.Navigator, cmdrA.Gunner, ScriptableObject.CreateInstance<UtilityTuning>()),
            new Patrol(cmdrA.Navigator, cmdrA.Gunner, ScriptableObject.CreateInstance<UtilityTuning>()));

        // Wait for Attack state and navigation toward enemy
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Attack &&
                  cmdrA.Navigator.CurrentWaypoint.isValid,
            timeoutSec: 6f,
            failureMessage: "Ship did not enter Attack state with valid waypoint",
            useFixedUpdate: true);

        // Verify ship is moving toward enemy
        var initialDist = Vector3.Distance(shipA.transform.position, shipB.transform.position);

        yield return AsyncAssert.WaitUntil(
            () => Vector3.Distance(shipA.transform.position, shipB.transform.position) < initialDist - 1f,
            timeoutSec: 5f,
            failureMessage: "Ship did not move toward enemy",
            useFixedUpdate: true);
    }

    [UnityTest]
    public IEnumerator FullLoop_NoEnemy_PatrolStateSelected()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);

        InitializeWithStates(cmdrA,
            new Attack(cmdrA.Navigator, cmdrA.Gunner, ScriptableObject.CreateInstance<UtilityTuning>()),
            new Patrol(cmdrA.Navigator, cmdrA.Gunner, ScriptableObject.CreateInstance<UtilityTuning>()));

        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Patrol,
            timeoutSec: 5f,
            failureMessage: "Ship did not transition to Patrol when no enemy present",
            useFixedUpdate: true);

        Assert.IsTrue(cmdrA.Navigator.CurrentWaypoint.isValid,
            "Patrol should set a valid waypoint");
    }

    // ──────────────────────────────────────────────────────────
    // State Transitions
    // ──────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Transition_HealthDrops_AttackToEvade()
    {
        var (shipA, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        CreateAIShip(new Vector3(15f, 0f, 0f), team: 1);

        var tuning = ScriptableObject.CreateInstance<UtilityTuning>();
        InitializeWithStates(cmdrA,
            new Attack(cmdrA.Navigator, cmdrA.Gunner, tuning),
            new Evade(cmdrA.Navigator, cmdrA.Gunner, tuning));

        // Wait for Attack state first
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Attack,
            timeoutSec: 5f,
            failureMessage: "Ship did not enter Attack state",
            useFixedUpdate: true);

        // Deal massive damage: drain shield + 90% health
        var maxShield = shipA.Damage.Shield.MaxValue;
        var maxHealth = shipA.Damage.Health.MaxValue;
        DealDamage(shipA, maxShield + maxHealth * 0.9f);

        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Evade,
            timeoutSec: 5f,
            failureMessage: "Ship did not transition to Evade after heavy damage",
            useFixedUpdate: true);
    }

    [UnityTest]
    public IEnumerator Transition_EnemyDestroyed_AttackToPatrol()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        var (shipB, _) = CreateAIShip(new Vector3(15f, 0f, 0f), team: 1);

        var tuning = ScriptableObject.CreateInstance<UtilityTuning>();
        InitializeWithStates(cmdrA,
            new Attack(cmdrA.Navigator, cmdrA.Gunner, tuning),
            new Patrol(cmdrA.Navigator, cmdrA.Gunner, tuning));

        // Wait for Attack state
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Attack,
            timeoutSec: 5f,
            failureMessage: "Ship did not enter Attack state",
            useFixedUpdate: true);

        // Destroy enemy
        Object.Destroy(shipB.gameObject);

        // Should transition to Patrol after combat exit delay (~3s default)
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Patrol,
            timeoutSec: 8f,
            failureMessage: "Ship did not transition to Patrol after enemy destroyed",
            useFixedUpdate: true);
    }

    // ──────────────────────────────────────────────────────────
    // Navigation Behavior Per State
    // ──────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator AttackState_NavigatorTargetsEnemyPosition()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        var (shipB, _) = CreateAIShip(new Vector3(20f, 0f, 20f), team: 1);

        // Wait for Attack state with valid waypoint
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Attack &&
                  cmdrA.Navigator.CurrentWaypoint.isValid,
            timeoutSec: 6f,
            failureMessage: "Ship did not enter Attack state with valid waypoint",
            useFixedUpdate: true);

        // Waypoint should be near enemy position (within some tolerance for prediction)
        var wp = cmdrA.Navigator.CurrentWaypoint.position;
        var enemyPlane = new Vector2(shipB.transform.position.x, shipB.transform.position.z);
        var dist = Vector2.Distance(wp, enemyPlane);
        Assert.Less(dist, 30f, $"Attack waypoint ({wp}) should be near enemy position ({enemyPlane}), was {dist} away");
    }

    [UnityTest]
    public IEnumerator EvadeState_NavigatorFleesFromEnemy()
    {
        var (shipA, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        CreateAIShip(new Vector3(10f, 0f, 0f), team: 1);

        var tuning = ScriptableObject.CreateInstance<UtilityTuning>();
        var evadeState = new Evade(cmdrA.Navigator, cmdrA.Gunner, tuning);
        InitializeWithStates(cmdrA,
            new Attack(cmdrA.Navigator, cmdrA.Gunner, tuning),
            evadeState);

        // Wait for enemy detection then force transition to Evade
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.Context != null &&
                  cmdrA.UtilitySelector.Context.Combat.HasEnemy,
            timeoutSec: 5f,
            failureMessage: "Enemy not detected",
            useFixedUpdate: true);

        cmdrA.UtilitySelector.ForceTransition(evadeState, cmdrA.UtilitySelector.Context);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(cmdrA.Navigator.CurrentWaypoint.isValid,
            "Evade should set a valid waypoint");

        // In Flee mode the nav target is the enemy position; the MPC cost function
        // drives the ship away. Verify the ship actually increases distance over time.
        var initialDist = Vector3.Distance(shipA.transform.position, new Vector3(10f, 0f, 0f));

        yield return AsyncAssert.WaitUntil(
            () => Vector3.Distance(shipA.transform.position, new Vector3(10f, 0f, 0f)) > initialDist + 1f,
            timeoutSec: 5f,
            failureMessage: "Ship did not move away from enemy in Evade/Flee mode",
            useFixedUpdate: true);
    }

    // ──────────────────────────────────────────────────────────
    // Gunner Integration
    // ──────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator AttackState_GunnerReceivesTarget()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        CreateAIShip(new Vector3(15f, 0f, 0f), team: 1);

        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Attack &&
                  cmdrA.Gunner.HasTarget,
            timeoutSec: 6f,
            failureMessage: "Gunner did not receive target in Attack state",
            useFixedUpdate: true);
    }

    [UnityTest]
    public IEnumerator EvadeState_GunnerTargetCleared()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        CreateAIShip(new Vector3(15f, 0f, 0f), team: 1);

        var tuning = ScriptableObject.CreateInstance<UtilityTuning>();
        var evadeState = new Evade(cmdrA.Navigator, cmdrA.Gunner, tuning);
        InitializeWithStates(cmdrA,
            new Attack(cmdrA.Navigator, cmdrA.Gunner, tuning),
            evadeState);

        // Wait for Attack + gunner target
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.Type == StateType.Attack &&
                  cmdrA.Gunner.HasTarget,
            timeoutSec: 6f,
            failureMessage: "Gunner did not get target in Attack",
            useFixedUpdate: true);

        // Force to Evade
        cmdrA.UtilitySelector.ForceTransition(evadeState, cmdrA.UtilitySelector.Context);

        yield return AsyncAssert.WaitUntil(
            () => !cmdrA.Gunner.HasTarget,
            timeoutSec: 3f,
            failureMessage: "Gunner target should be cleared in Evade state",
            useFixedUpdate: true);
    }

    // ──────────────────────────────────────────────────────────
    // Assessment Accuracy
    // ──────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Assessment_ReflectsEnemyCount()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        CreateAIShip(new Vector3(12f, 0f, 0f), team: 1);
        CreateAIShip(new Vector3(0f, 0f, 12f), team: 1);

        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.Context != null &&
                  cmdrA.UtilitySelector.Context.Assessment.NearbyEnemyCount == 2,
            timeoutSec: 5f,
            failureMessage: "Assessment did not report 2 nearby enemies",
            useFixedUpdate: true);
    }
}

} // namespace Tests.PlayMode
#endif
