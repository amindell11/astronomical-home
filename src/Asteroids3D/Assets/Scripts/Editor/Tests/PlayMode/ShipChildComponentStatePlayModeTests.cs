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
using UnityEngine.UI;

namespace Tests.PlayMode
{
    /// <summary>Child components (LockOnIndicator, ShieldUI, WeaponsController) deactivate with the ship on death and come back functional — still firing, still event-subscribed — after reset, across repeated cycles.</summary>
    [Category("Ships")]
    public class ShipChildComponentStatePlayModeTests : PlayModeWorldFixture
    {
        private Ship testShip;
        private Ship combatShip;
        private Ship enemyShip;
        
        private const bool ENABLE_DIAGNOSTICS = false;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            var shipPrefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab"); // Ship_1 has UI components
            var commanderPrefab = TestAssets.LoadTestPilotMpc();

            Assert.IsNotNull(shipPrefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(commanderPrefab, "TestPilotMPC prefab failed to load");

            testShip = ShipTestFactory.CreateShip(shipPrefab, commanderPrefab, Projectiles, team: 0);
            combatShip = testShip;
            Assert.IsNotNull(testShip, "Test ship failed to instantiate");
            Assert.IsNotNull(combatShip.Weapons, "Test ship should be armed (WeaponsController present)");

            enemyShip = ShipTestFactory.CreateDefaultShipAt(
                new Vector3(10, 0, 0),
                Quaternion.identity,
                Projectiles,
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

        [UnityTest]
        public IEnumerator ShipDeath_DeactivatesParent_ChildComponentsAlsoDeactivate()
        {
            yield return null;

            var weaponsController = combatShip.Weapons;
            var shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
            var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);

            LogDiagnostic($"Before death - Ship active: {testShip.gameObject.activeSelf}, " +
                         $"Weapons active: {weaponsController.gameObject.activeSelf}");

            TestDamage.Kill(testShip, enemyShip);
            yield return null;

            Assert.IsFalse(testShip.gameObject.activeSelf,
                "Ship GameObject should be inactive after death");

            LogDiagnostic($"After death - Ship active: {testShip.gameObject.activeSelf}, " +
                         $"Weapons active: {weaponsController.gameObject.activeSelf}");

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

        [UnityTest]
        public IEnumerator ShipReset_ReactivatesParent_ChildComponentsShouldReactivate()
        {
            yield return null;

            var weaponsController = combatShip.Weapons;
            var shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
            var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);

            TestDamage.Kill(testShip, enemyShip);
            yield return null;

            LogDiagnostic($"Before reset - Ship active: {testShip.gameObject.activeSelf}");

            testShip.ResetShip();
            yield return null;

            LogDiagnostic($"After reset - Ship active: {testShip.gameObject.activeSelf}, " +
                         $"Weapons active: {weaponsController.gameObject.activeSelf}, " +
                         $"Weapons object null: {weaponsController == null}");

            Assert.IsTrue(testShip.gameObject.activeSelf,
                "Ship GameObject should be active after ResetShip()");

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

            Assert.IsNotNull(weaponsController.Primary,
                "Primary weapon reference should not be null after reset");
            Assert.IsTrue(weaponsController.Primary.gameObject.activeInHierarchy,
                "Primary weapon GameObject should be active after ship reset");
        }

        [UnityTest]
        public IEnumerator AfterShipReset_PrimaryWeaponCanFire()
        {
            yield return null;

            TestDamage.Kill(testShip, enemyShip);
            yield return null;

            testShip.ResetShip();
            yield return null;

            var weaponsController = combatShip.Weapons;
            Assert.IsNotNull(weaponsController, "WeaponsController should exist after reset");
            Assert.IsNotNull(weaponsController.Primary, "Primary weapon should exist after reset");

            var fireCount = 0;
            weaponsController.Primary.OnFire += () => fireCount++;

            LogDiagnostic($"Before fire attempt - CanFire: {weaponsController.Primary.CanFire()}");

            weaponsController.Arm(Projectiles).Fire(Ships.Command.WeaponSlot.Primary,
                new Ships.Command.WeaponCommand { pressed = true, held = true });
            yield return new WaitForFixedUpdate();

            Assert.Greater(fireCount, 0,
                "Primary weapon should fire successfully after ship reset. " +
                $"CanFire: {weaponsController.Primary.CanFire()}, " +
                $"GameObject active: {weaponsController.Primary.gameObject.activeInHierarchy}");
        }

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

            TestDamage.Kill(testShip, enemyShip);
            yield return null;

            testShip.ResetShip();
            yield return null;

            shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
            Assert.IsNotNull(shieldUI, "ShieldUI should exist after reset");
            Assert.IsTrue(shieldUI.enabled, "ShieldUI component should be enabled after reset");

            LogDiagnostic($"ShieldUI after reset - enabled: {shieldUI.enabled}, active: {shieldUI.gameObject.activeInHierarchy}");

            // Fill tracking the post-damage shield fraction proves ShieldUI re-subscribed across death→reset, not merely that dispatch didn't throw.
            var ring = shieldUI.GetComponent<Image>();
            Assert.IsNotNull(ring, "ShieldUI requires an Image to render its fill");
            var maxShield = testShip.Damage.Shield.MaxValue;

            var shieldBefore = testShip.Damage.Shield.CurrentValue;
            testShip.Damage.TakeDamage(new Damage.DamageInfo(
                10f, Damage.DamageKind.Laser, enemyShip.Id, 1f, Vector3.zero, Vector3.zero));
            yield return null;

            var shieldAfter = testShip.Damage.Shield.CurrentValue;
            Assert.Less(shieldAfter, shieldBefore,
                "Shield should decrease after taking damage post-reset");
            Assert.AreEqual(shieldAfter / maxShield, ring.fillAmount, 0.01f,
                "ShieldUI fill should track the current shield fraction after reset (event re-subscribed)");
        }

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

