using System.Collections.Generic;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Mods;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Ships
{
    /// <summary>Ship mod arithmetic, the resolve that folds a stack in, and the fit and landing read off prefab assets, with no live ship: bonuses on a field group sum and apply once, the recharge delay divides, only the group's fields move, a shield mod needs a shield module, and a mixed pool draws by what fits.</summary>
    [TestFixture]
    [Category("Ships")]
    public class ShipModsEditModeTests
    {
        private const string VanguardPath = "Assets/Prefabs/Ships/Vanguard.prefab";

        private readonly List<Object> made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in made) Object.DestroyImmediate(item);
            made.Clear();
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

        private Ship BareShip()
        {
            var go = new GameObject("BareShip", typeof(Ship));
            made.Add(go);
            return go.GetComponent<Ship>();
        }

        private static float[] Bonus(ShipModField field, float sum)
        {
            var bonus = new float[ShipModFields.Count];
            bonus[(int)field] = sum;
            return bonus;
        }

        [Test]
        public void Apply_SumsBonusesOnAFieldGroup_AndScalesTheAuthoredValueOnce()
        {
            var mods = new ShipMods(Bonus(ShipModField.Hull, 0.25f + 0.25f));

            Assert.AreEqual(1.5f, mods.Factor(ShipModField.Hull), 1e-5f, "two +25% picks read +50%, not x1.5625");
            Assert.AreEqual(150f, mods.Apply(ShipModField.Hull, 100f), 1e-4f);
            Assert.AreEqual(100f, mods.Apply(ShipModField.Speed, 100f), 1e-4f, "an unmodded group reads authored");
        }

        [Test]
        public void Apply_DividesTheRechargeDelay()
        {
            var mods = new ShipMods(Bonus(ShipModField.ShieldRecharge, 0.25f));

            Assert.AreEqual(3.2f, mods.Apply(ShipModField.ShieldRecharge, 4f), 1e-5f, "+25% recharge shortens the wait");
        }

        [Test]
        public void None_ReadsAuthoredOnEveryField()
        {
            for (var i = 0; i < ShipModFields.Count; i++)
            {
                var field = (ShipModField)i;
                Assert.AreEqual(3f, ShipMods.None.Apply(field, 3f), 1e-6f, field.ToString());
            }
        }

        [Test]
        public void Resolve_WithAStack_ScalesTheGroupsFields_AndLeavesTheRest()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(VanguardPath);
            Assert.IsNotNull(ship, "Vanguard prefab loads");
            var stack = new LullModStack();
            stack.Add(Mod("Gyros", ShipModField.Handling, 0.25f));
            stack.Add(Mod("Thrusters", ShipModField.Thrust, 0.25f));
            stack.Add(Mod("Shield capacitor", ShipModField.ShieldCapacity, 0.25f));
            stack.Add(Mod("Shield capacitor", ShipModField.ShieldCapacity, 0.25f));
            stack.Add(WeaponMod("Hot rounds", WeaponModField.Damage, 0.25f));

            var authored = ship.ResolveStats();
            var modded = ResolvedShipStats.Resolve(ship, ship.Engine, ship.Shield, stack.ForShip());

            Assert.AreEqual(authored.maxYawRate * 1.25f, modded.maxYawRate, 1e-3f);
            Assert.AreEqual(authored.yawTorque * 1.25f, modded.yawTorque, 1e-3f);
            Assert.AreEqual(authored.minStrafeForce * 1.25f, modded.minStrafeForce, 1e-3f);
            Assert.AreEqual(authored.maxStrafeForce * 1.25f, modded.maxStrafeForce, 1e-3f);
            Assert.AreEqual(authored.forwardForce * 1.25f, modded.forwardForce, 1e-3f);
            Assert.AreEqual(authored.reverseForce * 1.25f, modded.reverseForce, 1e-3f);
            Assert.AreEqual(authored.maxShield * 1.5f, modded.maxShield, 1e-3f, "two capacitors sum to +50%");
            Assert.AreEqual(authored.maxSpeed, modded.maxSpeed, 1e-4f, "speed is in neither the handling nor the thrust group");
            Assert.AreEqual(authored.maxHealth, modded.maxHealth, 1e-4f);
            Assert.AreEqual(authored.mass, modded.mass, 1e-4f);
            Assert.AreEqual(authored.linearDrag, modded.linearDrag, 1e-4f);
            Assert.AreEqual(authored.shieldRegenDelay, modded.shieldRegenDelay, 1e-4f);
            Assert.AreEqual(authored.maxHealth, ship.ResolveStats().maxHealth, 1e-4f, "the hangar read stays unmodded");
        }

        [Test]
        public void Effect_ReadsOffTheFieldAndAmount()
        {
            Assert.AreEqual("+25% max hull", Mod("Hull plating", ShipModField.Hull, 0.25f).Effect);
            Assert.AreEqual("+10% shield recharge", Mod("Fast recharge", ShipModField.ShieldRecharge, 0.1f).Effect);
        }

        [Test]
        public void Fits_And_Landing_ReadTheShipsModules()
        {
            var vanguard = AssetDatabase.LoadAssetAtPath<Ship>(VanguardPath);
            Assert.IsNotNull(vanguard, "Vanguard prefab loads");
            Assert.IsNotNull(vanguard.Shield, "test premise: Vanguard carries a shield");
            var hull = Mod("Hull plating", ShipModField.Hull, 0.25f);
            var speed = Mod("Afterburner", ShipModField.Speed, 0.25f);
            var shield = Mod("Shield capacitor", ShipModField.ShieldCapacity, 0.25f);

            Assert.IsTrue(hull.Fits(vanguard));
            Assert.IsTrue(speed.Fits(vanguard));
            Assert.IsTrue(shield.Fits(vanguard));
            Assert.AreEqual("Hull", hull.Landing(vanguard));
            Assert.AreEqual(vanguard.Engine.DisplayName, speed.Landing(vanguard));
            Assert.AreEqual(vanguard.Shield.DisplayName, shield.Landing(vanguard));

            var bare = BareShip();
            Assert.IsTrue(hull.Fits(bare), "a hull mod fits any ship");
            Assert.IsFalse(speed.Fits(bare), "no engine, nothing to scale");
            Assert.IsFalse(shield.Fits(bare), "no shield module, nothing to scale");
        }

        [Test]
        public void Draw_FromAMixedPool_OffersWhatFitsTheShip()
        {
            var bare = BareShip();
            var pool = new List<LullMod>
            {
                WeaponMod("Hot rounds", WeaponModField.Damage, 0.25f),
                Mod("Shield capacitor", ShipModField.ShieldCapacity, 0.25f),
                Mod("Hull plating", ShipModField.Hull, 0.25f),
            };

            var offer = LootOffers.Draw(pool, mod => mod.Fits(bare), 3, _ => 0);

            Assert.AreEqual(1, offer.Count, "no weapons and no shield: only the hull mod fits");
            Assert.AreEqual("Hull plating", offer[0].DisplayName);
        }

        [Test]
        public void Stack_RaisesChanged_OnAddAndClear()
        {
            var stack = new LullModStack();
            var raised = 0;
            stack.Changed += () => raised++;

            stack.Add(Mod("Hull plating", ShipModField.Hull, 0.25f));
            Assert.AreEqual(1, raised);
            Assert.AreEqual(1.25f, stack.ForShip().Factor(ShipModField.Hull), 1e-5f);

            stack.Clear();
            Assert.AreEqual(2, raised);
            Assert.AreSame(ShipMods.None, stack.ForShip(), "an emptied stack reads authored");
        }
    }
}
