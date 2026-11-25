using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships.Loadout;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Weapons
{
    /// <summary>The sector-break weapon pick's reads on prefab assets, with no ship: every catalog weapon judges a mod's fit off its serialized condition refs as its components say, an offer lists the held mods it would use, and a mount lists only the held mods the swap takes out of force.</summary>
    [TestFixture]
    [Category("Weapons")]
    public class WeaponPickEditModeTests
    {
        private readonly List<WeaponMod> made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var mod in made) Object.DestroyImmediate(mod);
            made.Clear();
        }

        private WeaponMod Mod(string name, WeaponModField field, params WeaponComponent[] fits)
        {
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            mod.name = name;
            mod.Configure(name, field, field == WeaponModField.Magazine ? 6f : 0.25f, fits);
            made.Add(mod);
            return mod;
        }

        private static WeaponComponent Weapon(string name) =>
            AssetDatabase.LoadAssetAtPath<WeaponComponent>($"Assets/Prefabs/Weapons/{name}.prefab");

        private static string[] Labels(IReadOnlyList<HeldMod> held) => held.Select(entry => entry.Label).ToArray();

        // What the weapon's components say it carries, read the way only a test may: by lookup.
        private static bool Carries(WeaponComponent weapon, WeaponModField field)
        {
            var rounds = weapon.GetComponent<Rounds>();
            return field switch
            {
                WeaponModField.FireRate => weapon.GetComponent<Cooldown>(),
                WeaponModField.Damage => true,
                WeaponModField.HeatCapacity or WeaponModField.Cooling => weapon.GetComponent<Heat>(),
                WeaponModField.Magazine => rounds,
                WeaponModField.ReloadSpeed => rounds && rounds.ReloadTime > 0f,
                WeaponModField.ChargeSpeed => weapon.GetComponent<ChargeTime>(),
                WeaponModField.BlastRadius => weapon as Grenades,
                _ => false,
            };
        }

        [Test]
        public void Supports_OnEveryCatalogWeaponPrefab_MatchesItsConditionComponents()
        {
            var weapons = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath).Weapons;
            Assert.IsNotEmpty(weapons);
            foreach (var weapon in weapons)
            foreach (WeaponModField field in Enum.GetValues(typeof(WeaponModField)))
                Assert.AreEqual(Carries(weapon, field), weapon.Supports(field), $"{weapon.name} {field}");

            Assert.IsTrue(Weapon("Lasers").Supports(WeaponModField.HeatCapacity),
                "a prefab asset with no Awake still reads its heat");
            Assert.IsFalse(Weapon("Rippers").Supports(WeaponModField.HeatCapacity));
        }

        [Test]
        public void Using_ListsTheHeldModsThatFitTheOffer_WithRepeatsCounted()
        {
            var stack = new LullModStack();
            var hot = Mod("Hot rounds", WeaponModField.Damage);
            stack.Add(hot);
            stack.Add(Mod("Coolant", WeaponModField.Cooling));
            stack.Add(hot);
            stack.Add(Mod("Deep magazine", WeaponModField.Magazine, Weapon("Rippers")));

            CollectionAssert.AreEqual(new[] { "Hot rounds ×2", "Coolant" }, Labels(stack.Using(Weapon("Lasers"))));
            CollectionAssert.AreEqual(new[] { "Hot rounds ×2" }, Labels(stack.Using(Weapon("Missiles"))),
                "a magazine mod listed for Rippers skips Missiles though it has a magazine");
            CollectionAssert.AreEqual(new[] { "Hot rounds ×2", "Deep magazine" }, Labels(stack.Using(Weapon("Rippers"))));
        }

        [Test]
        public void IdledBy_NamesOnlyTheModsTheSwapTakesOutOfForce()
        {
            var rippers = Weapon("Rippers");
            var lasers = Weapon("Lasers");
            var missiles = Weapon("Missiles");
            var stack = new LullModStack();
            stack.Add(Mod("Hot rounds", WeaponModField.Damage));
            stack.Add(Mod("Coolant", WeaponModField.Cooling));
            stack.Add(Mod("Deep magazine", WeaponModField.Magazine, rippers));
            stack.Add(Mod("Quick charge", WeaponModField.ChargeSpeed));

            CollectionAssert.AreEqual(new[] { "Deep magazine" }, Labels(stack.IdledBy(rippers, missiles, lasers)),
                "missiles over rippers: the rippers-only mod idles, damage and cooling stay in force");
            CollectionAssert.AreEqual(new[] { "Coolant" }, Labels(stack.IdledBy(lasers, missiles, rippers)),
                "missiles over lasers: no heat is left on the kit");
            Assert.IsEmpty(stack.IdledBy(null, missiles, rippers), "filling an empty mount idles nothing");
        }
    }
}
