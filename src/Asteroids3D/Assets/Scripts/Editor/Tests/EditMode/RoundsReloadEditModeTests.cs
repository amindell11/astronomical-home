using NUnit.Framework;
using UnityEngine;
using Combat.Weapons.Conditions;

namespace Tests.EditMode
{
    [Category("Weapons")]
    public class RoundsReloadEditModeTests
    {
        private GameObject go;
        private Rounds rounds;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("RoundsTest");
            rounds = go.AddComponent<Rounds>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        private void FireUntilEmpty()
        {
            while (rounds.AmmoCount > 0)
                rounds.ProcessFire();
        }

        [Test]
        public void EmptyMagazine_WithReloadTime_StartsReloadAndBlocksFire()
        {
            rounds.Configure(maxAmmo: 2, reloadTime: 1f);

            FireUntilEmpty();

            Assert.IsTrue(rounds.IsReloading);
            Assert.IsFalse(rounds.CanFire());
            Assert.AreEqual(0, rounds.AmmoCount);
        }

        [Test]
        public void Reload_RefillsMagazine_AfterReloadTimeElapses()
        {
            rounds.Configure(maxAmmo: 2, reloadTime: 1f);
            FireUntilEmpty();

            rounds.Tick(0.5f);
            Assert.IsTrue(rounds.IsReloading, "Reload should still be in progress at half time.");
            Assert.IsFalse(rounds.CanFire());

            rounds.Tick(0.6f);
            Assert.IsFalse(rounds.IsReloading);
            Assert.AreEqual(2, rounds.AmmoCount);
            Assert.IsTrue(rounds.CanFire());
        }

        [Test]
        public void ReloadTimeZero_NeverAutoRefills()
        {
            // Missile-style magazine (the pre-reload behaviour): empty stays empty until Reset.
            rounds.Configure(maxAmmo: 2, reloadTime: 0f);
            FireUntilEmpty();

            Assert.IsFalse(rounds.IsReloading);
            rounds.Tick(60f);
            Assert.AreEqual(0, rounds.AmmoCount);
            Assert.IsFalse(rounds.CanFire());
        }

        [Test]
        public void ReloadOnlyStartsWhenMagazineEmpties()
        {
            rounds.Configure(maxAmmo: 3, reloadTime: 1f);

            rounds.ProcessFire();
            Assert.IsFalse(rounds.IsReloading);
            Assert.AreEqual(2, rounds.AmmoCount);
        }

        [Test]
        public void Reload_PartialMagazine_DumpsRoundsAndRefillsAfterReloadTime()
        {
            rounds.Configure(maxAmmo: 3, reloadTime: 1f);
            rounds.ProcessFire();

            rounds.Reload();

            Assert.IsTrue(rounds.IsReloading);
            Assert.AreEqual(0, rounds.AmmoCount, "the rounds left are dumped");
            Assert.IsFalse(rounds.CanFire());

            rounds.Tick(1.1f);
            Assert.IsFalse(rounds.IsReloading);
            Assert.AreEqual(3, rounds.AmmoCount);
        }

        [Test]
        public void Reload_FullMagazine_DoesNothing()
        {
            rounds.Configure(maxAmmo: 3, reloadTime: 1f);

            rounds.Reload();

            Assert.IsFalse(rounds.IsReloading);
            Assert.AreEqual(3, rounds.AmmoCount);
        }

        [Test]
        public void Reload_PerRoundRefill_DoesNothing()
        {
            rounds.Configure(maxAmmo: 3, reloadTime: 1f, Rounds.RefillMode.PerRound);
            rounds.ProcessFire();

            rounds.Reload();

            Assert.AreEqual(2, rounds.AmmoCount, "per-round refill keeps its rounds");
            rounds.Tick(1f);
            Assert.AreEqual(3, rounds.AmmoCount, "the spent round still returns on its own timer");
        }

