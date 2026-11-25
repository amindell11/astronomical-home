#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Mods;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Loot
{
    /// <summary>
    /// A ship bound to a run's lull mods applies a ship mod at once with the vitals' fractions kept and no
    /// heal, keeps it across the sector-load reset and a reequip, hands weapon mods to its mounts on
    /// every change, and leaves an unbound ship on the same prefab at authored numbers.
    /// </summary>
    [Category("Ships")]
    public class ShipModsPlayModeTests : PlayModeWorldFixture
    {
        private const string LasersPrefabPath = "Assets/Prefabs/Weapons/Lasers.prefab";

        private Ship ship;
        private Ship other;
        private readonly List<Object> made = new();

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            ShipTestFactory.DestroyShip(other);
            foreach (var item in made) Object.DestroyImmediate(item);
            made.Clear();
            base.TearDown();
        }

        private ShipMod Mod(string name, ShipModField field, float amount)
        {
            var mod = ScriptableObject.CreateInstance<ShipMod>();
            mod.name = name;
            mod.Configure(name, field, amount);
            made.Add(mod);
            return mod;
        }

        private WeaponMod WeaponMod(string name, WeaponModField field, float amount)
        {
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            mod.name = name;
            mod.Configure(name, field, amount);
            made.Add(mod);
            return mod;
        }

        [UnityTest]
        public IEnumerator Pick_RaisesTheMax_KeepsTheFraction_AndChangesHandling_WithNoHeal()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            var authored = ship.ResolveStats();
            var radius = ship.Stats.shipRadius;
            ship.Damage.Health.SetFraction(0.5f);
            ship.Damage.Shield.SetFraction(0.4f);

            var stack = new LullModStack();
            ship.BindMods(stack);
            Assert.AreEqual(authored.maxHealth, ship.Damage.Health.MaxValue, 1e-3f, "an empty stack binds at authored");
            Assert.AreEqual(0.5f, ship.Damage.Health.Pct, 1e-4f, "binding heals nothing");

            stack.Add(Mod("Hull plating", ShipModField.Hull, 0.25f));
            Assert.AreEqual(authored.maxHealth * 1.25f, ship.Stats.maxHealth, 1e-3f);
            Assert.AreEqual(authored.maxHealth * 1.25f, ship.Damage.Health.MaxValue, 1e-3f, "the bar rises at once");
            Assert.AreEqual(0.5f, ship.Damage.Health.Pct, 1e-4f, "the fraction is kept, no heal");
            Assert.AreEqual(0.4f, ship.Damage.Shield.Pct, 1e-4f, "a hull pick leaves the shield alone");

            stack.Add(Mod("Shield capacitor", ShipModField.ShieldCapacity, 0.5f));
            Assert.AreEqual(authored.maxShield * 1.5f, ship.Damage.Shield.MaxValue, 1e-3f);
            Assert.AreEqual(0.4f, ship.Damage.Shield.Pct, 1e-4f, "the shield keeps its fraction too");

            stack.Add(Mod("Afterburner", ShipModField.Speed, 0.25f));
            stack.Add(Mod("Gyros", ShipModField.Handling, 0.25f));
            Assert.AreEqual(authored.maxSpeed * 1.25f, ship.Rigidbody.maxLinearVelocity, 1e-3f, "the body's cap follows");
            Assert.AreEqual(authored.maxSpeed * 1.25f, ship.Dynamics.maxSpeed, 1e-3f, "the dynamics follow");
            Assert.AreEqual(authored.maxYawRate * 1.25f, ship.MaxYawRate, 1e-3f);
            Assert.AreEqual(authored.yawTorque * 1.25f, ship.Dynamics.yawTorque, 1e-3f);
            Assert.AreEqual(radius, ship.Stats.shipRadius, 1e-5f, "the derived radius carries across a pick");
        }

        [UnityTest]
        public IEnumerator Mods_SurviveResetAndReequip_AndAnUnboundShipReadsAuthored()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            other = ShipTestFactory.CreateDefaultShipAt(new Vector3(30f, 0f, 0f), Quaternion.identity, Projectiles);
            Assert.IsNotNull(ship, "ship created");
            Assert.IsNotNull(other, "other ship created");
            yield return null;

            var authored = ship.ResolveStats().maxHealth;
            var stack = new LullModStack();
            ship.BindMods(stack);
            stack.Add(Mod("Hull plating", ShipModField.Hull, 0.25f));

            ship.gameObject.SetActive(false);
            ship.ResetShip();
            yield return null;
            Assert.AreEqual(authored * 1.25f, ship.Damage.Health.MaxValue, 1e-3f, "the sector-load reset keeps the mod");

            ship.Reequip(ship.Weapons.PrimaryMountPrefab, ship.Weapons.SecondaryMountPrefab);
            yield return null;
            Assert.AreEqual(authored * 1.25f, ship.Damage.Health.MaxValue, 1e-3f, "the hangar equip keeps the mod");

            Assert.AreEqual(authored, other.Damage.Health.MaxValue, 1e-3f, "an unbound ship on the same prefab reads authored");
            Assert.AreEqual(authored, ship.ResolveStats().maxHealth, 1e-3f, "the hangar read stays unmodded");

            ship.Damage.Health.SetFraction(0.5f);
            stack.Clear();
            Assert.AreEqual(authored, ship.Damage.Health.MaxValue, 1e-3f, "a cleared stack reads authored again");
            Assert.AreEqual(0.5f, ship.Damage.Health.Pct, 1e-4f, "clearing heals nothing either");
        }

        [UnityTest]
        public IEnumerator BoundStack_HandsWeaponModsToTheMounts_OnEveryChange_AndLetsGoOnDestroy()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;
            var lasers = AssetDatabase.LoadAssetAtPath<Lasers>(LasersPrefabPath);
            Assert.IsNotNull(lasers, "Lasers prefab loads");
            ship.Reequip(lasers, ship.Weapons.SecondaryMountPrefab);
            yield return null;
            var authoredHeat = lasers.GetComponent<Heat>().MaxHeat;
            var heat = ship.Weapons.Primary.GetComponent<Heat>();

            var stack = new LullModStack();
            ship.BindMods(stack);
            stack.Add(WeaponMod("Heat sink", WeaponModField.HeatCapacity, 0.25f));
            Assert.AreEqual(authoredHeat * 1.25f, heat.MaxHeat, 1e-3f, "the ship hands the pick to its mount");
            stack.Add(WeaponMod("Heat sink", WeaponModField.HeatCapacity, 0.25f));
            Assert.AreEqual(authoredHeat * 1.5f, heat.MaxHeat, 1e-3f, "and re-hands on the next pick");

            ShipTestFactory.DestroyShip(ship);
            ship = null;
            yield return null;
            Assert.DoesNotThrow(() => stack.Add(Mod("Hull plating", ShipModField.Hull, 0.25f)),
                "a destroyed ship has let go of the stack");
        }
    }
}
#endif
