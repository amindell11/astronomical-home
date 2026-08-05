using System.Collections;
using Damage;
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

        private static DamageInfo Hit(float amount, Ship attacker) =>
            new(amount, DamageKind.Laser, attacker ? attacker.Id : ShipId.Invalid,
                1f, Vector3.zero, Vector3.zero);

        /// <summary>Death latch re-arms across ResetShip — a second life's death fires OnDeath again.</summary>
        [UnityTest]
        public IEnumerator AfterReset_DeathLatchRearms_OnDeathFiresAgain()
        {
            yield return null;

            var deaths = 0;
            void HandleDeath(ShipId _victim, DamageInfo _blow) => deaths++;
            playerDamage.OnDeath += HandleDeath;

            TestDamage.Kill(playerShip, enemyShip);
            yield return null;
            Assert.AreEqual(1, deaths, "First life should fire OnDeath once");

            playerShip.ResetShip();
            yield return null;

            TestDamage.Kill(playerShip, enemyShip);
            yield return null;

            playerDamage.OnDeath -= HandleDeath;
            Assert.AreEqual(2, deaths, "OnDeath should fire again after ResetShip re-arms the latch");
        }

        /// <summary>Death event should publish the victim id and the killing blow's attacker — needs real ships.</summary>
        [UnityTest]
        public IEnumerator OnDeath_PublishesVictimAndKillingBlow()
        {
            yield return null;

            ShipId victimId = ShipId.Invalid;
            DamageInfo killingBlow = default;
            var eventRaised = false;

            void HandleDeath(ShipId victim, DamageInfo blow)
            {
                eventRaised = true;
                victimId = victim;
                killingBlow = blow;
            }

            playerDamage.OnDeath += HandleDeath;

            var maxShield = playerDamage.Shield.MaxValue;
            var maxHealth = playerDamage.Health.MaxValue;

            playerDamage.TakeDamage(Hit(maxShield + 100f, enemyShip));
            yield return null;
            playerDamage.TakeDamage(Hit(maxHealth + 100f, enemyShip));
            yield return null;

            playerDamage.OnDeath -= HandleDeath;

            Assert.IsTrue(eventRaised, "OnDeath should be raised when ship health reaches zero");
            Assert.AreEqual(playerShip.Id, victimId, "OnDeath victim id should match the damaged ship id");
            Assert.AreEqual(enemyShip.Id, killingBlow.AttackerId,
                "The killing blow's attacker should be the attacking ship id");
            Assert.AreEqual(DamageKind.Laser, killingBlow.Kind,
                "The killing blow should preserve the producer's damage kind");
        }

    }
}