        [Test]
        public void Reset_CancelsReloadAndRefills()
        {
            rounds.Configure(maxAmmo: 2, reloadTime: 5f);
            FireUntilEmpty();
            Assert.IsTrue(rounds.IsReloading);

            rounds.Reset();

            Assert.IsFalse(rounds.IsReloading);
            Assert.AreEqual(2, rounds.AmmoCount);

            // A stale reload must not re-trigger after the reset.
            rounds.Tick(10f);
            Assert.AreEqual(2, rounds.AmmoCount);
        }

        [Test]
        public void ReloadEvents_FireOnStartAndCompletion()
        {
            rounds.Configure(maxAmmo: 1, reloadTime: 1f);

            var started = 0;
            var completed = 0;
            var lastAmmoNotified = -1;
            rounds.OnReloadStarted += () => started++;
            rounds.OnReloadCompleted += () => completed++;
            rounds.OnAmmoCountChanged += ammo => lastAmmoNotified = ammo;

            rounds.ProcessFire();
            Assert.AreEqual(1, started);
            Assert.AreEqual(0, completed);
            Assert.AreEqual(0, lastAmmoNotified);

            rounds.Tick(1.1f);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, lastAmmoNotified, "Completion should notify the refilled ammo count.");
        }

        [Test]
        public void ReloadProgress_ReportsNormalizedElapsed()
        {
            rounds.Configure(maxAmmo: 1, reloadTime: 2f);
            Assert.AreEqual(0f, rounds.ReloadProgress);

            rounds.ProcessFire();
            rounds.Tick(1f);
            Assert.AreEqual(0.5f, rounds.ReloadProgress, 0.0001f);

            rounds.Tick(1f);
            Assert.AreEqual(0f, rounds.ReloadProgress, 0.0001f, "Progress reads 0 once reload completes.");
        }

