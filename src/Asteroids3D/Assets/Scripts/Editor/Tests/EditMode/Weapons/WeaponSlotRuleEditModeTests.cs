using Combat.Weapons;
using NUnit.Framework;
using Ships.Command;
using Ships.Weapons;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Factions;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Weapons
{
    /// <summary>The slot rule on shipped assets: each catalog item's kind names its slot, every elite profile and faction kit fits the rule, and the trial elites fly their ruled kits.</summary>
    [TestFixture]
    [Category("Weapons")]
    public class WeaponSlotRuleEditModeTests
    {
        [TestCase("Lasers", WeaponSlot.Primary)]
        [TestCase("TurboLasers", WeaponSlot.Primary)]
        [TestCase("Rippers", WeaponSlot.Primary)]
        [TestCase("Railgun", WeaponSlot.Primary)]
        [TestCase("Missiles", WeaponSlot.Secondary)]
        [TestCase("Grenades", WeaponSlot.Secondary)]
        public void EachCatalogItem_FillsTheSlotItsKindNames(string item, WeaponSlot slot) =>
            Assert.AreEqual(slot, WeaponsController.SlotFor(Weapon(item)));

        [Test]
        public void EveryShippedEliteProfileAndFactionKit_FitsTheSlotRule()
        {
            var profiles = Assets<EliteProfile>();
            Assert.That(profiles.Length, Is.GreaterThanOrEqualTo(3), "test premise: the three trial elites");
            foreach (var profile in profiles)
                Assert.DoesNotThrow(profile.Validate, profile.name);

            var factions = Assets<Faction>();
            Assert.That(factions.Length, Is.GreaterThanOrEqualTo(3), "test premise: the three trial factions");
            foreach (var faction in factions)
            {
                Assert.DoesNotThrow(() => WeaponsController.RequireKinds(faction.name, faction.signature.kit, null),
                    $"{faction.name}'s signature kit");
                Assert.DoesNotThrow(() => WeaponsController.RequireKinds(faction.name, faction.common.kit, null),
                    $"{faction.name}'s common kit");
            }
        }

        [TestCase("ShoalElite", "TurboLasers", "Missiles")]
        [TestCase("VeilElite", "TurboLasers", "Missiles")]
        [TestCase("BastionBoss", "TurboLasers", "Grenades")]
        public void EachTrialElite_FliesItsRuledKit(string profile, string weapon, string ordnance)
        {
            var elite = AssetDatabase.LoadAssetAtPath<EliteProfile>($"Assets/Settings/Ships/Elites/{profile}.asset");
            Assert.IsNotNull(elite, profile);

            Assert.AreSame(Weapon(weapon), elite.Primary, $"{profile}'s weapon");
            Assert.AreSame(Weapon(ordnance), elite.Secondary, $"{profile}'s ordnance");
        }

        private static WeaponComponent Weapon(string name)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponComponent>($"Assets/Prefabs/Weapons/{name}.prefab");
            Assert.IsNotNull(weapon, name);
            return weapon;
        }

        private static T[] Assets<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" });
            var assets = new T[guids.Length];
            for (var i = 0; i < guids.Length; i++)
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return assets;
        }
    }
}
