using System.Collections.Generic;
using Combat.Weapons.Mods;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Weapons
{
    /// <summary>Weapon mod arithmetic and the lull offer draw, with no ship: bonuses on a field sum and apply once, rate fields divide their seconds, magazines add whole rounds, the effect line reads off the data, and an offer is distinct fitting mods up to the count.</summary>
    [TestFixture]
    [Category("Weapons")]
    public class WeaponModsEditModeTests
    {
        private readonly List<WeaponMod> made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var mod in made) Object.DestroyImmediate(mod);
            made.Clear();
        }

        private WeaponMod Mod(string name, WeaponModField field, float amount)
        {
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            mod.name = name;
            mod.Configure(name, field, amount);
            made.Add(mod);
            return mod;
        }

        private static float[] Bonus(WeaponModField field, float sum)
        {
            var bonus = new float[WeaponModFields.Count];
            bonus[(int)field] = sum;
            return bonus;
        }

        [Test]
        public void Apply_SumsBonusesOnAField_AndScalesTheAuthoredValueOnce()
        {
            var mods = new WeaponMods(Bonus(WeaponModField.HeatCapacity, 0.25f + 0.25f), 0);

            Assert.AreEqual(1.5f, mods.Factor(WeaponModField.HeatCapacity), 1e-5f, "two +25% picks read +50%, not x1.5625");
            Assert.AreEqual(150f, mods.Apply(WeaponModField.HeatCapacity, 100f), 1e-4f);
            Assert.AreEqual(100f, mods.Apply(WeaponModField.Cooling, 100f), 1e-4f, "an unmodded field reads authored");
        }

        [Test]
        public void Apply_DividesARateFieldsSeconds()
        {
            var mods = new WeaponMods(Bonus(WeaponModField.FireRate, 0.25f), 0);

            Assert.AreEqual(0.4f, mods.Apply(WeaponModField.FireRate, 0.5f), 1e-5f, "+25% fire rate shortens the interval");
            Assert.AreEqual(1.25f, mods.Factor(WeaponModField.FireRate), 1e-5f);
        }

        [Test]
        public void Rounds_AddsWholeRounds()
        {
            var mods = new WeaponMods(new float[WeaponModFields.Count], 6);

            Assert.AreEqual(30, mods.Rounds(24));
            Assert.AreEqual(2, WeaponMods.None.Rounds(2));
        }

        [Test]
        public void None_ReadsAuthoredOnEveryField()
        {
            for (var i = 0; i < WeaponModFields.Count; i++)
            {
                var field = (WeaponModField)i;
                Assert.AreEqual(3f, WeaponMods.None.Apply(field, 3f), 1e-6f, field.ToString());
                Assert.AreEqual(1f, WeaponMods.None.Factor(field), 1e-6f, field.ToString());
            }
        }

        [Test]
        public void Effect_ReadsOffTheFieldAndAmount()
        {
            Assert.AreEqual("+25% fire rate", Mod("Rapid fire", WeaponModField.FireRate, 0.25f).Effect);
            Assert.AreEqual("+1 round", Mod("Extra missile", WeaponModField.Magazine, 1f).Effect);
            Assert.AreEqual("+6 rounds", Mod("Deep magazine", WeaponModField.Magazine, 6f).Effect);
            Assert.AreEqual("+25% blast radius", Mod("Wide blast", WeaponModField.BlastRadius, 0.25f).Effect);
        }

        [Test]
        public void Draw_OffersOnlyFittingMods_DistinctUpToTheCount()
        {
            var pool = new List<WeaponMod>();
            foreach (var name in new[] { "a", "b", "c", "d", "e" })
                pool.Add(Mod(name, WeaponModField.Damage, 0.25f));

            var offer = WeaponModOffers.Draw(pool, mod => mod.name != "c", 3, _ => 0);
            CollectionAssert.AreEqual(new[] { "a", "b", "d" }, offer.ConvertAll(mod => mod.name),
                "the unfitting mod is skipped and each draw removes its pick");

            var two = WeaponModOffers.Draw(pool, mod => mod.name is "b" or "e", 3, n => n - 1);
            CollectionAssert.AreEquivalent(new[] { "b", "e" }, two.ConvertAll(mod => mod.name),
                "fewer fitting mods than the count offers what fits");

            Assert.IsEmpty(WeaponModOffers.Draw(pool, _ => false, 3, _ => 0), "nothing fits, nothing offered");
        }
    }
}
