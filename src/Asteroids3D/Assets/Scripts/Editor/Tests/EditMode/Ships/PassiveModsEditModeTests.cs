using System.Collections.Generic;
using System.Reflection;
using Asteroids;
using Asteroids.Fragnetics;
using Damage;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Mods;
using Substrate;
using Tests.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Ships
{
    /// <summary>The pitch passives with no physics: Backstab skips the shield only on the rear arc, Shrapnel rocks throws fragments away from the thrower and boosts their hits on other ships, and a lull pick never re-offers a held behaviour key.</summary>
    [TestFixture]
    [Category("Ships")]
    public class PassiveModsEditModeTests
    {
        private static readonly MethodInfo ShipAwake =
            typeof(Ship).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in made) Object.DestroyImmediate(item);
            made.Clear();
        }

        private PassiveMod Passive(string name)
        {
            var mod = ScriptableObject.CreateInstance<PassiveMod>();
            mod.name = name;
            mod.Configure(name, $"{name} effect");
            made.Add(mod);
            return mod;
        }

        private Ship BareShip(Vector3 position)
        {
            var go = new GameObject("BareShip", typeof(Ship));
            made.Add(go);
            go.transform.SetPositionAndRotation(position, GamePlane.Rotation);
            var ship = go.GetComponent<Ship>();
            ShipAwake.Invoke(ship, null);
            ship.Damage.PopulateSettings(new ResolvedShipStats
            {
                maxHealth = 100f, maxShield = 50f, shieldRegenRate = 10f, shieldRegenDelay = 1f,
            });
            return ship;
        }

        private static HeldBehaviours Holding(PassiveMod key)
        {
            var held = new HeldBehaviours();
            held.Set(new BehaviourMod[] { key });
            return held;
        }

        private static Vector3 OffTail(Transform victim, float degrees) =>
            victim.position + Quaternion.AngleAxis(degrees, GamePlane.Normal) * -victim.up * 2f;

        [Test]
        public void Backstab_PiercesOnlyInsideTheRearArc_AndOnlyWhenHeld()
        {
            var key = Passive("Backstab");
            var backstab = new Backstab();
            backstab.Configure(key, 60f);
            var victim = BareShip(new Vector3(5f, 3f, 0f)).transform;

            backstab.Read(new HeldBehaviours());
            Assert.IsFalse(backstab.Pierces(victim, OffTail(victim, 0f)), "not held: no hit pierces");

            backstab.Read(Holding(key));
            Assert.IsTrue(backstab.Pierces(victim, OffTail(victim, 0f)), "dead astern");
            Assert.IsTrue(backstab.Pierces(victim, OffTail(victim, 55f)), "inside the arc");
            Assert.IsTrue(backstab.Pierces(victim, OffTail(victim, -55f)), "inside the arc, the other side");
            Assert.IsFalse(backstab.Pierces(victim, OffTail(victim, 65f)), "just outside the arc");
            Assert.IsFalse(backstab.Pierces(victim, OffTail(victim, 180f)), "the nose");
        }

        [Test]
        public void ABackstabbersRearHit_SkipsTheShield_AndAFrontHitDoesNot()
        {
            var key = Passive("Backstab");
            var attacker = BareShip(Vector3.zero);
            attacker.Backstab.Configure(key, 60f);
            attacker.Backstab.Read(Holding(key));
            var victim = BareShip(new Vector3(0f, 20f, 0f));

            DamageInfo From(Vector3 point) =>
                new(20f, DamageKind.Laser, attacker.Id, 0.1f, Vector3.zero, point, attacker: attacker);

            victim.Damage.TakeDamage(From(OffTail(victim.transform, 0f)));
            Assert.AreEqual((50f, 80f), (victim.Damage.Shield.CurrentValue, victim.Damage.Health.CurrentValue),
                "a rear hit goes straight to the hull");

            victim.Damage.TakeDamage(From(OffTail(victim.transform, 180f)));
            Assert.AreEqual((30f, 80f), (victim.Damage.Shield.CurrentValue, victim.Damage.Health.CurrentValue),
                "a front hit meets the shield");
        }

        [Test]
        public void ShrapnelRocks_ThrowsFragmentsAwayFromTheThrower_WithMoreSpread()
        {
            var shrapnel = new ShrapnelRocks();
            shrapnel.Configure(Passive("Shrapnel rocks"), spreadScale: 2f, throwSpeed: 10f, enemyDamageScale: 3f);
            var rock = new Vector3(1f, 0f, 0f);
            var frags = new[]
            {
                new Frag(1f, Vector3.zero, Quaternion.identity) { Velocity = new Vector3(4f, 0f, 0f) },
                new Frag(1f, Vector3.zero, Quaternion.identity) { Velocity = new Vector3(1f, -3f, 0f) },
            };

            shrapnel.Throw(frags, rock, Vector3.up);

            Assert.AreEqual(new Vector3(7f, 10f, 0f), frags[0].Velocity, "spread doubled about the rock, thrown 10 m/s away");
            Assert.AreEqual(new Vector3(1f, 4f, 0f), frags[1].Velocity);
        }

        [Test]
        public void AThrownFragment_HitsOtherShipsHarder_ButNotItsThrower()
        {
            var key = Passive("Shrapnel rocks");
            var thrower = BareShip(Vector3.zero);
            thrower.ShrapnelRocks.Configure(key, 2f, 10f, 3f);
            var enemy = BareShip(new Vector3(0f, 20f, 0f));
            var fragment = TestRocks.Spawn(Vector2.zero);
            made.Add(fragment.gameObject);
            var law = new CollisionDamageLaw(graceDeltaV: 5f, damagePerDeltaV: 3f, lethality: 1f);
            fragment.Damage.Initialize(10f, law);

            Assert.AreEqual(15f, fragment.Damage.DamageTo(enemy.Damage, 10f), 1e-4f, "an ordinary rock");

            fragment.Damage.ThrownBy(thrower);
            Assert.AreEqual(45f, fragment.Damage.DamageTo(enemy.Damage, 10f), 1e-4f, "thrown at an enemy");
            Assert.AreEqual(15f, fragment.Damage.DamageTo(thrower.Damage, 10f), 1e-4f, "the thrower takes the plain knock");

            fragment.Damage.Initialize(10f, law);
            Assert.AreEqual(15f, fragment.Damage.DamageTo(enemy.Damage, 10f), 1e-4f, "a respawned rock is ordinary again");
        }

        [Test]
        public void ALullPick_NeverReoffersAHeldBehaviourKey_ButStillOffersAStatMod()
        {
            var ship = BareShip(Vector3.zero);
            var backstab = Passive("Backstab");
            var shrapnel = Passive("Shrapnel rocks");
            var hull = ScriptableObject.CreateInstance<ShipMod>();
            made.Add(hull);
            hull.Configure("Hull plating", ShipModField.Hull, 0.25f);
            var stack = new LullModStack();
            stack.Add(backstab);
            stack.Add(hull);

            Assert.IsFalse(stack.Offers(backstab, ship), "a second copy would do nothing");
            Assert.IsTrue(stack.Offers(shrapnel, ship), "an unheld key fits any ship");
            Assert.IsTrue(stack.Offers(hull, ship), "a stat mod stacks");

            var pool = new List<LullMod> { backstab, shrapnel, hull };
            CollectionAssert.AreEquivalent(new LullMod[] { shrapnel, hull },
                LootOffers.Draw(pool, mod => stack.Offers(mod, ship), 3, _ => 0));
        }
    }
}
