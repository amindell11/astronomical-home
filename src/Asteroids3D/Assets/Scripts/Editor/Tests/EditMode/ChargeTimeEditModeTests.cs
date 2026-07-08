using Combat.Conditions;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Weapons")]
    public class ChargeTimeEditModeTests
    {
        private GameObject go;
        private ChargeTime charge;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("ChargeTimeTest");
            charge = go.AddComponent<ChargeTime>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Charge_AccumulatesWhileHeld()
        {
            charge.Configure(chargeTime: 1f, minChargeToFire: 0.3f, autoFireAtFull: false);

            var fire = charge.HandleTrigger(held: true, dt: 0.5f);

            Assert.IsFalse(fire, "No auto-fire when autoFireAtFull is off.");
            Assert.AreEqual(0.5f, charge.ChargePct, 0.0001f);
        }

        [Test]
        public void FullCharge_AutoFiresWhileHeld()
        {
            charge.Configure(chargeTime: 1f, minChargeToFire: 0.3f, autoFireAtFull: true);

            Assert.IsFalse(charge.HandleTrigger(held: true, dt: 0.5f));
            Assert.IsTrue(charge.HandleTrigger(held: true, dt: 0.6f), "Reaching full charge while held should fire.");
            Assert.AreEqual(1f, charge.ChargePct, 0.0001f);
            Assert.IsTrue(charge.CanFire());
        }

        [Test]
        public void ReleaseAboveMinimum_Fires_AndFiringConsumesCharge()
        {
            charge.Configure(chargeTime: 1f, minChargeToFire: 0.3f, autoFireAtFull: false);
            charge.HandleTrigger(held: true, dt: 0.5f);

            var fire = charge.HandleTrigger(held: false, dt: 0.02f);

            Assert.IsTrue(fire, "Release at or above the minimum charge fires.");
            Assert.AreEqual(0.5f, charge.ChargePct, 0.0001f, "Charge is spent by ProcessFire, not the release itself.");

            charge.ProcessFire();
            Assert.AreEqual(0f, charge.ChargePct, 0.0001f);
            Assert.IsFalse(charge.CanFire());
        }

        [Test]
        public void ReleaseBelowMinimum_DropsChargeWithoutFiring()
        {
            charge.Configure(chargeTime: 1f, minChargeToFire: 0.3f, autoFireAtFull: false);
            charge.HandleTrigger(held: true, dt: 0.2f);

            var fire = charge.HandleTrigger(held: false, dt: 0.02f);

            Assert.IsFalse(fire);
            Assert.AreEqual(0f, charge.ChargePct, 0.0001f, "An unfired release drains the charge.");
        }

        [Test]
        public void BlockedReleaseFire_DrainsOnTheFollowingStep()
        {
            // Release fires, but if the weapon was blocked (e.g. cooldown) and never called
            // ProcessFire, the loose charge drains on the next idle step.
            charge.Configure(chargeTime: 1f, minChargeToFire: 0.3f, autoFireAtFull: false);
            charge.HandleTrigger(held: true, dt: 0.5f);
            Assert.IsTrue(charge.HandleTrigger(held: false, dt: 0.02f));

            Assert.IsFalse(charge.HandleTrigger(held: false, dt: 0.02f));
            Assert.AreEqual(0f, charge.ChargePct, 0.0001f);
        }

        [Test]
        public void Reset_EmptiesChargeAndHeldState()
        {
            charge.Configure(chargeTime: 1f, minChargeToFire: 0.3f, autoFireAtFull: false);
            charge.HandleTrigger(held: true, dt: 0.5f);

            charge.Reset();

            Assert.AreEqual(0f, charge.ChargePct, 0.0001f);
            // No stale release edge after a reset: the next un-held step must not fire.
            Assert.IsFalse(charge.HandleTrigger(held: false, dt: 0.02f));
        }

        [Test]
        public void ChargeEvents_ReportProgress()
        {
            charge.Configure(chargeTime: 1f, minChargeToFire: 0.3f, autoFireAtFull: false);

            var lastPct = -1f;
            charge.OnChargeChanged += pct => lastPct = pct;

            charge.HandleTrigger(held: true, dt: 0.25f);
            Assert.AreEqual(0.25f, lastPct, 0.0001f);

            charge.HandleTrigger(held: false, dt: 0.02f);
            Assert.AreEqual(0f, lastPct, 0.0001f, "A below-minimum release drains, and the drain is published.");
        }
    }
}
