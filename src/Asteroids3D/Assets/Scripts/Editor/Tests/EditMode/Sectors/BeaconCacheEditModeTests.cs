using System;
using System.Linq;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships.Mods;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Phases;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Sectors
{
    /// <summary>Each trial sector carries one beacon cache, set up after its spine and reading it, on the shared feel: 20 s bonuses from damage +100%, fire rate +60% and shield regen +100%, none of them a lull pick, or a 30% hull patch, all weighted alike unless the low-hull lean (off by default) favours the patch; the roll maps a uniform draw onto those weights.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class BeaconCacheEditModeTests
    {
        private const string FeelPath = "Assets/Settings/Game/BeaconCacheFeel.asset";

        [TestCase("TrialSector")]
        public void TrialSector_CarriesOneBeaconCache_AfterItsSpine(string sector)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/{sector}.prefab");
            var cache = prefab.Modules.OfType<BeaconCache>().Single();
            var spine = prefab.Modules.OfType<PhaseSpineModule>().Single();
            var modules = prefab.Modules.ToList();
            Assert.Greater(modules.IndexOf(cache), modules.IndexOf(spine),
                "The cache sets up after the spine resets its beacon claim.");

            using var serialized = new SerializedObject(cache);
            Assert.AreSame(spine, serialized.FindProperty("spine").objectReferenceValue, "The cache reads its own sector's spine.");
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<BeaconCacheFeelProfile>(FeelPath),
                serialized.FindProperty("feel").objectReferenceValue);
        }

        [Test]
        public void TheFeel_RollsTwentySecondBonuses_FromItsOwnList()
        {
            var feel = AssetDatabase.LoadAssetAtPath<BeaconCacheFeelProfile>(FeelPath);
            Assert.AreEqual(20f, feel.cacheSeconds);
            Assert.AreEqual(3, feel.bonuses.Length);

            var damage = (WeaponMod)feel.bonuses[0];
            Assert.AreEqual((WeaponModField.Damage, 1f), (damage.Field, damage.Amount));
            var fireRate = (WeaponMod)feel.bonuses[1];
            Assert.AreEqual((WeaponModField.FireRate, .6f), (fireRate.Field, fireRate.Amount));
            var shield = (ShipMod)feel.bonuses[2];
            Assert.AreEqual((ShipModField.ShieldRegen, 1f), (shield.Field, shield.Amount));
            Assert.AreEqual(.3f, feel.hullRestore, 1e-6f, "The hull patch restores 30% of a full hull.");
            Assert.AreEqual(1f, feel.hullWeight, "The hull patch weighs the same as each bonus.");
            Assert.IsFalse(feel.leanToHull, "The low-hull lean ships off.");
            Assert.AreEqual((.35f, 3f), (feel.lowHullThreshold, feel.lowHullWeightMultiplier));

            var pool = AssetDatabase.LoadAssetAtPath<LullModPool>("Assets/Settings/Weapons/Mods/LullMods.asset");
            Assert.IsFalse(feel.bonuses.Any(bonus => pool.mods.Contains(bonus)), "The cache's bonuses are never lull picks.");
        }

        [TestCase(.1f, 0)]
        [TestCase(.3f, 1)]
        [TestCase(.6f, 2)]
        [TestCase(.8f, -1)]
        [TestCase(1f, -1)]
        public void TheRoll_WeighsEachFittingBonusAndTheHullPatchAlike(float roll, int picked) =>
            Assert.AreEqual(picked, BeaconCache.Pick(3, 1f, roll), "Three bonuses and the patch split the draw in quarters; -1 is the patch.");

        [TestCase(false, .1f, 1f)]
        [TestCase(true, .1f, 3f)]
        [TestCase(true, .35f, 1f)]
        [TestCase(true, .6f, 1f)]
        public void TheLowHullLean_MultipliesTheHullPatchWeight_OnlyWhenOnAndBelowTheThreshold(bool lean, float hull, float weight)
        {
            var feel = ScriptableObject.CreateInstance<BeaconCacheFeelProfile>();
            try
            {
                feel.leanToHull = lean;
                Assert.AreEqual(weight, feel.HullWeight(hull), 1e-6f,
                    "Off leaves the odds alone; on, a hull below 35% triples the patch's weight, and 35% exactly does not.");
            }
            finally
            {
                Object.DestroyImmediate(feel);
            }
        }

        [Test]
        public void TheRoll_TakesTheHullPatch_WhenNoBonusFits_AndNeverAtZeroWeight()
        {
            Assert.AreEqual(-1, BeaconCache.Pick(0, 1f, .2f), "A ship no bonus fits still gets the patch.");
            Assert.AreEqual(1, BeaconCache.Pick(2, 0f, 1f), "At zero weight the top of the draw is the last bonus, never the patch.");
            Assert.Throws<InvalidOperationException>(() => BeaconCache.Pick(0, 0f, .5f), "Nothing to roll is a wiring bug.");
        }

        [Test]
        public void EachShippedBonus_HasItsCompanionKey_AndAnUnkeyedBonusThrows()
        {
            var feel = AssetDatabase.LoadAssetAtPath<BeaconCacheFeelProfile>(FeelPath);
            Assert.AreEqual(new[] { "damage", "fire-rate", "shield" }, feel.bonuses.Select(BeaconCache.KeyOf));

            var unkeyed = ScriptableObject.CreateInstance<ShipMod>();
            try
            {
                unkeyed.Configure("Hull bonus", ShipModField.Hull, .5f);
                Assert.Throws<InvalidOperationException>(() => BeaconCache.KeyOf(unkeyed));
            }
            finally
            {
                Object.DestroyImmediate(unkeyed);
            }
        }
    }
}
