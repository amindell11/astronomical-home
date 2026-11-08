using System.Linq;
using Combat.Weapons.Mods;
using Game.Moments;
using NUnit.Framework;
using Ships.Damage;
using UnityEngine;

namespace Tests.EditMode.Companion
{
    [Category("UI")]
    public class CloseCallWatchEditModeTests
    {
        private const float Max = 100f;

        private Resource hull;
        private RunMomentStream moments;

        [SetUp]
        public void SetUp()
        {
            hull = new Resource(Max);
            moments = new RunMomentStream();
        }

        private CloseCall[] CloseCalls => moments.Moments.OfType<CloseCall>().ToArray();

        [Test]
        public void OneDip_RaisesOneCloseCall_HoweverManyHitsFollow()
        {
            using var watch = new CloseCallWatch(hull, moments, 2);

            hull.ApplyDamage(70f);
            Assert.IsEmpty(CloseCalls, "30% is still above the low threshold");
            hull.ApplyDamage(15f);
            hull.ApplyDamage(5f);
            hull.ApplyDamage(5f);

            var call = CloseCalls.Single();
            Assert.AreEqual(2, call.Sector);
            Assert.AreEqual(.15f, call.Hull, 1e-4f);
            Assert.AreEqual("15", call.Facts["hull"]);
            Assert.AreEqual("2", call.Facts["sector"]);
        }

        [Test]
        public void ClimbingBackAboveTheThreshold_RearmsIt()
        {
            using var watch = new CloseCallWatch(hull, moments, 1);

            hull.ApplyDamage(80f);
            hull.RestoreFraction(.05f);
            hull.ApplyDamage(5f);
            Assert.AreEqual(1, CloseCalls.Length, "25% is not back above the threshold");

            hull.RestoreFraction(.5f);
            hull.ApplyDamage(60f);
            Assert.AreEqual(2, CloseCalls.Length);
        }

        [Test]
        public void ALethalHit_IsNotACloseCall()
        {
            using var watch = new CloseCallWatch(hull, moments, 1);

            hull.ApplyDamage(Max);

            Assert.IsEmpty(CloseCalls);
        }

        [Test]
        public void AHullAlreadyLow_SaysNothingUntilItRecoversAndDipsAgain()
        {
            hull.SetFraction(.2f);
            using var watch = new CloseCallWatch(hull, moments, 1);

            hull.ApplyDamage(5f);
            Assert.IsEmpty(CloseCalls);

            hull.RestoreFraction(.5f);
            hull.ApplyDamage(60f);
            Assert.AreEqual(1, CloseCalls.Length);
        }

        [Test]
        public void ADisposedWatch_RaisesNothing()
        {
            new CloseCallWatch(hull, moments, 1).Dispose();

            hull.ApplyDamage(90f);

            Assert.IsEmpty(moments.Moments);
        }

        [Test]
        public void InRunMoments_QuoteTheirFacts()
        {
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            mod.name = "Overclock";
            try
            {
                Assert.AreEqual("Overclock", new ModPicked(mod).Facts["mod"]);
                Assert.AreEqual(ModPicked.PickSlot, new ModPicked(mod).Slot);
            }
            finally
            {
                Object.DestroyImmediate(mod);
            }
            Assert.AreEqual("3", new SectorEntered(3).Facts["sector"]);
            Assert.AreEqual(SectorEntered.SectorSlot, new SectorEntered(3).Slot);
            Assert.AreEqual("2", new BeaconClaimed(2).Facts["sector"]);
            Assert.AreEqual(BeaconClaimed.BeaconSlot, new BeaconClaimed(2).Slot);
        }
    }
}
