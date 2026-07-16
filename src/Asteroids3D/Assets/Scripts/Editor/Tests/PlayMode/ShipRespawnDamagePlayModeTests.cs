using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>ShipId attribution on damage/death — the residue that needs real ships; pure damage-routing/reset/regen logic lives in Tests.EditMode/DamageControllerEditModeTests, hull-smoke presentation in ShipPresentationPlayModeTests.</summary>
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
            playerShip = ShipTestFactory.CreateDefaultShip(Projectiles, team: 0);
            playerDamage = playerShip.Damage;

            enemyShip = ShipTestFactory.CreateDefaultShipAt(
                new Vector3(10, 0, 0),
                Quaternion.identity,
                Projectiles,
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

        /// <summary>LastAttackerId set from a real attacking ship and cleared by ResetShip — the round-trip that needs real ships and ids.</summary>
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

    }
}
