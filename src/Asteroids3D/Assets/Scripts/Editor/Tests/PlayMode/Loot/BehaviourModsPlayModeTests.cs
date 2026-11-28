#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Ships.Mods;
using Ships.Status;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Loot
{
    /// <summary>
    /// On a live ship, a behaviour mod fits only while the ship carries a part it lands on; a bound
    /// ship hands each mount, its keystone and itself the held keys that land there on every pick,
    /// and a swap idles a weapon key the new mount doesn't take. A stunned ship answers neither the
    /// pilot nor the trigger, through the same actuators an AI commander drives, until the stun ends.
    /// </summary>
    [Category("Ships")]
    public class BehaviourModsPlayModeTests : PlayModeWorldFixture
    {
        private const string LasersPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const string ScattergunPath = "Assets/Prefabs/Weapons/Scattergun.prefab";
        private const string MissilesPath = "Assets/Prefabs/Weapons/Missiles.prefab";

        private Ship ship;
        private readonly List<Object> made = new();

        public override void TearDown()
        {
            if (ship) Object.DestroyImmediate(ship.gameObject);
            foreach (var item in made) Object.DestroyImmediate(item);
            made.Clear();
            base.TearDown();
        }

        private Ship SpawnCrimson(WeaponComponent primary, WeaponComponent secondary)
        {
            ship = Object.Instantiate(TestAssets.LoadCrimsonPrefab(), Vector3.zero, GamePlane.Rotation);
            ship.Initialize(0, 0, Projectiles);
            ship.Reequip(primary, secondary);
            return ship;
        }

        private T Made<T>(T item) where T : Object
        {
            made.Add(item);
            return item;
        }

        private WeaponBehaviourMod WeaponKey(string name, params WeaponComponent[] fits)
        {
            var mod = Made(ScriptableObject.CreateInstance<WeaponBehaviourMod>());
            mod.name = name;
            mod.Configure(name, "effect", fits);
            return mod;
        }

        private KeystoneMod KeystoneKey(string name, Keystone kit)
        {
            var mod = Made(ScriptableObject.CreateInstance<KeystoneMod>());
            mod.name = name;
            mod.Configure(name, "effect", kit);
            return mod;
        }

        private PassiveMod Passive(string name)
        {
            var mod = Made(ScriptableObject.CreateInstance<PassiveMod>());
            mod.name = name;
            mod.Configure(name, "effect");
            return mod;
        }

        [Test]
        public void AnOffer_FitsOnlyTheCurrentShipAndLoadout()
        {
            var lasers = TestAssets.Load<WeaponComponent>(LasersPath);
            var scattergun = TestAssets.Load<WeaponComponent>(ScattergunPath);
            var missiles = TestAssets.Load<WeaponComponent>(MissilesPath);
            SpawnCrimson(lasers, missiles);
            var bankShot = WeaponKey("Bank shot", lasers);
            var stoppingPower = WeaponKey("Stopping power", scattergun);
            var recall = KeystoneKey("Recall", ship.KeystonePrefab);
            var riposte = KeystoneKey("Riposte", TestAssets.Load<Keystone>("Assets/Prefabs/Keystones/Vanguard_Dash.prefab"));

            Assert.IsTrue(bankShot.Fits(ship));
            Assert.AreEqual(ship.Weapons.Primary.DisplayName, bankShot.Landing(ship));
            Assert.IsFalse(stoppingPower.Fits(ship), "no scattergun carried");
            Assert.IsTrue(recall.Fits(ship));
            Assert.AreEqual("Blink", recall.Landing(ship));
            Assert.IsFalse(riposte.Fits(ship), "another ship's keystone");
            Assert.IsTrue(Passive("Backstab").Fits(ship), "a passive fits any ship");

            ship.Reequip(scattergun, missiles);
            Assert.IsFalse(bankShot.Fits(ship), "the lasers left");
            Assert.IsTrue(stoppingPower.Fits(ship));
        }

        [Test]
        public void APick_ReachesItsPartAtOnce_AndASwapIdlesAKeyTheNewMountDoesntTake()
        {
            var lasers = TestAssets.Load<WeaponComponent>(LasersPath);
            var scattergun = TestAssets.Load<WeaponComponent>(ScattergunPath);
            SpawnCrimson(lasers, TestAssets.Load<WeaponComponent>(MissilesPath));
            var bankShot = WeaponKey("Bank shot", lasers);
            var recall = KeystoneKey("Recall", ship.KeystonePrefab);
            var backstab = Passive("Backstab");
            var stack = new LullModStack();
            ship.BindMods(stack);
            var weaponTold = 0;
            var keystoneTold = 0;
            var shipTold = 0;
            ship.Weapons.Primary.Behaviours.Changed += () => weaponTold++;
            ship.Keystone.Behaviours.Changed += () => keystoneTold++;
            ship.Behaviours.Changed += () => shipTold++;

            stack.Add(bankShot);
            Assert.IsTrue(ship.Weapons.Primary.Behaviours.Holds(bankShot), "the pick lands on the lasers at once");
            Assert.AreEqual((1, 0, 0), (weaponTold, keystoneTold, shipTold), "only the part it lands on is told");
            Assert.IsFalse(ship.Weapons.Secondary.Behaviours.Holds(bankShot));

            stack.Add(recall);
            stack.Add(backstab);
            Assert.IsTrue(ship.Keystone.Behaviours.Holds(recall));
            Assert.IsTrue(ship.Behaviours.Holds(backstab));
            Assert.IsFalse(ship.Keystone.Behaviours.Holds(backstab) || ship.Behaviours.Holds(recall));
            Assert.AreEqual((1, 1, 1), (weaponTold, keystoneTold, shipTold));

            ship.Reequip(scattergun, ship.Weapons.SecondaryMountPrefab);
            Assert.IsFalse(ship.Weapons.Primary.Behaviours.Holds(bankShot), "the scattergun doesn't take Bank shot");
            CollectionAssert.Contains(stack.PickedNames(), bankShot.name, "the key stays held, idle");

            ship.Reequip(lasers, ship.Weapons.SecondaryMountPrefab);
            Assert.IsTrue(ship.Weapons.Primary.Behaviours.Holds(bankShot), "remounting the lasers wakes it");
        }

        [UnityTest]
        public IEnumerator AStunnedShip_NeitherMovesNorFires_UntilTheStunEnds()
        {
            SpawnCrimson(TestAssets.Load<WeaponComponent>(LasersPath), null);
            yield return new WaitForFixedUpdate();
            var fired = 0;
            ship.Weapons.Fired += _ => fired++;
            var weapons = ship.Weapons.Arm(Projectiles);
            ship.Status.Apply(StatusEffect.Stunned, 2f);

            for (var i = 0; i < 5; i++)
            {
                ship.Movement.Drive(new PilotCommand { thrust = 1f, strafe = 1f, boost = 1f, yawTorque = 1f });
                weapons.Fire(WeaponSlot.Primary, new WeaponCommand { held = true, pressed = i == 0 });
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(ship.Status.Has(StatusEffect.Stunned), "test premise: still stunned");
            Assert.Less(ship.Rigidbody.linearVelocity.magnitude, 1e-3f, "no thrust or strafe while stunned");
            Assert.Less(Mathf.Abs(Vector3.Dot(ship.Rigidbody.angularVelocity, GamePlane.Normal)), 1e-3f, "no turning");
            Assert.AreEqual(0, ship.Keystone.Activations, "no keystone");
            Assert.AreEqual(0, fired, "no shots");

            for (var i = 0; i < 400 && ship.Status.Has(StatusEffect.Stunned); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(ship.Status.Has(StatusEffect.Stunned), "the stun timed out");
            for (var i = 0; i < 3; i++)
            {
                ship.Movement.Drive(new PilotCommand { thrust = 1f, yawTorque = 1f });
                yield return new WaitForFixedUpdate();
            }
            weapons.Fire(WeaponSlot.Primary, new WeaponCommand { held = true, pressed = true });
            Assert.Greater(ship.Rigidbody.linearVelocity.magnitude, 1e-3f, "the engines answer once it ends");
            Assert.Greater(fired, 0, "and so does the trigger");
        }
    }
}
#endif
