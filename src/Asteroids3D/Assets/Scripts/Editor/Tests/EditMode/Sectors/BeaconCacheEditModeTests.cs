using System.Linq;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships.Mods;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Phases;
using UnityEditor;

namespace Tests.EditMode.Sectors
{
    /// <summary>Each trial sector carries one beacon cache, set up after its spine and reading it, on the shared feel: 20 s bonuses rolled from damage +100%, fire rate +60% and shield regen +100%, none of them a lull pick.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class BeaconCacheEditModeTests
    {
        private const string FeelPath = "Assets/Settings/Game/BeaconCacheFeel.asset";

        [TestCase("TrialSector")]
        [TestCase("TrialSector2")]
        [TestCase("TrialSector3")]
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

            var pool = AssetDatabase.LoadAssetAtPath<LullModPool>("Assets/Settings/Weapons/Mods/LullMods.asset");
            Assert.IsFalse(feel.bonuses.Any(bonus => pool.mods.Contains(bonus)), "The cache's bonuses are never lull picks.");
        }
    }
}
