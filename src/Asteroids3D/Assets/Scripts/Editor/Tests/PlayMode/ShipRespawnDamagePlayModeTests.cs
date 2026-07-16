using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode residue for ship death/respawn that genuinely needs a real ship or the real prefab:
    /// ShipId attribution on damage/death — needs real ships (hull-smoke presentation moved to
    /// ShipPresentationPlayModeTests with the visual rig attached).
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

    }
}
