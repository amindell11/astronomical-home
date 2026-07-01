using NUnit.Framework;
using Ships;
using Ships.Damage;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for DamageController's routing / reset / regen logic. These were previously
    /// slow PlayMode tests (ShipRespawnDamage) that spun up a full ship through the factory. The
    /// routing is Time-free and configured through the production <see cref="ShipSettings"/> path
    /// (<see cref="DamageController.PopulateSettings"/>), so it runs here without play mode, physics,
    /// or a prefab. Behaviour that genuinely needs a real ship — LastAttackerId from an enemy ship,
    /// OnDeath victim/killer ids — and presentation (hull smoke) stay in PlayMode.
    /// </summary>
    [Category("Damage")]
    public class DamageControllerEditModeTests
    {
        private GameObject _go;
        private ShipSettings _settings;

        private DamageController NewDamage(float maxHealth = 100f, float maxShield = 50f,
                                           float regenRate = 10f, float regenDelay = 0.5f)
        {
            _go = new GameObject("DamageTest");
            var dc = _go.AddComponent<DamageController>();
            _settings = ScriptableObject.CreateInstance<ShipSettings>();
            _settings.maxHealth = maxHealth;
            _settings.maxShield = maxShield;
            _settings.shieldRegenRate = regenRate;
            _settings.shieldRegenDelay = regenDelay;
            dc.PopulateSettings(_settings); // production init path — also runs without Awake
            return dc;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            if (_settings) Object.DestroyImmediate(_settings);
            _go = null;
            _settings = null;
        }

        private static void Damage(DamageController dc, float amount) =>
            dc.TakeDamage(amount, 0f, Vector3.zero, Vector3.zero, null);

        [Test]
        public void NewController_StartsWithFullHealthAndShield()
        {
            var dc = NewDamage(maxHealth: 120f, maxShield: 60f);
            Assert.AreEqual(120f, dc.Health.MaxValue, 0.001f);
            Assert.AreEqual(120f, dc.Health.CurrentValue, 0.001f);
            Assert.AreEqual(60f, dc.Shield.MaxValue, 0.001f);
            Assert.AreEqual(60f, dc.Shield.CurrentValue, 0.001f);
        }

        [Test]
        public void TakeDamage_ShieldAbsorbsFirst_ThenHealth()
        {
            var dc = NewDamage(maxHealth: 100f, maxShield: 50f);

            Damage(dc, 20f); // shield 50 -> 30
            Assert.AreEqual(30f, dc.Shield.CurrentValue, 0.001f);
            Assert.AreEqual(100f, dc.Health.CurrentValue, 0.001f, "Health untouched while shield remains");

            Damage(dc, 30f); // shield 30 -> 0 exactly
            Assert.AreEqual(0f, dc.Shield.CurrentValue, 0.001f);
            Assert.AreEqual(100f, dc.Health.CurrentValue, 0.001f, "Health full when shield hits exactly zero");

            Damage(dc, 25f); // shield depleted -> health 100 -> 75
            Assert.AreEqual(75f, dc.Health.CurrentValue, 0.001f);
        }

        [Test]
        public void TakeDamage_NoOverflow_ExcessOverShieldDoesNotHitHealth()
        {
            var dc = NewDamage(maxShield: 50f);
            Damage(dc, 35f);         // shield 50 -> 15
            Damage(dc, 15f + 30f);   // exceeds remaining shield by 30

            Assert.AreEqual(0f, dc.Shield.CurrentValue, 0.001f, "Shield fully depleted");
            Assert.AreEqual(dc.Health.MaxValue, dc.Health.CurrentValue, 0.001f,
                "Per design, damage exceeding shield is absorbed, not spilled into hull");
        }

        [Test]
        public void ResetDamageState_RestoresHealthAndShield()
        {
            var dc = NewDamage();
            Damage(dc, dc.Shield.MaxValue); // shield -> 0
            Damage(dc, dc.Health.MaxValue); // health -> 0
            Assert.AreEqual(0f, dc.Health.CurrentValue, 0.001f);

            dc.ResetDamageState();
            Assert.AreEqual(dc.Health.MaxValue, dc.Health.CurrentValue, 0.001f);
            Assert.AreEqual(dc.Shield.MaxValue, dc.Shield.CurrentValue, 0.001f);
        }

        [Test]
        public void MultipleResetCycles_NoHealthOrShieldDrift()
        {
            var dc = NewDamage();
            var maxH = dc.Health.MaxValue;
            var maxS = dc.Shield.MaxValue;

            for (var i = 0; i < 5; i++)
            {
                Damage(dc, maxS + 50f);
                Damage(dc, maxH + 50f);
                dc.ResetDamageState();

                Assert.AreEqual(maxH, dc.Health.MaxValue, 0.001f, $"cycle {i}: max health drift");
                Assert.AreEqual(maxS, dc.Shield.MaxValue, 0.001f, $"cycle {i}: max shield drift");
                Assert.AreEqual(maxH, dc.Health.CurrentValue, 0.001f, $"cycle {i}: current health");
                Assert.AreEqual(maxS, dc.Shield.CurrentValue, 0.001f, $"cycle {i}: current shield");
            }
        }

        [Test]
        public void SetInvulnerability_BlocksDamage_ResetClearsIt()
        {
            var dc = NewDamage();
            dc.SetInvulnerability(10f);
            Assert.IsTrue(dc.IsInvulnerable);

            Damage(dc, 20f);
            Assert.AreEqual(dc.Shield.MaxValue, dc.Shield.CurrentValue, 0.001f,
                "An invulnerable controller takes no damage");

            dc.ResetDamageState();
            Assert.IsFalse(dc.IsInvulnerable, "ResetDamageState clears invulnerability");
        }

        [Test]
        public void NonShipAttacker_DoesNotSetLastAttackerId()
        {
            var dc = NewDamage();
            var attacker = new GameObject("NonShipAttacker");
            try
            {
                dc.TakeDamage(10f, 0f, Vector3.zero, Vector3.zero, attacker);
                Assert.AreEqual(ShipId.Invalid, dc.LastAttackerId,
                    "An attacker with no Ship component must not set LastAttackerId");
            }
            finally
            {
                Object.DestroyImmediate(attacker);
            }
        }

        [Test]
        public void Shield_RegenNeverExceedsMax_AndReachesMax()
        {
            var dc = NewDamage(maxShield: 50f, regenRate: 100f, regenDelay: 0.5f);
            Damage(dc, 30f); // shield 50 -> 20
            Assert.AreEqual(20f, dc.Shield.CurrentValue, 0.001f);

            const float dt = 0.05f;
            for (var t = 0f; t < 2f; t += dt)
            {
                dc.Shield.Update(dt);
                Assert.LessOrEqual(dc.Shield.CurrentValue, dc.Shield.MaxValue + 0.001f,
                    $"Shield exceeded max during regen at t={t:F2}");
            }

            Assert.AreEqual(dc.Shield.MaxValue, dc.Shield.CurrentValue, 0.001f,
                "Shield should refill to exactly max given enough time");
        }

        [Test]
        public void Shield_RegenRespectsDelay()
        {
            var dc = NewDamage(maxShield: 50f, regenRate: 10f, regenDelay: 0.5f);
            Damage(dc, 20f); // shield -> 30
            var afterHit = dc.Shield.CurrentValue;

            // Within the delay window: no regen.
            dc.Shield.Update(0.2f);
            dc.Shield.Update(0.2f); // clock 0.4 < 0.5
            Assert.AreEqual(afterHit, dc.Shield.CurrentValue, 0.001f,
                "Shield must not regen before regenDelay elapses");

            // Cross the delay: regen resumes.
            dc.Shield.Update(0.2f); // clock 0.6 > 0.5
            Assert.Greater(dc.Shield.CurrentValue, afterHit, "Shield should regen after the delay");
        }
    }
}
