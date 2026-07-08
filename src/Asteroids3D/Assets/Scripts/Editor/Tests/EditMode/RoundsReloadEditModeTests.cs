using Combat.Conditions;
using NUnit.Framework;
using UnityEngine;

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
    }
}
