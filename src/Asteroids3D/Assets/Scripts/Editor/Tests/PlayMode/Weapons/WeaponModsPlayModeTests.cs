#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    /// <summary>
    /// Mods ride the run: handed to a ship's mounts they change the effective numbers the conditions
    /// and the HUD read, survive the sector-load reset and a reequip, land only where they fit and idle
    /// otherwise, and leave the prefab assets, which enemies share, untouched.
    /// </summary>
    [Category("Weapons")]
    public class WeaponModsPlayModeTests : PlayModeWorldFixture
    {
        private const string LasersPrefabPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const string RippersPrefabPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";

        private Ship ship;
        private readonly List<WeaponMod> made = new();

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            foreach (var mod in made) Object.DestroyImmediate(mod);
            made.Clear();
            base.TearDown();
        }

        private static T LoadWeapon<T>(string path) where T : WeaponComponent
        {
            var weapon = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(weapon, $"Failed to load weapon prefab at {path}");
            return weapon;
        }

        private WeaponMod Mod(string name, WeaponModField field, float amount, params WeaponComponent[] fits)
        {
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            mod.name = name;
            mod.Configure(name, field, amount, fits);
            made.Add(mod);
            return mod;
        }

        [UnityTest]
        public IEnumerator Mods_ChangeTheMountsEffectiveNumbers_AndSurviveResetAndReequip()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            var lasers = LoadWeapon<Lasers>(LasersPrefabPath);
            var rippers = LoadWeapon<Rippers>(RippersPrefabPath);
            ship.Reequip(ship.Shield, lasers, rippers);
            yield return null;

            var authoredHeat = lasers.GetComponent<Heat>().MaxHeat;
            var authoredMagazine = rippers.GetComponent<Rounds>().MaxAmmo;
            var authoredInterval = rippers.GetComponent<Cooldown>().SecondsBetweenShots;

            var stack = new WeaponModStack();
            stack.Add(Mod("Heat sink", WeaponModField.HeatCapacity, 0.25f));
            stack.Add(Mod("Heat sink", WeaponModField.HeatCapacity, 0.25f));
            stack.Add(Mod("Deep magazine", WeaponModField.Magazine, 6f, rippers));
            stack.Add(Mod("Rapid fire", WeaponModField.FireRate, 0.25f));
            ship.Weapons.SetMods(stack);

            var heat = ship.Weapons.Primary.GetComponent<Heat>();
            var rounds = ship.Weapons.Secondary.GetComponent<Rounds>();
            Assert.AreEqual(authoredHeat * 1.5f, heat.MaxHeat, 1e-3f, "two heat sinks sum to +50%");
            Assert.AreEqual(authoredMagazine + 6, rounds.MaxAmmo, "the deep magazine adds its rounds");
            Assert.AreEqual(rounds.MaxAmmo, rounds.AmmoCount, "added rounds arrive loaded");
            Assert.AreEqual(authoredInterval / 1.25f, ship.Weapons.Secondary.GetComponent<Cooldown>().SecondsBetweenShots,
                1e-4f, "rapid fire shortens the interval on every carried weapon");
            Assert.AreEqual(authoredHeat, lasers.GetComponent<Heat>().MaxHeat, 1e-4f, "the prefab asset reads authored");

            ship.ResetShip();
            Assert.AreEqual(authoredHeat * 1.5f, heat.MaxHeat, 1e-3f, "the sector-load reset keeps the mods");
            Assert.AreEqual(authoredMagazine + 6, rounds.AmmoCount, "a reset refills to the modded magazine");

            ship.Reequip(ship.Shield, rippers, lasers);
            yield return null;
            Assert.AreEqual(authoredMagazine + 6, ship.Weapons.Primary.GetComponent<Rounds>().MaxAmmo,
                "a fresh mount reads the same picks");
            Assert.AreEqual(authoredHeat * 1.5f, ship.Weapons.Secondary.GetComponent<Heat>().MaxHeat, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Mods_LandOnlyWhereTheyFit_AndIdleOtherwise()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            var lasers = LoadWeapon<Lasers>(LasersPrefabPath);
            var rippers = LoadWeapon<Rippers>(RippersPrefabPath);
            var missiles = LoadWeapon<Missiles>(MissilesPrefabPath);
            ship.Reequip(ship.Shield, rippers, missiles);
            yield return null;

            var authoredRipperMagazine = rippers.GetComponent<Rounds>().MaxAmmo;
            var authoredMissileMagazine = missiles.GetComponent<Rounds>().MaxAmmo;

            var deepMagazine = Mod("Deep magazine", WeaponModField.Magazine, 6f, rippers);
            var extraMissile = Mod("Extra missile", WeaponModField.Magazine, 1f, missiles);
            var coolant = Mod("Coolant", WeaponModField.Cooling, 0.25f);
            var stack = new WeaponModStack();
            stack.Add(deepMagazine);
            stack.Add(extraMissile);
            stack.Add(coolant);
            ship.Weapons.SetMods(stack);

            Assert.AreEqual(authoredRipperMagazine + 6, ship.Weapons.Primary.GetComponent<Rounds>().MaxAmmo);
            Assert.AreEqual(authoredMissileMagazine + 1, ship.Weapons.Secondary.GetComponent<Rounds>().MaxAmmo);
            Assert.IsFalse(ship.Weapons.Fits(coolant), "no carried weapon has heat, so coolant idles");
            Assert.IsTrue(ship.Weapons.Fits(deepMagazine));
            Assert.AreEqual("Rippers", ship.Weapons.LandingNames(deepMagazine));

            ship.Reequip(ship.Shield, lasers, missiles);
            yield return null;
            Assert.IsFalse(ship.Weapons.Fits(deepMagazine), "the magazine mod idles once the rippers leave");
            Assert.IsTrue(ship.Weapons.Fits(coolant), "coolant wakes on the lasers");
            Assert.AreEqual(authoredMissileMagazine + 1, ship.Weapons.Secondary.GetComponent<Rounds>().MaxAmmo,
                "the unchanged mount keeps its mods");

            stack.Clear();
            ship.Weapons.SetMods(stack);
            Assert.AreEqual(authoredMissileMagazine, ship.Weapons.Secondary.GetComponent<Rounds>().MaxAmmo,
                "an emptied stack reads authored again");
        }
    }
}
#endif
