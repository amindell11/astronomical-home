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
    /// PlayMode residue for ship death/respawn that genuinely needs a real ship or the real prefab:
    /// ShipId attribution on damage/death, and hull-smoke presentation across respawn.
    ///
    /// The pure damage-routing / reset / regen / invulnerability logic previously duplicated here now
    /// lives fast in <c>Tests.EditMode/DamageControllerEditModeTests</c> (constructed via the production
    /// <see cref="DamageController.PopulateSettings"/> path — no prefab, no play mode).
    /// </summary>
    [Category("Damage")]
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
            playerShip = ShipTestFactory.CreateDefaultShip(team: 0);
            playerDamage = playerShip.Damage;

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
        /// LastAttackerId is set from a real attacking ship and cleared after ResetShip(). The
        /// clear-in-isolation path is also covered in EditMode; this exercises the set-from-enemy-ship
        /// round-trip, which needs real ships and their ids.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterReset_LastAttackerIdIsCleared()
        {
            yield return null;

            playerDamage.TakeDamage(10f, 1f, Vector3.zero, Vector3.zero, enemyShip.gameObject);
            yield return null;

            Assert.IsTrue(playerDamage.LastAttackerId.IsValid,
                "LastAttackerId should be set after taking damage");
            Assert.AreEqual(enemyShip.Id, playerDamage.LastAttackerId,
                "LastAttackerId should match the enemy ship id");

            playerShip.ResetShip();
            yield return null;

            Assert.AreEqual(ShipId.Invalid, playerDamage.LastAttackerId,
                "LastAttackerId should be ShipId.Invalid after ResetShip()");
        }

        /// <summary>Death event should publish ShipId payloads (victim + killer) — needs real ships.</summary>
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
        /// Board-bug repro around respawn visuals: hull smoke should be hidden after respawn and only
        /// re-appear once health drops below 50% again. Presentation — needs the real Ship_1 prefab.
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

                // Deplete shield first, then lower health below the smoke threshold.
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

                Assert.IsFalse(smokeObject.activeSelf,
                    "Smoke should be hidden immediately after respawn and remain off until health drops again");
            }
            finally
            {
                ShipTestFactory.DestroyShip(smokeShip);
            }
        }

        private static GameObject GetSmokeObject(Ship ship)
        {
            var hull = ship.GetComponentInChildren<HullVisuals>(true);
            if (!hull) return null;

            var smokeField = typeof(HullVisuals).GetField("smoke", BindingFlags.Instance | BindingFlags.NonPublic);
            if (smokeField == null) return null;

            var smoke = smokeField.GetValue(hull) as ParticleSystem;
            return smoke ? smoke.gameObject : null;
        }
    }
}
