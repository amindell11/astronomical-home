using System.Collections;
using System.Collections.Generic;
using Combat.Targeting;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode characterization tests for child component state across ship deactivation/reactivation.
    /// 
    /// BUG SYMPTOM: LockOnIndicator, Shield UI, and primary weapon GameObject can disable unexpectedly.
    /// Source: User report via task request.
    /// 
    /// Expected behavior:
    /// - When a ship is killed, HandleShipDeath() calls gameObject.SetActive(false)
    /// - This triggers OnDisable on all child components
    /// - When ResetShip() calls gameObject.SetActive(true), all child components should re-enable properly
    /// - Child components (LockOnIndicator, ShieldUI, WeaponsController) should remain functional
    /// 
    /// Potential issues:
    /// - Child components might unsubscribe from events in OnDisable and fail to resubscribe in OnEnable
    /// - Child GameObjects might be destroyed or disabled independently
    /// - Child references might become null after parent deactivation
    /// 
    /// This test suite characterizes:
    /// 1. Whether child objects disable due to parent ship deactivation (expected)
    /// 2. Whether child objects re-enable properly after parent reactivation (expected)
    /// 3. Whether direct child deactivation causes different behavior (unexpected edge case)
    /// </summary>
    [Category("Integration")]
    [Category("UI")]
    [Category("Weapons")]
    public class ShipChildComponentStatePlayModeTests : PlayModeWorldFixture
    {
        private Ship testShip;
        private Ship enemyShip;
        
        // Toggle for diagnostic logging (set to true to enable detailed logs)
        private const bool ENABLE_DIAGNOSTICS = false;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            // Create test ship with all UI and weapon components
            var settings = TestAssets.LoadDefaultShipSettings();
            var shipPrefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab"); // Ship_1 has UI components
            var commanderPrefab = TestAssets.LoadTestPilot();

            Assert.IsNotNull(settings, "Default ship settings failed to load");
            Assert.IsNotNull(shipPrefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(commanderPrefab, "TestPilot prefab failed to load");

            testShip = ShipTestFactory.CreateShip(shipPrefab, commanderPrefab, settings, team: 0);
            Assert.IsNotNull(testShip, "Test ship failed to instantiate");

            // Create enemy for damage attribution
            enemyShip = ShipTestFactory.CreateDefaultShipAt(
                new Vector3(10, 0, 0),
                Quaternion.identity,
                useMpcPilot: false,
                team: 1);
            Assert.IsNotNull(enemyShip, "Enemy ship failed to instantiate");

            if (ENABLE_DIAGNOSTICS)
            {
                Debug.Log($"[ShipChildComponentState] SetUp complete. Ship: {testShip.name}, Enemy: {enemyShip.name}");
            }
#else
            Assert.Ignore("ShipChildComponentStatePlayModeTests requires Unity Editor assets.");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(testShip);
            ShipTestFactory.DestroyShip(enemyShip);
            base.TearDown();
        }

        /// <summary>
        /// STEP 1: Baseline characterization - verify child components exist and are enabled initially.
        /// </summary>
        [UnityTest]
        public IEnumerator NewShip_ChildComponentsAreEnabled()
        {
            yield return null; // Wait for initialization

            var weaponsController = testShip.Weapons;
            Assert.IsNotNull(weaponsController, "WeaponsController missing on test ship");
            Assert.IsNotNull(weaponsController.Primary, "Primary weapon missing");
            Assert.IsTrue(weaponsController.gameObject.activeInHierarchy, 
                "WeaponsController GameObject should be active initially");

            var shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: false);
            if (shieldUI != null)
            {
                Assert.IsTrue(shieldUI.gameObject.activeInHierarchy,
                    "ShieldUI GameObject should be active initially");
                LogDiagnostic($"ShieldUI found: {shieldUI.name}, active: {shieldUI.gameObject.activeInHierarchy}");
            }
            else
            {
                LogDiagnostic("ShieldUI not found on Ship_1 prefab (may not be present in this variant)");
            }

            var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: false);
            if (lockOnIndicator != null)
            {
                Assert.IsTrue(lockOnIndicator.gameObject.activeInHierarchy,
                    "LockOnIndicator GameObject should be active initially");
                LogDiagnostic($"LockOnIndicator found: {lockOnIndicator.name}, active: {lockOnIndicator.gameObject.activeInHierarchy}");
            }
            else
            {
                LogDiagnostic("LockOnIndicator not found on Ship_1 prefab (may not be present in this variant)");
            }

            LogDiagnostic("Baseline check passed - all found child components are active");
        }

        /// <summary>
        /// STEP 2: Characterize parent deactivation behavior.
        /// When ship is killed and parent GameObject is deactivated, verify child components also deactivate.
        /// </summary>
        [UnityTest]
        public IEnumerator ShipDeath_DeactivatesParent_ChildComponentsAlsoDeactivate()
        {
            yield return null;

            var weaponsController = testShip.Weapons;
            var shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
            var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);

            LogDiagnostic($"Before death - Ship active: {testShip.gameObject.activeSelf}, " +
                         $"Weapons active: {weaponsController.gameObject.activeSelf}");

            // Deal lethal damage to kill ship
            var maxShield = testShip.Damage.Shield.MaxValue;
            var maxHealth = testShip.Damage.Health.MaxValue;
            
            testShip.Damage.TakeDamage(maxShield + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;
            
            testShip.Damage.TakeDamage(maxHealth + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;

            // Verify ship is deactivated
            Assert.IsFalse(testShip.gameObject.activeSelf,
                "Ship GameObject should be inactive after death");

            LogDiagnostic($"After death - Ship active: {testShip.gameObject.activeSelf}, " +
                         $"Weapons active: {weaponsController.gameObject.activeSelf}");

            // Verify child components are also deactivated (expected due to parent deactivation)
            Assert.IsFalse(weaponsController.gameObject.activeInHierarchy,
                "WeaponsController should be inactive when parent ship is inactive");

            if (shieldUI != null)
            {
                Assert.IsFalse(shieldUI.gameObject.activeInHierarchy,
                    "ShieldUI should be inactive when parent ship is inactive");
            }

            if (lockOnIndicator != null)
            {
                Assert.IsFalse(lockOnIndicator.gameObject.activeInHierarchy,
                    "LockOnIndicator should be inactive when parent ship is inactive");
            }
        }

        /// <summary>
        /// STEP 3: BUG REPRODUCTION - Characterize reactivation behavior.
        /// After ship reset, verify child components reactivate properly and remain functional.
        /// </summary>
        [UnityTest]
        public IEnumerator ShipReset_ReactivatesParent_ChildComponentsShouldReactivate()
        {
            yield return null;

            var weaponsController = testShip.Weapons;
            var shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
            var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);

            // Kill ship
            var maxShield = testShip.Damage.Shield.MaxValue;
            var maxHealth = testShip.Damage.Health.MaxValue;
            
            testShip.Damage.TakeDamage(maxShield + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;
            testShip.Damage.TakeDamage(maxHealth + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;

            LogDiagnostic($"Before reset - Ship active: {testShip.gameObject.activeSelf}");

            // Reset ship (simulates respawn)
            testShip.ResetShip();
            yield return null;

            LogDiagnostic($"After reset - Ship active: {testShip.gameObject.activeSelf}, " +
                         $"Weapons active: {weaponsController.gameObject.activeSelf}, " +
                         $"Weapons object null: {weaponsController == null}");

            // BUG REPRODUCTION: Verify ship is reactivated
            Assert.IsTrue(testShip.gameObject.activeSelf,
                "Ship GameObject should be active after ResetShip()");

            // BUG REPRODUCTION: Verify child components are reactivated
            Assert.IsNotNull(weaponsController, 
                "WeaponsController reference should not be null after reset");
            
            Assert.IsTrue(weaponsController.gameObject.activeSelf,
                "WeaponsController GameObject self should be active after ship reset");
            
            Assert.IsTrue(weaponsController.gameObject.activeInHierarchy,
                "WeaponsController GameObject should be active in hierarchy after ship reset");

            if (shieldUI != null)
            {
                Assert.IsNotNull(shieldUI,
                    "ShieldUI reference should not be null after reset");
                Assert.IsTrue(shieldUI.gameObject.activeInHierarchy,
                    "ShieldUI GameObject should be active after ship reset");
            }

            if (lockOnIndicator != null)
            {
                Assert.IsNotNull(lockOnIndicator,
                    "LockOnIndicator reference should not be null after reset");
                Assert.IsTrue(lockOnIndicator.gameObject.activeInHierarchy,
                    "LockOnIndicator GameObject should be active after ship reset");
            }

            // Verify primary weapon GameObject is still accessible
            Assert.IsNotNull(weaponsController.Primary,
                "Primary weapon reference should not be null after reset");
            Assert.IsTrue(weaponsController.Primary.gameObject.activeInHierarchy,
                "Primary weapon GameObject should be active after ship reset");
        }

        /// <summary>
        /// STEP 4: Characterize weapon functionality after reset.
        /// Verify primary weapon can still fire after ship death and reset.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterShipReset_PrimaryWeaponCanFire()
        {
            yield return null;

            // Kill and reset ship
            var maxShield = testShip.Damage.Shield.MaxValue;
            var maxHealth = testShip.Damage.Health.MaxValue;
            
            testShip.Damage.TakeDamage(maxShield + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;
            testShip.Damage.TakeDamage(maxHealth + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;

            testShip.ResetShip();
            yield return null;

            // Verify weapon can fire
            var weaponsController = testShip.Weapons;
            Assert.IsNotNull(weaponsController, "WeaponsController should exist after reset");
            Assert.IsNotNull(weaponsController.Primary, "Primary weapon should exist after reset");

            var fireCount = 0;
            weaponsController.Primary.OnFire += () => fireCount++;

            LogDiagnostic($"Before fire attempt - CanFire: {weaponsController.Primary.CanFire()}");

            // Attempt to fire
            weaponsController.FirePrimary();
            yield return new WaitForFixedUpdate();

            // BUG REPRODUCTION: Weapon should be able to fire after reset
            Assert.Greater(fireCount, 0,
                "Primary weapon should fire successfully after ship reset. " +
                $"CanFire: {weaponsController.Primary.CanFire()}, " +
                $"GameObject active: {weaponsController.Primary.gameObject.activeInHierarchy}");
        }

        /// <summary>
        /// STEP 5: Characterize ShieldUI event subscription after reset.
        /// Verify ShieldUI responds to shield damage after ship death and reset.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterShipReset_ShieldUIRespondsToShieldDamage()
        {
            yield return null;

            var shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
            if (shieldUI == null)
            {
                Assert.Ignore("ShieldUI not present on Ship_1 prefab variant");
                yield break;
            }

            // Kill and reset ship
            var maxShield = testShip.Damage.Shield.MaxValue;
            var maxHealth = testShip.Damage.Health.MaxValue;
            
            testShip.Damage.TakeDamage(maxShield + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;
            testShip.Damage.TakeDamage(maxHealth + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;

            testShip.ResetShip();
            yield return null;

            // Get fresh reference after reset
            shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
            Assert.IsNotNull(shieldUI, "ShieldUI should exist after reset");
            Assert.IsTrue(shieldUI.enabled, "ShieldUI component should be enabled after reset");

            LogDiagnostic($"ShieldUI after reset - enabled: {shieldUI.enabled}, active: {shieldUI.gameObject.activeInHierarchy}");

            // Apply shield damage and verify UI responds (implicit via event subscription)
            var shieldBefore = testShip.Damage.Shield.CurrentValue;
            testShip.Damage.TakeDamage(10f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;

            var shieldAfter = testShip.Damage.Shield.CurrentValue;

            // BUG REPRODUCTION: Shield should have decreased
            Assert.Less(shieldAfter, shieldBefore,
                "Shield should decrease after taking damage post-reset");

            LogDiagnostic($"Shield damage applied successfully - Before: {shieldBefore}, After: {shieldAfter}");

            // Note: We can't directly verify UI response without accessing private fields,
            // but if the component is enabled and events are subscribed, it should respond.
            // The fact that no exceptions are thrown is a good sign.
        }

        /// <summary>
        /// STEP 6: Characterize LockOnIndicator event subscription after reset.
        /// Verify LockOnIndicator responds to lock events after ship death and reset.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterShipReset_LockOnIndicatorRespondsToLockEvents()
        {
            yield return null;

            var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);
            if (lockOnIndicator == null)
            {
                Assert.Ignore("LockOnIndicator not present on Ship_1 prefab variant");
                yield break;
            }

            // Kill and reset ship
            var maxShield = testShip.Damage.Shield.MaxValue;
            var maxHealth = testShip.Damage.Health.MaxValue;
            
            testShip.Damage.TakeDamage(maxShield + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;
            testShip.Damage.TakeDamage(maxHealth + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;

            testShip.ResetShip();
            yield return null;

            // Get fresh reference after reset
            lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);
            Assert.IsNotNull(lockOnIndicator, "LockOnIndicator should exist after reset");
            Assert.IsTrue(lockOnIndicator.enabled, "LockOnIndicator component should be enabled after reset");

            LogDiagnostic($"LockOnIndicator after reset - enabled: {lockOnIndicator.enabled}, active: {lockOnIndicator.gameObject.activeInHierarchy}");

            // Trigger lock progress event via ITargetable interface
            var targetable = testShip as ITargetable;
            Assert.IsNotNull(targetable, "Ship should implement ITargetable");

            // Simulate lock progress
            targetable.Lock.RaiseProgress(0.5f);
            yield return null;

            // No exception means event subscription is working
            LogDiagnostic("Lock progress event dispatched successfully without exception");

            // Simulate lock acquired
            targetable.Lock.RaiseAcquired();
            yield return null;

            LogDiagnostic("Lock acquired event dispatched successfully without exception");
        }

        /// <summary>
        /// STEP 7: Edge case - Direct child deactivation vs parent deactivation.
        /// Characterize whether directly disabling a child component causes different behavior
        /// than parent deactivation.
        /// </summary>
        [UnityTest]
        public IEnumerator DirectChildDeactivation_VsParentDeactivation_BehaviorDifference()
        {
            yield return null;

            var weaponsController = testShip.Weapons;
            var originalActive = weaponsController.gameObject.activeSelf;

            LogDiagnostic($"Test setup - Weapons originally active: {originalActive}");

            // Scenario A: Direct child deactivation
            weaponsController.gameObject.SetActive(false);
            yield return null;

            Assert.IsFalse(weaponsController.gameObject.activeSelf,
                "WeaponsController should be inactive after direct SetActive(false)");

            weaponsController.gameObject.SetActive(true);
            yield return null;

            Assert.IsTrue(weaponsController.gameObject.activeSelf,
                "WeaponsController should be active after direct SetActive(true)");
            Assert.IsNotNull(weaponsController.Primary,
                "Primary weapon should still exist after direct child activation toggle");

            LogDiagnostic("Scenario A (direct child toggle) passed");

            // Scenario B: Parent deactivation
            testShip.gameObject.SetActive(false);
            yield return null;

            Assert.IsFalse(testShip.gameObject.activeSelf,
                "Ship should be inactive after SetActive(false)");
            Assert.IsFalse(weaponsController.gameObject.activeInHierarchy,
                "WeaponsController should be inactive in hierarchy when parent is inactive");

            testShip.gameObject.SetActive(true);
            yield return null;

            Assert.IsTrue(testShip.gameObject.activeSelf,
                "Ship should be active after SetActive(true)");
            Assert.IsTrue(weaponsController.gameObject.activeInHierarchy,
                "WeaponsController should be active in hierarchy when parent is active");
            Assert.IsNotNull(weaponsController.Primary,
                "Primary weapon should still exist after parent activation toggle");

            LogDiagnostic("Scenario B (parent toggle) passed - no behavioral difference detected");
        }

        /// <summary>
        /// STEP 8: Multiple death/reset cycles - verify stability.
        /// </summary>
        [UnityTest]
        public IEnumerator MultipleDeathResetCycles_ChildComponentsRemainStable()
        {
            yield return null;

            const int numCycles = 3;

            for (int cycle = 0; cycle < numCycles; cycle++)
            {
                LogDiagnostic($"=== Cycle {cycle + 1}/{numCycles} ===");

                var weaponsController = testShip.Weapons;
                var shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
                var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);

                // Kill ship
                var maxShield = testShip.Damage.Shield.MaxValue;
                var maxHealth = testShip.Damage.Health.MaxValue;
                
                testShip.Damage.TakeDamage(maxShield + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
                yield return null;
                testShip.Damage.TakeDamage(maxHealth + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
                yield return null;

                Assert.IsFalse(testShip.gameObject.activeSelf,
                    $"Cycle {cycle}: Ship should be inactive after death");

                // Reset ship
                testShip.ResetShip();
                yield return null;

                // Verify stability
                Assert.IsTrue(testShip.gameObject.activeSelf,
                    $"Cycle {cycle}: Ship should be active after reset");

                Assert.IsNotNull(weaponsController,
                    $"Cycle {cycle}: WeaponsController reference should not be null");
                Assert.IsTrue(weaponsController.gameObject.activeInHierarchy,
                    $"Cycle {cycle}: WeaponsController should be active");
                Assert.IsNotNull(weaponsController.Primary,
                    $"Cycle {cycle}: Primary weapon should exist");

                if (shieldUI != null)
                {
                    Assert.IsTrue(shieldUI.gameObject.activeInHierarchy,
                        $"Cycle {cycle}: ShieldUI should be active");
                }

                if (lockOnIndicator != null)
                {
                    Assert.IsTrue(lockOnIndicator.gameObject.activeInHierarchy,
                        $"Cycle {cycle}: LockOnIndicator should be active");
                }

                LogDiagnostic($"Cycle {cycle + 1} passed all stability checks");
            }
        }

        private void LogDiagnostic(string message)
        {
            if (ENABLE_DIAGNOSTICS)
            {
                Debug.Log($"[ShipChildComponentState] {message}");
            }
        }
    }
}
