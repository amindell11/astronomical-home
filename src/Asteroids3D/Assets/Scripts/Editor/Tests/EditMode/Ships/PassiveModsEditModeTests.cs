using System.Collections.Generic;
using System.Reflection;
using Asteroids;
using Asteroids.Fragnetics;
using Damage;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using Ships.Loadout;
using Ships.Mods;
using Ships.Registry;
using Combat.Targeting;
using Substrate;
using Tests.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Ships
{
    /// <summary>The passives with no physics: Backstab skips the shield only on the rear arc, Shrapnel rocks throws fragments away from the thrower and boosts their hits on other ships, Headstrong doubles the front shield and drops the rear one, Turret slows the turn and swings a socket to the aim point, Duck and load speeds recovery only while unsighted, concealment holds until every hider lets go, and a lull pick never re-offers a held behaviour key.</summary>
    [TestFixture]
    [Category("Ships")]
    public class PassiveModsEditModeTests
    {
        private static readonly MethodInfo ShipAwake =
            typeof(Ship).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo DamageAwake =
            typeof(DamageController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

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
        public void AHeadstrongShip_ShieldsDoubleOnTheFrontArc_AndNotAtAllBehind()
        {
            var key = Passive("Headstrong");
            var ship = BareShip(new Vector3(0f, 20f, 0f));
            DamageAwake.Invoke(ship.Damage, null);
            ship.Damage.PopulateSettings(new ResolvedShipStats
            {
                maxHealth = 100f, maxShield = 50f, shieldRegenRate = 10f, shieldRegenDelay = 1f,
            });
            ship.Headstrong.Configure(key, 90f, 2f);
            DamageInfo At(float amount, Vector3 point) => new(amount, DamageKind.Laser, ShipId.Invalid, 0.1f, Vector3.zero, point);
            var nose = ship.transform.position + ship.transform.up * 2f;
            var tail = ship.transform.position - ship.transform.up * 2f;

            ship.Damage.TakeDamage(At(20f, nose));
            Assert.AreEqual((30f, 100f), (ship.Damage.Shield.CurrentValue, ship.Damage.Health.CurrentValue),
                "not held: the shield takes a front hit point for point");

            ship.Headstrong.Read(Holding(key));
            ship.Damage.TakeDamage(At(20f, nose));
            Assert.AreEqual((20f, 100f), (ship.Damage.Shield.CurrentValue, ship.Damage.Health.CurrentValue),
                "held: each shield point absorbs two from the front");

            ship.Damage.TakeDamage(At(20f, tail));
            Assert.AreEqual((20f, 80f), (ship.Damage.Shield.CurrentValue, ship.Damage.Health.CurrentValue),
                "a rear hit skips the shield");

            ship.Damage.TakeDamage(At(60f, nose));
            Assert.AreEqual((0f, 60f), (ship.Damage.Shield.CurrentValue, ship.Damage.Health.CurrentValue),
                "a front hit past the doubled shield spills the rest onto the hull");
        }

        [Test]
        public void Turret_SlowsTheTurn_HardensReverse_AndSwingsASocketToTheAimPoint()
        {
            var key = Passive("Turret");
            var turret = new Turret();
            turret.Configure(key, 0.5f, 2.5f);
            var socket = new GameObject("Socket").transform;
            made.Add(socket.gameObject);
            socket.SetPositionAndRotation(new Vector3(1f, 1f, 0f), GamePlane.Rotation);
            var nose = socket.up;
            var target = socket.position + GamePlane.Right * 10f;

            turret.Read(new HeldBehaviours());
            turret.Aim(socket, target);
            Assert.AreEqual(0f, Vector3.Angle(nose, socket.up), 1e-3f, "not held: the socket keeps to the nose");

            turret.Read(Holding(key));
            var stats = new ResolvedShipStats { maxYawRate = 200f, yawTorque = 8000f, reverseForce = 2000f, forwardForce = 6000f };
            turret.Scale(stats);
            Assert.AreEqual((100f, 4000f, 5000f, 6000f),
                (stats.maxYawRate, stats.yawTorque, stats.reverseForce, stats.forwardForce));

            turret.Aim(socket, target);
            Assert.AreEqual(0f, Vector3.Angle(GamePlane.Right, GamePlane.WorldDirOnPlane(socket.up)), 1e-2f,
                "held: the socket points at the aim point");
        }

        [Test]
        public void DuckAndLoad_SpeedsRecovery_OnlyOnceNoEnemyHasSightedTheShipLately()
        {
            var key = Passive("Duck and load");
            var duck = new DuckAndLoad();
            duck.Configure(key, 3f, 0.5f);
            var sight = new LockChannel();

            Assert.AreEqual(1f, duck.RecoveryScale(sight, 10f), "not held");
            duck.Read(Holding(key));
            Assert.AreEqual(3f, duck.RecoveryScale(sight, 10f), "never sighted");
            sight.NoteSighted(10f);
            Assert.AreEqual(1f, duck.RecoveryScale(sight, 10.3f), "sighted just now");
            Assert.AreEqual(3f, duck.RecoveryScale(sight, 10.5f), "unseen long enough");
        }

        [Test]
        public void Concealment_HoldsUntilEveryHiderLetsGo_AndAStrayRevealThrows()
        {
            var sight = new LockChannel();
            var told = 0;
            sight.Concealed += () => told++;

            sight.Conceal();
            sight.Conceal();
            Assert.AreEqual(1, told, "the first hider conceals the target");
            sight.Reveal();
            Assert.IsTrue(sight.IsConcealed, "one hider still holds it");
            sight.Reveal();
            Assert.IsFalse(sight.IsConcealed);
            Assert.Throws<System.InvalidOperationException>(sight.Reveal);
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
