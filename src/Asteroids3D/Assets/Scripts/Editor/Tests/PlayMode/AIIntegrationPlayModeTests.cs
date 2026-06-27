#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using AI;
using AI.States;
using Movement.MPC;
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
    // Helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a 2-keyframe AnimationCurve with zero tangents.
    /// Reproduces the old smoothstep behavior: Lerp(atLow, atHigh, smoothstep(t)).
    /// </summary>
    private static AnimationCurve MakeCurve(float atLow, float atHigh)
    {
        return new AnimationCurve(
            new Keyframe(0f, atLow, 0f, 0f),
            new Keyframe(1f, atHigh, 0f, 0f));
    }

    // ──────────────────────────────────────────────────────────
    // State Profile Helpers
    // ──────────────────────────────────────────────────────────

    private static StateProfile CreateAttackProfile()
    {
        var p = ScriptableObject.CreateInstance<StateProfile>();
        p.name = "Attack";
        p.goal = new TrackEnemyGoal { desiredRange = 17.5f, rangeTolerance = 12.5f };
        p.enableTacticalCosts = true;
        p.enableFiring = true;
        p.weightMultipliers = WeightMultipliers.Default;
        p.requiresEnemy = true;
        p.maxRange = 30f;
        p.utilityFactors = new List<UtilityFactor>
        {
            new CurveFactor { input = ContinuousInput.Health, response = MakeCurve(0.65f, 1.35f) },
            new CurveFactor { input = ContinuousInput.Shield, response = MakeCurve(0.7f, 1.3f) },
            new CurveFactor { input = ContinuousInput.EnemyDurability, response = MakeCurve(1.2f, 0.8f) },
            new RangeBandFactor { optimalMin = 5f, optimalMax = 30f },
            new BinaryFactor { signal = BinarySignal.LOS, whenTrue = 1.4f, whenFalse = 0.6f },
            new CurveFactor { input = ContinuousInput.Outnumbered, response = MakeCurve(1.0f, 0.7f) },
        };
        return p;
    }

    private static StateProfile CreatePatrolProfile()
    {
        var p = ScriptableObject.CreateInstance<StateProfile>();
        p.name = "Patrol";
        p.goal = new RandomWaypointGoal
        {
            patrolRadius = 50f,
            minDistanceFactor = 0.3f,
            arriveRadius = 3f,
            stuckTimeout = 3f,
            stuckProgressThreshold = 1f,
        };
        p.enableTacticalCosts = false;
        p.enableFiring = false;
        p.weightMultipliers = WeightMultipliers.Default;
        p.requiresNoEnemy = true;
        p.utilityFactors = new List<UtilityFactor>
        {
            new BinaryFactor { signal = BinarySignal.NoCombat, whenTrue = 2.0f, whenFalse = 0.01f },
        };
        return p;
    }

    private static StateProfile CreateEvadeProfile()
    {
        var p = ScriptableObject.CreateInstance<StateProfile>();
        p.name = "Evade";
        p.goal = new FleeEnemyGoal();
        p.enableTacticalCosts = false;
        p.enableFiring = false;
        p.weightMultipliers = WeightMultipliers.Default;
        p.requiresEnemy = true;
        p.utilityFactors = new List<UtilityFactor>
        {
            new CurveFactor { input = ContinuousInput.Health, response = MakeCurve(1.8f, 0.2f) },
            new CurveFactor { input = ContinuousInput.Shield, response = MakeCurve(1.6f, 0.4f) },
            new CurveFactor { input = ContinuousInput.Outnumbered, response = MakeCurve(0.6f, 1.4f) },
            new BinaryFactor { signal = BinarySignal.LOS, whenTrue = 1.2f, whenFalse = 0.8f },
            new CurveFactor { input = ContinuousInput.ClosingRate, response = MakeCurve(0.7f, 1.3f) },
            new CurveFactor { input = ContinuousInput.EnemyFacing, response = MakeCurve(0.5f, 1.5f) },
            new CurveFactor { input = ContinuousInput.SelfAngle, response = MakeCurve(0.8f, 1.2f) },
        };
        return p;
    }

    private static AIState CreateState(StateProfile profile, AICommander cmdr)
    {
        return new AIState(profile, cmdr.Navigator, cmdr.Gunner);
    }

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
            CreateState(CreateAttackProfile(), cmdrA),
            CreateState(CreatePatrolProfile(), cmdrA));

        // Wait for Attack state and navigation toward enemy
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Attack" &&
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
            CreateState(CreateAttackProfile(), cmdrA),
            CreateState(CreatePatrolProfile(), cmdrA));

        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Patrol",
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

        InitializeWithStates(cmdrA,
            CreateState(CreateAttackProfile(), cmdrA),
            CreateState(CreateEvadeProfile(), cmdrA));

        // Wait for Attack state first
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Attack",
            timeoutSec: 5f,
            failureMessage: "Ship did not enter Attack state",
            useFixedUpdate: true);

        // Deal massive damage: drain shield + 90% health
        var maxShield = shipA.Damage.Shield.MaxValue;
        var maxHealth = shipA.Damage.Health.MaxValue;
        DealDamage(shipA, maxShield + maxHealth * 0.9f);

        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Evade",
            timeoutSec: 5f,
            failureMessage: "Ship did not transition to Evade after heavy damage",
            useFixedUpdate: true);
    }

    [UnityTest]
    public IEnumerator Transition_EnemyDestroyed_AttackToPatrol()
    {
        var (_, cmdrA) = CreateAIShip(Vector3.zero, team: 0);
        var (shipB, _) = CreateAIShip(new Vector3(15f, 0f, 0f), team: 1);

        InitializeWithStates(cmdrA,
            CreateState(CreateAttackProfile(), cmdrA),
            CreateState(CreatePatrolProfile(), cmdrA));

        // Wait for Attack state
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Attack",
            timeoutSec: 5f,
            failureMessage: "Ship did not enter Attack state",
            useFixedUpdate: true);

        // Destroy enemy
        Object.Destroy(shipB.gameObject);

        // Should transition to Patrol after combat exit delay (~3s default)
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Patrol",
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

        InitializeWithStates(cmdrA,
            CreateState(CreateAttackProfile(), cmdrA),
            CreateState(CreatePatrolProfile(), cmdrA));

        // Wait for Attack state with valid waypoint
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Attack" &&
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

        var attackState = CreateState(CreateAttackProfile(), cmdrA);
        var evadeState = CreateState(CreateEvadeProfile(), cmdrA);
        InitializeWithStates(cmdrA, attackState, evadeState);

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

        InitializeWithStates(cmdrA,
            CreateState(CreateAttackProfile(), cmdrA),
            CreateState(CreatePatrolProfile(), cmdrA));

        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Attack" &&
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

        var attackState = CreateState(CreateAttackProfile(), cmdrA);
        var evadeState = CreateState(CreateEvadeProfile(), cmdrA);
        InitializeWithStates(cmdrA, attackState, evadeState);

        // Wait for Attack + gunner target
        yield return AsyncAssert.WaitUntil(
            () => cmdrA.UtilitySelector.CurrentState?.ProfileName == "Attack" &&
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