            TestDamage.Kill(testShip, enemyShip);
            yield return null;

            testShip.ResetShip();
            yield return null;

            lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);
            Assert.IsNotNull(lockOnIndicator, "LockOnIndicator should exist after reset");
            Assert.IsTrue(lockOnIndicator.enabled, "LockOnIndicator component should be enabled after reset");

            LogDiagnostic($"LockOnIndicator after reset - enabled: {lockOnIndicator.enabled}, active: {lockOnIndicator.gameObject.activeInHierarchy}");

            var targetable = testShip as ITargetable;
            Assert.IsNotNull(targetable, "Ship should implement ITargetable");

            // Alpha tracking the lock progress/release events proves LockOnIndicator re-subscribed across death→reset, not merely that dispatch didn't throw.
            var canvasGroup = lockOnIndicator.GetComponent<CanvasGroup>();
            Assert.IsNotNull(canvasGroup, "LockOnIndicator requires a CanvasGroup");

            targetable.Lock.RaiseProgress(0.5f);
            yield return null;
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f,
                "LockOnIndicator should show (alpha 1) on lock progress after reset");

            targetable.Lock.RaiseReleased();
            yield return null;
            Assert.AreEqual(0f, canvasGroup.alpha, 0.0001f,
                "LockOnIndicator should hide (alpha 0) on lock release after reset");
        }

        [UnityTest]
        public IEnumerator MultipleDeathResetCycles_ChildComponentsRemainStable()
        {
            yield return null;

            const int numCycles = 3;

            for (int cycle = 0; cycle < numCycles; cycle++)
            {
                LogDiagnostic($"=== Cycle {cycle + 1}/{numCycles} ===");

                var weaponsController = combatShip.Weapons;
                var shieldUI = testShip.GetComponentInChildren<ShieldUI>(includeInactive: true);
                var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);

                TestDamage.Kill(testShip, enemyShip);
                yield return null;

                Assert.IsFalse(testShip.gameObject.activeSelf,
                    $"Cycle {cycle}: Ship should be inactive after death");

                testShip.ResetShip();
                yield return null;

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
