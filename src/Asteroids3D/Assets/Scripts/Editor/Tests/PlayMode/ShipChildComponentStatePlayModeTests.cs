using System.Collections;
using Combat.Targeting;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    [Category("Ships")]
    public class ShipChildComponentStatePlayModeTests : PlayModeWorldFixture
    {
        private Ship testShip;
        private Ship combatShip;
        private Ship enemyShip;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            var shipPrefab = TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab"); // Vanguard has UI components
            var commanderPrefab = TestAssets.LoadTestPilotMpc();

            Assert.IsNotNull(shipPrefab, "Vanguard prefab failed to load");
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

            weaponsController.Arm(Projectiles).Fire(Ships.Command.WeaponSlot.Primary,
                new Combat.Weapons.WeaponCommand { pressed = true, held = true });
            yield return new WaitForFixedUpdate();

            Assert.Greater(fireCount, 0,
                "Primary weapon should fire successfully after ship reset. " +
                $"CanFire: {weaponsController.Primary.CanFire()}, " +
                $"GameObject active: {weaponsController.Primary.gameObject.activeInHierarchy}");
        }

        [UnityTest]
        public IEnumerator AfterShipReset_LockOnIndicatorRespondsToLockEvents()
        {
            yield return null;

            var lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);
            if (lockOnIndicator == null)
            {
                Assert.Ignore("LockOnIndicator not present on Vanguard prefab variant");
                yield break;
            }

            TestDamage.Kill(testShip, enemyShip);
            yield return null;

            testShip.ResetShip();
            yield return null;

            lockOnIndicator = testShip.GetComponentInChildren<LockOnIndicator>(includeInactive: true);
            Assert.IsNotNull(lockOnIndicator, "LockOnIndicator should exist after reset");
            Assert.IsTrue(lockOnIndicator.enabled, "LockOnIndicator component should be enabled after reset");

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
                var weaponsController = combatShip.Weapons;
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

                if (lockOnIndicator != null)
                {
                    Assert.IsTrue(lockOnIndicator.gameObject.activeInHierarchy,
                        $"Cycle {cycle}: LockOnIndicator should be active");
                }
            }
        }
    }
}
