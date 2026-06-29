using System.Collections;
using Combat.Conditions;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Ship-level stress test for reproducing board bug "overheat not working right".
    /// Tests realistic weapon/controller flow with repeated fire-cooldown cycles.
    /// </summary>
    [Category("Regression")]
    [Category("Weapons")]
    public class HeatOverheatStressPlayModeTests : PlayModeWorldFixture
    {
        private sealed class DisableProbe : MonoBehaviour
        {
            public int disableCount;
            public bool parentInactiveAtDisable;

            private void OnDisable()
            {
                disableCount++;
                parentInactiveAtDisable = transform.parent && !transform.parent.gameObject.activeInHierarchy;
            }
        }

        private Ship ship;
        private CombatShip combatShip;
        private Commander commanderPrefab;
        private Heat primaryHeat;
        private DisableProbe primaryWeaponDisableProbe;

        private sealed class ContinuousFireCommander : Commander
        {
            public bool enablePrimaryFire = true;
            private IWeapons weapons;

            public override void Initialize(in ShipControl control)
            {
                weapons = control.WeaponActuator;
            }

            private void FixedUpdate()
            {
                weapons?.Fire(WeaponSlot.Primary, new WeaponCommand { fire = enablePrimaryFire });
            }
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            var settings = TestAssets.LoadDefaultShipSettings();
            var shipPrefab = TestAssets.LoadShip2Prefab();

            Assert.IsNotNull(settings, "Default ship settings asset failed to load");
            Assert.IsNotNull(shipPrefab, "Ship_2 prefab failed to load");

            var commanderGo = new GameObject("ContinuousFireCommanderPrefab");
            commanderPrefab = commanderGo.AddComponent<ContinuousFireCommander>();

            ship = Factory.CreateShip(shipPrefab, commanderPrefab, settings, team: 0,
                                      position: Vector3.zero, rotation: Quaternion.identity);
            combatShip = ship as CombatShip;

            Assert.IsNotNull(ship, "Ship failed to instantiate");
            Assert.IsNotNull(combatShip, "Ship must be a CombatShip");
            Assert.IsNotNull(combatShip.Weapons, "WeaponsController missing on ship");
            Assert.IsNotNull(combatShip.Weapons.Primary, "Primary weapon not instantiated");

            primaryHeat = combatShip.Weapons.Primary.GetComponent<Heat>();
            Assert.IsNotNull(primaryHeat, "Heat component missing on primary weapon");
            primaryWeaponDisableProbe = combatShip.Weapons.Primary.gameObject.AddComponent<DisableProbe>();
#else
            Assert.Ignore("HeatOverheatStressPlayModeTests requires Unity Editor assets.");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            if (commanderPrefab != null)
                DestroyTestObject(commanderPrefab.gameObject);

            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        private ContinuousFireCommander GetCommander()
        {
            return ship.Commander as ContinuousFireCommander;
        }

        [Test]
        public void PrimaryWeapon_HasReasonableHeatSettings()
        {
            Assert.LessOrEqual(primaryHeat.MaxHeat, 200f, "MaxHeat seems too high");
            
            // Overheat penalty should be reasonable for gameplay (not minutes)
            var overheatPenalty = (float)typeof(Heat)
                .GetField("overheatPenaltyTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(primaryHeat);
            
            Debug.Log($"Primary weapon heat settings: MaxHeat={primaryHeat.MaxHeat:F1}, OverheatPenalty={overheatPenalty:F2}s");
            Assert.IsNotNull(overheatPenalty, "Could not read overheatPenaltyTime");
            Assert.LessOrEqual(overheatPenalty, 5f, $"OverheatPenaltyTime ({overheatPenalty}s) too long for stress test timeout");
        }

        [UnityTest]
        public IEnumerator PrimaryWeaponGameObject_RemainsActive_DuringExtendedContinuousFire()
        {
            var commander = GetCommander();
            Assert.IsNotNull(commander, "Commander cast failed");
            Assert.IsNotNull(primaryWeaponDisableProbe, "Disable probe missing");

            commander.enablePrimaryFire = true;
            var elapsed = 0f;
            const float duration = 12f;

            while (elapsed < duration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                Assert.IsTrue(combatShip.Weapons.Primary.gameObject.activeSelf,
                    $"Primary weapon gameobject became inactive at t={elapsed:F2}s");
                Assert.AreEqual(0, primaryWeaponDisableProbe.disableCount,
                    $"Primary weapon received OnDisable during continuous fire at t={elapsed:F2}s");
            }
        }

        /// <summary>
        /// Stress test: Repeatedly fire primary weapon through ship command flow
        /// until overheat, then wait for cooldown. Verify weapon doesn't get stuck
        /// in overheated state across multiple cycles.
        /// </summary>
        [UnityTest]
        public IEnumerator RepeatedFireCooldownCycles_ThroughShipCommandFlow_DoesNotStuckOverheated()
        {
            var commander = GetCommander();
            Assert.IsNotNull(commander, "Commander cast failed");

            const int targetCycles = 6; // Realistic for ~15s test (observed ~2.5s per cycle)
            const float maxCycleTime = 4.0f; // generous per-cycle timeout
            var cyclesCompleted = 0;
            var totalElapsed = 0f;
            const float absoluteTimeout = 15f; // keep total test time practical

            for (var cycle = 0; cycle < targetCycles && totalElapsed < absoluteTimeout; cycle++)
            {
                // Phase 1: Fire until overheat
                commander.enablePrimaryFire = true;
                var fireElapsed = 0f;
                var overheatDetected = false;

                while (!primaryHeat.Overheated && fireElapsed < maxCycleTime)
                {
                    yield return new WaitForFixedUpdate();
                    fireElapsed += Time.fixedDeltaTime;
                }

                overheatDetected = primaryHeat.Overheated;
                Assert.IsTrue(overheatDetected, 
                    $"Cycle {cycle}: Expected primary weapon to overheat within {maxCycleTime}s");

                // Phase 2: Wait for cooldown (keep fire enabled to test realistic flow)
                var cooldownElapsed = 0f;
                while (primaryHeat.Overheated && cooldownElapsed < maxCycleTime)
                {
                    yield return new WaitForFixedUpdate();
                    cooldownElapsed += Time.fixedDeltaTime;
                }

                Assert.IsFalse(primaryHeat.Overheated,
                    $"Cycle {cycle}: Weapon stuck overheated after {cooldownElapsed:F2}s. " +
                    "Possible heat debt or cooldown logic bug.");

                // Phase 3: Verify weapon can fire again (at least one successful shot)
                var refireElapsed = 0f;
                var refireDetected = false;
                var fireCountBefore = 0;
                combatShip.Weapons.Primary.OnFire += () => fireCountBefore++;

                while (!refireDetected && refireElapsed < 1.0f)
                {
                    yield return new WaitForFixedUpdate();
                    refireElapsed += Time.fixedDeltaTime;
                    if (fireCountBefore > 0)
                        refireDetected = true;
                }

                Assert.IsTrue(refireDetected,
                    $"Cycle {cycle}: Weapon did not fire after cooldown. Possible CanFire() stuck false.");

                cyclesCompleted++;
                totalElapsed += fireElapsed + cooldownElapsed + refireElapsed;
            }

            Assert.GreaterOrEqual(cyclesCompleted, targetCycles,
                $"Only {cyclesCompleted}/{targetCycles} cycles completed in {totalElapsed:F2}s");
        }

        /// <summary>
        /// Stress test: Aggressively spam fire command (even while overheated)
        /// to simulate worst-case controller behavior. Verify weapon eventually
        /// recovers and doesn't accumulate unbounded heat debt.
        /// </summary>
        [UnityTest]
        public IEnumerator AggressiveFireSpam_WhileOverheated_EventuallyRecovers()
        {
            var commander = GetCommander();
            commander.enablePrimaryFire = true;

            // Phase 1: Fire until overheat
            var elapsed = 0f;
            while (!primaryHeat.Overheated && elapsed < 2.0f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            Assert.IsTrue(primaryHeat.Overheated, "Expected weapon to overheat");

            // Phase 2: Continue aggressive fire commands while overheated
            // (simulating player/AI spam or controller bug)
            const float spamDuration = 1.0f;
            var spamElapsed = 0f;
            while (spamElapsed < spamDuration)
            {
                yield return new WaitForFixedUpdate();
                spamElapsed += Time.fixedDeltaTime;
            }

            // Verify heat is clamped (no unbounded debt)
            Assert.LessOrEqual(primaryHeat.CurrentHeat, primaryHeat.MaxHeat + 0.01f,
                "Heat exceeded max during spam (possible unbounded heat debt bug)");

            // Phase 3: Stop firing and verify recovery
            commander.enablePrimaryFire = false;
            var recoveryElapsed = 0f;
            const float recoveryTimeout = 3.0f;

            while (primaryHeat.Overheated && recoveryElapsed < recoveryTimeout)
            {
                yield return new WaitForFixedUpdate();
                recoveryElapsed += Time.fixedDeltaTime;
            }

            Assert.IsFalse(primaryHeat.Overheated,
                $"Weapon failed to recover after {recoveryTimeout}s. " +
                "Possible permanent stuck state from fire spam.");

            // Phase 4: Verify weapon can fire again
            commander.enablePrimaryFire = true;
            var fireCount = 0;
            combatShip.Weapons.Primary.OnFire += () => fireCount++;

            var refireElapsed = 0f;
            while (fireCount == 0 && refireElapsed < 1.0f)
            {
                yield return new WaitForFixedUpdate();
                refireElapsed += Time.fixedDeltaTime;
            }

            Assert.Greater(fireCount, 0,
                "Weapon did not fire after recovery from spam. Possible CanFire() stuck false.");
        }
    }
}
