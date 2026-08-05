using Damage;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for DamageController's routing / reset / regen logic. These were previously
    /// slow PlayMode tests (ShipRespawnDamage) that spun up a full ship through the factory. The
    /// routing is Time-free and configured through the production <see cref="ResolvedShipStats"/> path
    /// (<see cref="DamageController.PopulateSettings"/>), so it runs here without play mode, physics,
    /// or a prefab. Behaviour that genuinely needs a real ship — OnDeath victim/killing-blow ids
    /// from an enemy ship — and presentation (hull smoke) stay in PlayMode.
    /// </summary>
    [Category("Damage")]
    public class DamageControllerEditModeTests
    {
        private GameObject _go;

        private DamageController NewDamage(float maxHealth = 100f, float maxShield = 50f,
                                           float regenRate = 10f, float regenDelay = 0.5f)
        {
            _go = new GameObject("DamageTest");
            var dc = _go.AddComponent<DamageController>();
            var stats = new ResolvedShipStats
            {
                maxHealth = maxHealth,
                maxShield = maxShield,
                shieldRegenRate = regenRate,
                shieldRegenDelay = regenDelay,
            };
            dc.PopulateSettings(stats); // production init path — also runs without Awake
            return dc;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
        }

        private static DamageInfo Hit(float amount,
            DamageKind kind = DamageKind.Laser, ShipId attackerId = default) =>
            new(amount, kind, attackerId, 0f, Vector3.zero, Vector3.zero);

        private static void Damage(DamageController dc, float amount) =>
            dc.TakeDamage(Hit(amount));

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
        public void TakeDamage_BleedThrough_RemainderOverShieldHitsHealth()
        {
            var dc = NewDamage(maxHealth: 100f, maxShield: 50f);
            Damage(dc, 35f);         // shield 50 -> 15
            Damage(dc, 15f + 30f);   // exceeds remaining shield by 30

            Assert.AreEqual(0f, dc.Shield.CurrentValue, 0.001f, "Shield fully depleted");
            Assert.AreEqual(70f, dc.Health.CurrentValue, 0.001f,
                "Remainder over the shield bleeds into hull (council §C3)");
        }

        [Test]
        public void TakeDamage_BleedThrough_SingleHitCanKillThroughShield()
        {
            var dc = NewDamage(maxHealth: 100f, maxShield: 50f);
            var eventDamage = 0f;
            dc.OnDamaged += hit => eventDamage = hit.Amount;
            var died = false;
            dc.OnDeath += (_, _) => died = true;

            Damage(dc, 200f); // 50 shield + 100 hull absorbed; 50 lost past the hull floor

            Assert.AreEqual(0f, dc.Shield.CurrentValue, 0.001f);
            Assert.AreEqual(0f, dc.Health.CurrentValue, 0.001f);
            Assert.IsTrue(died, "A single hit exceeding shield + hull kills");
            Assert.AreEqual(150f, eventDamage, 0.001f,
                "OnDamaged reports total absorbed, capped at shield + hull");
        }

        [Test]
        public void HullOnlyDamage_DoesNotPostponeShieldRegen()
        {
            var dc = NewDamage(maxHealth: 100f, maxShield: 50f, regenRate: 10f, regenDelay: 0.5f);
            Damage(dc, 50f);        // shield -> 0 exactly; delay clock starts
            dc.Shield.Update(0.2f); // inside the delay window, shield still 0

            Damage(dc, 10f);        // hull-only: shield absorbs nothing
            Assert.AreEqual(90f, dc.Health.CurrentValue, 0.001f, "Hull takes the full hit");

            dc.Shield.Update(0.4f); // clock 0.6 > 0.5: regen due from the FIRST hit's delay
            Assert.Greater(dc.Shield.CurrentValue, 0f,
                "A hit the shield absorbed none of must not reset the regen delay");
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
        public void OnDeath_CarriesTheKillingBlow_AttackerAndKind()
        {
            var dc = NewDamage(maxHealth: 100f, maxShield: 50f);
            var attackerId = new ShipId(7);
            DamageInfo killingBlow = default;
            dc.OnDeath += (_, blow) => killingBlow = blow;

            dc.TakeDamage(Hit(500f, DamageKind.Collision, attackerId));

            Assert.AreEqual(attackerId, killingBlow.AttackerId,
                "The killing blow must carry the producer's attacker id");
            Assert.AreEqual(DamageKind.Collision, killingBlow.Kind,
                "The killing blow must carry the producer's damage kind");
            Assert.AreEqual(150f, killingBlow.Amount, 0.001f,
                "The killing blow's amount is the applied damage, capped at shield + hull");
        }

        [Test]
        public void OnDeath_FiresOncePerLife_ResetRearms()
        {
            var dc = NewDamage(maxHealth: 100f, maxShield: 50f);
            var deaths = 0;
            dc.OnDeath += (_, _) => deaths++;

            Damage(dc, 500f);
            Damage(dc, 500f); // second lethal hit in the same life must not re-fire
            Assert.AreEqual(1, deaths, "OnDeath fires once per life");

            dc.ResetDamageState();
            Damage(dc, 500f);
            Assert.AreEqual(2, deaths, "ResetDamageState re-arms the death latch");
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
