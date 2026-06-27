using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using Ships.Visuals;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for Ship respawn behavior, specifically health/shield management.
    /// 
    /// BUG REPRODUCTION: "Health / Shield issues when respawning"
    /// Source: D:/amind/Documents/Obsidian Vault/Astronomical/Engineering/Project Board.md
    /// 
    /// Expected behavior (per design):
    /// - Newly created ships start with full Health and full Shield
    /// - Damage is absorbed by Shield first; Health only decreases after Shield reaches zero
    /// - After lethal damage + Ship.ResetShip(), Health and Shield are both restored to max
    /// - Shield regen should respect configured delay and not violate max bounds
    /// - After respawn, attacker state should be cleared
    /// - Multiple respawn cycles should not cause drift in health/shield values
    /// 
    /// Candidate code locations to investigate after test failures:
    /// - Ship.ResetShip()
    /// - DamageController.ResetDamageState()
    /// - DamageController.TakeDamage() (damage routing logic)
    /// - RegenResource.Reset() / RegenResource.Configure()
    /// </summary>
    [Category("Integration")]
    [Category("Slow")]
    public class ShipRespawnDamagePlayModeTests : PlayModeWorldFixture
    {
        private Ship playerShip;
        private Ship enemyShip;
        private DamageController playerDamage;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            // Create player ship for testing
            playerShip = ShipTestFactory.CreateDefaultShip(team: 0);
            playerDamage = playerShip.Damage;

            // Create enemy ship to act as attacker
            enemyShip = ShipTestFactory.CreateDefaultShipAt(
                new Vector3(10, 0, 0),
                Quaternion.identity,
                team: 1);

            Assert.IsNotNull(playerShip, "Player ship failed to instantiate");
            Assert.IsNotNull(playerDamage, "DamageController not found on player ship");
            Assert.IsNotNull(enemyShip, "Enemy ship failed to instantiate");
#else
            Assert.Ignore("ShipRespawnDamagePlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(playerShip);
            ShipTestFactory.DestroyShip(enemyShip);
            base.TearDown();
        }

        /// <summary>
        /// STEP 2: Baseline characterization test.
        /// Verify that a newly created ship starts at full Health and full Shield.
        /// This is a sanity check for the test harness and default settings.
        /// </summary>
        [UnityTest]
        [Category("Smoke")]
        public IEnumerator NewShip_StartsWithFullHealthAndShield()
        {
            yield return null; // Wait one frame for initialization

            var settings = playerShip.settings;
            Assert.IsNotNull(settings, "Ship settings are null");

            Assert.AreEqual(settings.maxHealth, playerDamage.Health.CurrentValue,
                $"Expected Health to be {settings.maxHealth}, but was {playerDamage.Health.CurrentValue}");

            Assert.AreEqual(settings.maxShield, playerDamage.Shield.CurrentValue,
                $"Expected Shield to be {settings.maxShield}, but was {playerDamage.Shield.CurrentValue}");

            Assert.AreEqual(settings.maxHealth, playerDamage.Health.MaxValue,
                $"Expected max Health to be {settings.maxHealth}, but was {playerDamage.Health.MaxValue}");

            Assert.AreEqual(settings.maxShield, playerDamage.Shield.MaxValue,
                $"Expected max Shield to be {settings.maxShield}, but was {playerDamage.Shield.MaxValue}");
        }

        /// <summary>
        /// STEP 3: Pre-respawn damage routing test.
        /// Incoming damage should be absorbed by Shield first.
        /// Health should only decrease after Shield reaches zero.
        /// </summary>
        [UnityTest]
        public IEnumerator TakeDamage_ShieldAbsorbsFirst_ThenHealth()
        {
            yield return null; // Wait for initialization

            var initialHealth = playerDamage.Health.CurrentValue;
            var initialShield = playerDamage.Shield.CurrentValue;

            // Apply damage less than shield (should only affect shield)
            float smallDamage = initialShield * 0.5f;
            playerDamage.TakeDamage(smallDamage, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

            yield return null;

            Assert.AreEqual(initialHealth, playerDamage.Health.CurrentValue,
                "Health should not decrease when Shield still has value");

            Assert.AreEqual(initialShield - smallDamage, playerDamage.Shield.CurrentValue, 0.01f,
                $"Shield should have absorbed {smallDamage} damage");

            // Apply damage to deplete remaining shield
            float remainingShield = playerDamage.Shield.CurrentValue;
            playerDamage.TakeDamage(remainingShield, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

            yield return null;

            Assert.AreEqual(0f, playerDamage.Shield.CurrentValue, 0.01f,
                "Shield should be depleted");

            Assert.AreEqual(initialHealth, playerDamage.Health.CurrentValue,
                "Health should still be full when shield reaches exactly zero");

            // Apply damage to health (shield is now zero)
            float healthDamage = 20f;
            playerDamage.TakeDamage(healthDamage, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

            yield return null;

            Assert.AreEqual(initialHealth - healthDamage, playerDamage.Health.CurrentValue, 0.01f,
                "Health should decrease when Shield is depleted");
        }

        /// <summary>
        /// STEP 4: Respawn reset test.
        /// After lethal damage + Ship.ResetShip(), Health and Shield should both be restored to max.
        /// Ship should be active again.
        /// Note: Damage does NOT overflow from shield to health per design, so we deplete shield first.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterLethalDamageAndReset_HealthAndShieldAreRestored()
        {
            yield return null;

            var maxHealth = playerDamage.Health.MaxValue;
            var maxShield = playerDamage.Shield.MaxValue;

            // Deal lethal damage: first exhaust shield, then health
            // Damage does NOT overflow from shield to health (per design)
            playerDamage.TakeDamage(maxShield + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

            yield return null;

            // Verify shield is depleted
            Assert.AreEqual(0f, playerDamage.Shield.CurrentValue, 0.01f,
                "Shield should be depleted after first damage");

            // Deal damage to health
            playerDamage.TakeDamage(maxHealth + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

            yield return null;

            // Verify ship is dead
            Assert.IsFalse(playerShip.gameObject.activeSelf,
                "Ship should be inactive after lethal damage");

            Assert.LessOrEqual(playerDamage.Health.CurrentValue, 0f,
                "Health should be zero or negative after lethal damage");

            // Reset the ship (simulating respawn)
            playerShip.ResetShip();

            yield return null;

            // BUG REPRODUCTION: Verify health and shield are restored
            Assert.IsTrue(playerShip.gameObject.activeSelf,
                "Ship should be active after ResetShip()");

            Assert.AreEqual(maxHealth, playerDamage.Health.CurrentValue, 0.01f,
                "Health should be restored to max after ResetShip()");

            Assert.AreEqual(maxShield, playerDamage.Shield.CurrentValue, 0.01f,
                "Shield should be restored to max after ResetShip()");

            Assert.AreEqual(1f, playerDamage.Health.Pct, 0.01f,
                "Health percentage should be 100% after reset");

            Assert.AreEqual(1f, playerDamage.Shield.Pct, 0.01f,
                "Shield percentage should be 100% after reset");
        }

        /// <summary>
        /// STEP 5: Regen-delay contract test around respawn.
        /// Shield should not violate max bounds and should follow configured regen timing rules.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterReset_ShieldDoesNotExceedMaxDuringRegen()
        {
            yield return null;

            var maxShield = playerDamage.Shield.MaxValue;
            var settings = playerShip.settings;

            // Reset ship
            playerShip.ResetShip();
            yield return null;

            // Shield should be at max immediately after reset
            Assert.AreEqual(maxShield, playerDamage.Shield.CurrentValue, 0.01f,
                "Shield should be at max immediately after reset");

            // Wait for several regen cycles (more than regen delay)
            float waitTime = settings.shieldRegenDelay + 2f;
            float elapsed = 0f;
            while (elapsed < waitTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                // Shield should never exceed max
                Assert.LessOrEqual(playerDamage.Shield.CurrentValue, maxShield,
                    $"Shield exceeded max during regen at t={elapsed:F2}s. Current: {playerDamage.Shield.CurrentValue}, Max: {maxShield}");
            }

            // Shield should still be at max (since it started at max)
            Assert.AreEqual(maxShield, playerDamage.Shield.CurrentValue, 0.01f,
                "Shield should remain at max when already full");
        }

        /// <summary>
        /// STEP 6: Attacker-state reset test.
        /// Verify DamageController.LastAttackerId is cleared after ResetShip().
        /// This catches stale state across deaths.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterReset_LastAttackerIdIsCleared()
        {
            yield return null;

            // Deal damage to establish an attacker
            playerDamage.TakeDamage(10f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

            yield return null;

            Assert.IsTrue(playerDamage.LastAttackerId.IsValid,
                "LastAttackerId should be set after taking damage");

            Assert.AreEqual(enemyShip.Id, playerDamage.LastAttackerId,
                "LastAttackerId should match the enemy ship id");

            // Reset the ship
            playerShip.ResetShip();

            yield return null;

            // BUG REPRODUCTION: LastAttackerId should be cleared
            Assert.AreEqual(ShipId.Invalid, playerDamage.LastAttackerId,
                "LastAttackerId should be ShipId.Invalid after ResetShip()");
        }

        /// <summary>
        /// STEP 6 (continued): Death event should publish ShipId payloads.
        /// </summary>
        [UnityTest]
        public IEnumerator OnDeath_PublishesVictimAndKillerShipIds()
        {
            yield return null;

            ShipId victimId = ShipId.Invalid;
            ShipId killerId = ShipId.Invalid;
            var eventRaised = false;

            void HandleDeath(ShipId victim, ShipId killer)
            {
                eventRaised = true;
                victimId = victim;
                killerId = killer;
            }

            playerDamage.OnDeath += HandleDeath;

            var maxShield = playerDamage.Shield.MaxValue;
            var maxHealth = playerDamage.Health.MaxValue;

            playerDamage.TakeDamage(maxShield + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;
            playerDamage.TakeDamage(maxHealth + 100f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;

            playerDamage.OnDeath -= HandleDeath;

            Assert.IsTrue(eventRaised, "OnDeath should be raised when ship health reaches zero");
            Assert.AreEqual(playerShip.Id, victimId, "OnDeath victim id should match the damaged ship id");
            Assert.AreEqual(enemyShip.Id, killerId, "OnDeath killer id should match the attacking ship id");
        }

        /// <summary>
        /// STEP 6 (continued): Non-ship attackers should not set LastAttackerId.
        /// </summary>
        [UnityTest]
        public IEnumerator NonShipAttacker_DoesNotSetLastAttackerId()
        {
            yield return null;

            var asteroid = new GameObject("TestNonShipAttacker");
            try
            {
                playerDamage.TakeDamage(10f, 1f, Vector3.zero, Vector3.zero, asteroid);
                yield return null;

                Assert.AreEqual(ShipId.Invalid, playerDamage.LastAttackerId,
                    "LastAttackerId should remain invalid when attacker has no Ship component");
            }
            finally
            {
                Object.Destroy(asteroid);
            }
        }

        /// <summary>
        /// STEP 6 (continued): Invulnerability state reset test.
        /// Verify invulnerability is cleared after ResetShip().
        /// </summary>
        [UnityTest]
        public IEnumerator AfterReset_InvulnerabilityIsCleared()
        {
            yield return null;

            // Set invulnerability
            playerDamage.SetInvulnerability(10f);

            yield return null;

            Assert.IsTrue(playerDamage.IsInvulnerable,
                "Ship should be invulnerable after SetInvulnerability");

            // Reset the ship
            playerShip.ResetShip();

            yield return null;

            Assert.IsFalse(playerDamage.IsInvulnerable,
                "Invulnerability should be cleared after ResetShip()");
        }

        /// <summary>
        /// STEP 7: Respawn-cycle stability test.
        /// Repeat damage->death->reset across multiple cycles and verify no drift in max/current values.
        /// Note: Damage does NOT overflow from shield to health per design, so we deal separate shield then health damage.
        /// </summary>
        [UnityTest]
        public IEnumerator MultipleRespawnCycles_NoHealthOrShieldDrift()
        {
            yield return null;

            var expectedMaxHealth = playerDamage.Health.MaxValue;
            var expectedMaxShield = playerDamage.Shield.MaxValue;

            const int numCycles = 5;

            for (int cycle = 0; cycle < numCycles; cycle++)
            {
                // First deplete shield (damage does NOT overflow per design)
                playerDamage.TakeDamage(expectedMaxShield + 50f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

                yield return null;

                // Then deal lethal health damage
                playerDamage.TakeDamage(expectedMaxHealth + 50f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

                yield return null;

                // Verify ship is dead
                Assert.IsFalse(playerShip.gameObject.activeSelf,
                    $"Cycle {cycle}: Ship should be inactive after lethal damage");

                // Reset ship
                playerShip.ResetShip();

                yield return null;

                // BUG REPRODUCTION: Check for drift
                Assert.AreEqual(expectedMaxHealth, playerDamage.Health.MaxValue, 0.01f,
                    $"Cycle {cycle}: Max Health should not drift. Expected {expectedMaxHealth}, got {playerDamage.Health.MaxValue}");

                Assert.AreEqual(expectedMaxShield, playerDamage.Shield.MaxValue, 0.01f,
                    $"Cycle {cycle}: Max Shield should not drift. Expected {expectedMaxShield}, got {playerDamage.Shield.MaxValue}");

                Assert.AreEqual(expectedMaxHealth, playerDamage.Health.CurrentValue, 0.01f,
                    $"Cycle {cycle}: Current Health should be restored to max. Expected {expectedMaxHealth}, got {playerDamage.Health.CurrentValue}");

                Assert.AreEqual(expectedMaxShield, playerDamage.Shield.CurrentValue, 0.01f,
                    $"Cycle {cycle}: Current Shield should be restored to max. Expected {expectedMaxShield}, got {playerDamage.Shield.CurrentValue}");

                Assert.IsTrue(playerShip.gameObject.activeSelf,
                    $"Cycle {cycle}: Ship should be active after reset");

                // Wait a frame between cycles
                yield return null;
            }
        }

        /// <summary>
        /// STEP 8: Repro test for board bug around respawn visuals.
        /// Smoke should be hidden after respawn and only re-appear once health drops below 50% again.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterReset_SmokeTrailIsHidden_UntilHealthDropsAgain()
        {
            yield return null;

            GameSettings.SetVfxEnabled(true);

            var settings = TestAssets.LoadDefaultShipSettings();
            var ship1Prefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab");
            var commanderPrefab = TestAssets.LoadTestPilotMpc();

            Assert.IsNotNull(settings, "Default ship settings failed to load");
            Assert.IsNotNull(ship1Prefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(commanderPrefab, "TestPilotMPC prefab failed to load");

            Ship smokeShip = null;
            try
            {
                smokeShip = ShipTestFactory.CreateShip(ship1Prefab, commanderPrefab, settings, team: 0);
                Assert.IsNotNull(smokeShip, "Failed to create smoke test ship");

                var smokeDamage = smokeShip.Damage;
                var smokeObject = GetSmokeObject(smokeShip);

                Assert.IsNotNull(smokeDamage, "DamageController missing on smoke test ship");
                Assert.IsNotNull(smokeObject, "Hull smoke ParticleSystem reference was not found");

                yield return null;

                Assert.IsFalse(smokeObject.activeSelf,
                    "Smoke should start hidden at full health");

                // Deplete shield first, then lower health below smoke threshold.
                smokeDamage.TakeDamage(smokeDamage.Shield.CurrentValue + 25f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
                yield return null;

                smokeDamage.TakeDamage(smokeDamage.Health.MaxValue * 0.6f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
                yield return null;

                Assert.Less(smokeDamage.Health.Pct, 0.5f,
                    "Health should be below 50% to trigger smoke");
                Assert.IsTrue(smokeObject.activeSelf,
                    "Smoke should be visible when health drops below 50%");

                // Kill + respawn.
                smokeDamage.TakeDamage(smokeDamage.Health.MaxValue + 25f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
                yield return null;

                Assert.IsFalse(smokeShip.gameObject.activeSelf,
                    "Smoke test ship should be inactive after lethal damage");

                smokeShip.ResetShip();
                yield return null;

                Assert.IsTrue(smokeShip.gameObject.activeSelf,
                    "Smoke test ship should be active after ResetShip()");
                Assert.AreEqual(1f, smokeDamage.Health.Pct, 0.01f,
                    "Health should be fully restored after reset");

                // BUG EXPECTATION: this currently fails when smoke state is stale across disable/enable.
                Assert.IsFalse(smokeObject.activeSelf,
                    "Smoke should be hidden immediately after respawn and remain off until health drops again");
            }
            finally
            {
                ShipTestFactory.DestroyShip(smokeShip);
            }
        }

        /// <summary>
        /// STEP 9: Integration test for damage routing (no overflow).
        /// When shield is partially depleted and damage exceeds remaining shield,
        /// the overflow is absorbed (not applied to health). This is the intended behavior per design:
        /// "If shield is present, incoming damage is fully absorbed by shield and does not spill into hull."
        /// </summary>
        [UnityTest]
        public IEnumerator DamageNoOverflow_ExceedsShield_DoesNotDamageHealth()
        {
            yield return null;

            var initialHealth = playerDamage.Health.CurrentValue;
            var initialShield = playerDamage.Shield.CurrentValue;

            // Partially deplete shield first
            float partialShieldDamage = initialShield * 0.7f;
            playerDamage.TakeDamage(partialShieldDamage, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

            yield return null;

            float remainingShield = playerDamage.Shield.CurrentValue;
            Assert.Greater(remainingShield, 0f, "Shield should have remaining value");

            // Apply damage that exceeds remaining shield
            // Per design, this damage does NOT overflow to health
            float excessDamage = remainingShield + 30f;
            playerDamage.TakeDamage(excessDamage, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);

            yield return null;

            // Shield should be fully depleted (absorbed its remaining value only)
            Assert.AreEqual(0f, playerDamage.Shield.CurrentValue, 0.01f,
                "Shield should be fully depleted");

            // Health should NOT be damaged by the overflow
            Assert.AreEqual(initialHealth, playerDamage.Health.CurrentValue, 0.01f,
                $"Health should NOT be damaged by overflow (per design). Expected {initialHealth}, got {playerDamage.Health.CurrentValue}");
        }

        private static GameObject GetSmokeObject(Ship ship)
        {
            var hull = ship.GetComponentInChildren<Hull>(true);
            if (!hull) return null;

            var smokeField = typeof(Hull).GetField("smoke", BindingFlags.Instance | BindingFlags.NonPublic);
            if (smokeField == null) return null;

            var smoke = smokeField.GetValue(hull) as ParticleSystem;
            return smoke ? smoke.gameObject : null;
        }
    }
}