        [Test]
        public void Sequential_RapidShots_ReturnAtTenAndTwentySeconds_AndNotifyIdleEdges()
        {
            rounds.Configure(2, 10f, Rounds.RefillMode.Sequential);
            var started = 0;
            var completed = 0;
            var notifiedAmmo = -1;
            rounds.OnReloadStarted += () => started++;
            rounds.OnReloadCompleted += () => completed++;
            rounds.OnAmmoCountChanged += ammo => notifiedAmmo = ammo;
            rounds.ProcessFire();
            rounds.Tick(1f);
            rounds.ProcessFire();
            Assert.That(rounds.ReloadProgress, Is.EqualTo(.1f).Within(.001f));
            Assert.That(started, Is.EqualTo(1));
            rounds.Tick(8.9f);
            Assert.IsFalse(rounds.CanFire());
            rounds.Tick(.1f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(1));
            Assert.That(notifiedAmmo, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
            rounds.Tick(1f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(1));
            rounds.Tick(9f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(2));
            Assert.That(notifiedAmmo, Is.EqualTo(2));
            Assert.That(completed, Is.EqualTo(1));
            Assert.IsFalse(rounds.IsReloading);
        }

        [Test]
        public void Sequential_FiringDuringRecharge_PreservesProgressTowardNextRound()
        {
            rounds.Configure(2, 10f, Rounds.RefillMode.Sequential);
            FireUntilEmpty();
            rounds.Tick(15f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(1));
            rounds.ProcessFire();
            Assert.That(rounds.ReloadProgress, Is.EqualTo(.5f));
            Assert.That(rounds.RoundProgress(0), Is.EqualTo(.5f));
            Assert.That(rounds.RoundProgress(1), Is.Zero);
            rounds.Tick(5f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(1));
            Assert.That(rounds.ReloadProgress, Is.Zero);
        }

        [Test]
        public void Sequential_FullCapacity_DiscardsExtraTime_AndIdleDoesNotBankRecharge()
        {
            rounds.Configure(2, 10f, Rounds.RefillMode.Sequential);
            FireUntilEmpty();
            rounds.Tick(25f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(2));
            rounds.Tick(60f);
            rounds.ProcessFire();
            Assert.That(rounds.ReloadProgress, Is.Zero);
            rounds.Tick(9f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(1));
            rounds.Tick(1f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(2));
        }

        [Test]
        public void Sequential_Reset_ClearsPartialRecharge()
        {
            rounds.Configure(2, 10f, Rounds.RefillMode.Sequential);
            rounds.ProcessFire();
            rounds.Tick(5f);
            rounds.Reset();
            Assert.IsFalse(rounds.IsReloading);
            rounds.ProcessFire();
            rounds.Tick(5f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(1));
            Assert.That(rounds.ReloadProgress, Is.EqualTo(.5f));
        }

        [Test]
        public void PerRound_EachRoundReturnsOnItsOwnTimer_AndFiresWhileRegenerating()
        {
            rounds.Configure(maxAmmo: 3, reloadTime: 1f, Rounds.RefillMode.PerRound);

            rounds.ProcessFire();
            Assert.IsTrue(rounds.IsReloading, "Regen starts on the first shot, not on empty.");
            rounds.Tick(0.5f);
            Assert.IsTrue(rounds.CanFire());
            rounds.ProcessFire();
            Assert.AreEqual(1, rounds.AmmoCount);

            rounds.Tick(0.5f);
            Assert.AreEqual(2, rounds.AmmoCount, "First round is back one reloadTime after it was fired.");
            Assert.IsTrue(rounds.IsReloading);

            rounds.Tick(0.5f);
            Assert.AreEqual(3, rounds.AmmoCount);
            Assert.IsFalse(rounds.IsReloading);
        }

        [Test]
        public void PerRound_RefillCapsAtMax()
        {
            rounds.Configure(maxAmmo: 3, reloadTime: 1f, Rounds.RefillMode.PerRound);
            FireUntilEmpty();

            rounds.Tick(10f);

            Assert.AreEqual(3, rounds.AmmoCount);
            Assert.IsFalse(rounds.IsReloading);
        }

        [Test]
        public void PerRound_ReloadEvents_FireOnlyOnIdleEdges()
        {
            rounds.Configure(maxAmmo: 2, reloadTime: 1f, Rounds.RefillMode.PerRound);
            var started = 0;
            var completed = 0;
            var lastAmmoNotified = -1;
            rounds.OnReloadStarted += () => started++;
            rounds.OnReloadCompleted += () => completed++;
            rounds.OnAmmoCountChanged += ammo => lastAmmoNotified = ammo;

            rounds.ProcessFire();
            rounds.Tick(0.5f);
            rounds.ProcessFire();
            Assert.AreEqual(1, started);

            rounds.Tick(0.5f);
            Assert.AreEqual(0, completed, "One round still regenerating.");
            Assert.AreEqual(1, lastAmmoNotified);

            rounds.Tick(0.5f);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(2, lastAmmoNotified);
        }

        [Test]
        public void PerRound_ReloadProgress_TracksSoonestRound()
        {
            rounds.Configure(maxAmmo: 2, reloadTime: 2f, Rounds.RefillMode.PerRound);

            rounds.ProcessFire();
            rounds.Tick(1f);
            rounds.ProcessFire();
            Assert.AreEqual(0.5f, rounds.ReloadProgress, 0.0001f);

            rounds.Tick(1f);
            Assert.AreEqual(0.5f, rounds.ReloadProgress, 0.0001f, "Second round is now the soonest, half done.");
        }

        [Test]
        public void PerRound_Reset_ClearsPendingRounds()
        {
            rounds.Configure(maxAmmo: 2, reloadTime: 1f, Rounds.RefillMode.PerRound);
            FireUntilEmpty();
            rounds.Tick(0.5f);

            rounds.Reset();
            Assert.IsFalse(rounds.IsReloading);
            Assert.AreEqual(2, rounds.AmmoCount);

            rounds.ProcessFire();
            rounds.Tick(0.75f);
            Assert.AreEqual(1, rounds.AmmoCount, "A stale pre-reset timer must not return this round early.");
            rounds.Tick(0.25f);
            Assert.AreEqual(2, rounds.AmmoCount);
        }
    }
}
