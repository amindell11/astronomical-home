using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships.Keystones;
using Ships.Mods;
using Ships.Status;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Ships
{
    /// <summary>Behaviour mods with no ship: each kind lands only on the parts it lists, a part's held set tells its consumers only when it changes, the shipped offer pool holds only behaviour mods, and the shared status effects time out, keep the longer timer and gate what each effect names.</summary>
    [TestFixture]
    [Category("Ships")]
    public class BehaviourModsEditModeTests
    {
        private const string PoolPath = "Assets/Settings/Weapons/Mods/LullMods.asset";

        private readonly List<Object> made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in made) Object.DestroyImmediate(item);
            made.Clear();
        }

        private static T Prefab<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(prefab, path);
            return prefab;
        }

        private static WeaponComponent Weapon(string name) => Prefab<WeaponComponent>($"Assets/Prefabs/Weapons/{name}.prefab");

        private static Keystone Kit(string name) => Prefab<Keystone>($"Assets/Prefabs/Keystones/{name}.prefab");

        private WeaponBehaviourMod WeaponKey(string name, params WeaponComponent[] fits)
        {
            var mod = ScriptableObject.CreateInstance<WeaponBehaviourMod>();
            mod.name = name;
            mod.Configure(name, $"{name} effect", fits);
            made.Add(mod);
            return mod;
        }

        private KeystoneMod KeystoneKey(string name, params Keystone[] fits)
        {
            var mod = ScriptableObject.CreateInstance<KeystoneMod>();
            mod.name = name;
            mod.Configure(name, $"{name} effect", fits);
            made.Add(mod);
            return mod;
        }

        private PassiveMod Passive(string name)
        {
            var mod = ScriptableObject.CreateInstance<PassiveMod>();
            mod.name = name;
            mod.Configure(name, $"{name} effect");
            made.Add(mod);
            return mod;
        }

        [Test]
        public void EachKind_LandsOnlyOnThePartsItLists()
        {
            var lasers = Weapon("Lasers");
            var blink = Kit("Crimson_Blink");
            var bankShot = WeaponKey("Bank shot", lasers);
            var recall = KeystoneKey("Recall", blink);
            var backstab = Passive("Backstab");

            Assert.IsTrue(bankShot.LandsOn(lasers));
            Assert.IsFalse(bankShot.LandsOn(Weapon("Scattergun")), "an unlisted weapon");
            Assert.IsFalse(bankShot.LandsOn((WeaponComponent)null), "an empty mount");
            Assert.IsFalse(bankShot.LandsOn(blink) || bankShot.LandsOnShip, "a weapon key lands on weapons only");

            Assert.IsTrue(recall.LandsOn(blink));
            Assert.IsFalse(recall.LandsOn(Kit("Vanguard_Dash")), "another ship's kit");
            Assert.IsFalse(recall.LandsOn(lasers) || recall.LandsOnShip, "a keystone key lands on its kit only");

            Assert.IsTrue(backstab.LandsOnShip);
            Assert.IsFalse(backstab.LandsOn(lasers) || backstab.LandsOn(blink), "a passive lands on the ship, not a part");
            Assert.AreEqual("Backstab effect", backstab.Effect, "the card's effect line is authored text");
        }

        [Test]
        public void AKeyListingNoPart_ThrowsWhenAsked()
        {
            Assert.Throws<InvalidOperationException>(() => WeaponKey("Unlisted").LandsOn(Weapon("Lasers")));
            Assert.Throws<InvalidOperationException>(() => KeystoneKey("Unlisted").LandsOn(Kit("Crimson_Blink")));
        }

        [Test]
        public void HeldBehaviours_TellsItsConsumers_OnlyWhenTheSetChanges()
        {
            var lasers = Weapon("Lasers");
            var bankShot = WeaponKey("Bank shot", lasers);
            var held = new HeldBehaviours();
            var told = 0;
            held.Changed += () => told++;

            Assert.IsFalse(held.Holds(bankShot));
            held.Set(new BehaviourMod[] { bankShot });
            Assert.IsTrue(held.Holds(bankShot));
            Assert.AreEqual(1, told);

            held.Set(new BehaviourMod[] { bankShot });
            Assert.AreEqual(1, told, "the same set again tells nobody");

            held.Set(Array.Empty<BehaviourMod>());
            Assert.IsFalse(held.Holds(bankShot));
            Assert.AreEqual(2, told);
            Assert.Throws<ArgumentNullException>(() => held.Holds(null));
        }

        [Test]
        public void TheStack_HandsEachPartTheHeldKeysThatLandOnIt_OnceEach()
        {
            var lasers = Weapon("Lasers");
            var missiles = Weapon("Missiles");
            var bankShot = WeaponKey("Bank shot", lasers);
            var wireGuided = WeaponKey("Wire guided", missiles);
            var backstab = Passive("Backstab");
            var hot = ScriptableObject.CreateInstance<WeaponMod>();
            made.Add(hot);
            hot.Configure("Hot rounds", WeaponModField.Damage, .25f);
            var stack = new LullModStack();
            stack.Add(bankShot);
            stack.Add(bankShot);
            stack.Add(wireGuided);
            stack.Add(backstab);
            stack.Add(hot);

            CollectionAssert.AreEqual(new BehaviourMod[] { bankShot }, stack.Behaviours(mod => mod.LandsOn(lasers)));
            CollectionAssert.AreEqual(new BehaviourMod[] { backstab }, stack.Behaviours(mod => mod.LandsOnShip));
            CollectionAssert.AreEqual(new[] { "Bank shot ×2", "Hot rounds" },
                stack.Using(lasers).Select(held => held.Label), "a weapon offer's card lists the keys it would use");
            CollectionAssert.AreEqual(new[] { "Bank shot ×2" },
                stack.IdledBy(lasers, Weapon("Scattergun"), missiles).Select(held => held.Label),
                "swapping the lasers out idles their key");
        }

        [Test]
        public void TheShippedPool_HoldsOnlyBehaviourMods_EachLandingSomewhere()
        {
            var pool = AssetDatabase.LoadAssetAtPath<LullModPool>(PoolPath);
            var statKinds = new[] { typeof(WeaponMod), typeof(ShipMod) };
            Assert.IsFalse(pool.mods.Any(mod => statKinds.Contains(mod.GetType())),
                "no stat mod is left in the lull pool");

            CollectionAssert.AreEquivalent(new[]
            {
                "Bank shot", "Stopping power", "Deep strike", "Shrapnel rocks", "Wire guided", "Implosion",
                "Battering ram", "Ambush", "Recall", "Orbit", "Riposte", "Backstab",
                "Point defence", "Suppression", "EMP warhead", "Limpet",
                "Headstrong", "Turret", "Lie in wait", "Duck and load",
                "Chain ram", "Double dash", "Shield bash", "Relentless", "Silent running", "Quiet ordnance",
            }, pool.mods.Select(mod => mod.DisplayName));

            var weapons = new[] { "Lasers", "TurboLasers", "Rippers", "Scattergun", "Railgun", "Missiles", "Grenades" }.Select(Weapon).ToArray();
            var kits = new[] { "Bulwark_Dash", "Nightshade_Stealth", "Crimson_Blink", "Valis_Overdrive", "Vanguard_Dash" }
                .Select(Kit).ToArray();
            foreach (var mod in pool.mods.Cast<BehaviourMod>())
            {
                Assert.IsNotEmpty(mod.Effect, $"{mod.name} prints an effect line");
                Assert.IsTrue(mod.LandsOnShip || weapons.Any(weapon => mod.LandsOn(weapon)) || kits.Any(kit => mod.LandsOn(kit)),
                    $"{mod.name} lands on a player weapon, a player kit or the ship");
            }
        }

        [Test]
        public void StatusEffects_TimeOut_AndReapplyingKeepsTheLongerTimer()
        {
            var status = new StatusEffects();
            status.Apply(StatusEffect.Stunned, 2f);
            status.Tick(1f);
            status.Apply(StatusEffect.Stunned, .5f);
            Assert.AreEqual(1f, status.Remaining(StatusEffect.Stunned), 1e-5f, "a shorter re-apply leaves the longer timer");

            status.Apply(StatusEffect.Stunned, 3f);
            Assert.AreEqual(3f, status.Remaining(StatusEffect.Stunned), 1e-5f, "a longer re-apply extends it");

            status.Tick(2.9f);
            Assert.IsTrue(status.Has(StatusEffect.Stunned));
            status.Tick(.2f);
            Assert.IsFalse(status.Has(StatusEffect.Stunned), "the effect times out");
            Assert.AreEqual(0f, status.Remaining(StatusEffect.Stunned));
            Assert.Throws<ArgumentOutOfRangeException>(() => status.Apply(StatusEffect.EnginesOut, 0f));
        }

        [TestCase(StatusEffect.Stunned, false, false, false, false)]
        [TestCase(StatusEffect.WeaponsJammed, true, true, false, true)]
        [TestCase(StatusEffect.EnginesOut, false, true, true, true)]
        [TestCase(StatusEffect.Suppressed, true, true, true, false)]
        public void EachEffect_GatesWhatItNames(StatusEffect effect, bool thrust, bool turn, bool fire, bool keystone)
        {
            var status = new StatusEffects();
            Assert.IsTrue(status.CanThrust && status.CanTurn && status.CanFire && status.CanLock && status.CanKeystone,
                "a clear ship can do everything");

            status.Apply(effect, 1f);
            Assert.AreEqual((thrust, turn, fire, fire, keystone),
                (status.CanThrust, status.CanTurn, status.CanFire, status.CanLock, status.CanKeystone));

            status.Clear();
            Assert.IsFalse(status.Has(effect), "a clear lifts every effect");
        }
    }
}
