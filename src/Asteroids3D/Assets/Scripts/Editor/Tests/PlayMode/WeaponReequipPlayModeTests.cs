using System.Collections;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using Combat.Weapons.Conditions;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// Weapons are first-class loadout slots: <see cref="Ship.Reequip"/> swaps the mounted weapon
    /// prefabs like it swaps engine/shield modules. Changed slots replace their mount instances
    /// (unchanged slots keep them — an unedited apply is a no-op), while the weapon context object
    /// survives (refreshed in place) so commander- and HUD-held references stay valid, and the
    /// ship's targeting cache follows the missile mount in and out. Also covers the death path:
    /// a reequip on a deactivated ship must survive the later revive.
    /// </summary>
    [Category("Weapons")]
    public class WeaponReequipPlayModeTests : PlayModeWorldFixture
    {
        private const string LasersPrefabPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const string RippersPrefabPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string JunkerPrefabPath = "Assets/Prefabs/Ships/Cargo Ships/Junker_1.prefab";

        private Ship ship;

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        private static T LoadWeapon<T>(string path) where T : WeaponComponent
        {
#if UNITY_EDITOR
            var weapon = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(weapon, $"Failed to load weapon prefab at {path}");
            return weapon;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator Reequip_Weapons_ReplacesMounts_AndContextSurvivesInPlace()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            var oldPrimary = ship.Weapons.Primary;
            var contextBefore = ship.Weapons.ReadoutContext;
            var commanderView = ship.Weapons.Context;

            var rippers = LoadWeapon<Rippers>(RippersPrefabPath);
            var lasers = LoadWeapon<Lasers>(LasersPrefabPath);
            ship.Reequip(ship.Engine, ship.Shield, rippers, lasers);

            Assert.IsInstanceOf<Rippers>(ship.Weapons.Primary, "primary mount replaced by the ripper");
            Assert.IsInstanceOf<Lasers>(ship.Weapons.Secondary, "secondary mount replaced by the laser");
            Assert.AreSame(rippers, ship.Weapons.PrimaryMountPrefab, "source prefab tracks the new module");

            // The context object commanders/HUD hold is the SAME instance, refreshed in place.
            Assert.AreSame(contextBefore, ship.Weapons.ReadoutContext, "readout context survives the swap");
            Assert.AreSame(commanderView, ship.Weapons.Context, "commander context survives the swap");
            Assert.AreEqual(2, commanderView.Slots.Count);
            Assert.AreEqual(ship.Weapons.Primary.ProjectileSpeed, commanderView.ProjectileSpeed(WeaponSlot.Primary),
                "per-slot data reads through to the new mount");

            // The new primary (ripper) exposes an ammo readout where the old laser had heat.
            var primaryReadouts = contextBefore.Readouts(WeaponSlot.Primary);
            var hasAmmo = false;
            for (var i = 0; i < primaryReadouts.Count; i++)
                if (primaryReadouts[i] is IAmmoReadout) hasAmmo = true;
            Assert.IsTrue(hasAmmo, "primary readouts regenerated from the new mount");

            yield return null; // deferred Destroy completes
            Assert.IsTrue(!oldPrimary, "old mount instance destroyed");
        }

        [UnityTest]
        public IEnumerator Reequip_TargetingCache_FollowsTheMissileMount()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            Assert.IsNotNull(ship.Targeting, "default loadout (missiles in secondary) has a lock sensor");

            // Swap the missiles away → the ship no longer has a lock sensor.
            var lasers = LoadWeapon<Lasers>(LasersPrefabPath);
            var rippers = LoadWeapon<Rippers>(RippersPrefabPath);
            ship.Reequip(ship.Engine, ship.Shield, lasers, rippers);
            Assert.IsNull(ship.Targeting, "targeting cache cleared when no mount carries a sensor");

            // Swap missiles back in → the cache points at the NEW mount's sensor, ready for re-wiring.
            var missiles = LoadWeapon<Missiles>(MissilesPrefabPath);
            ship.Reequip(ship.Engine, ship.Shield, lasers, missiles);
            Assert.IsNotNull(ship.Targeting, "targeting cache found the fresh missile mount's sensor");
            Assert.AreSame(ship.Targeting, ship.Weapons.Secondary.GetComponent<Combat.Targeting.LockOnSensor>(),
                "cache points at the newly instantiated sensor, not a stale one");
        }

        [UnityTest]
        public IEnumerator Reequip_EmptyWeaponSlots_IsAValidBuild()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            ship.Reequip(ship.Engine, ship.Shield, null, null);

            Assert.IsNull(ship.Weapons.Primary, "empty primary slot");
            Assert.IsNull(ship.Weapons.Secondary, "empty secondary slot");
            Assert.AreEqual(0, ship.Weapons.Context.Slots.Count, "context reports no equipped slots");
            Assert.IsNull(ship.Targeting, "no sensor on an unarmed build");
        }

        [UnityTest]
        public IEnumerator Reequip_UnchangedModules_KeepsMountInstances()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            var primaryBefore = ship.Weapons.Primary;
            var secondaryBefore = ship.Weapons.Secondary;
            var sensorBefore = ship.Targeting;

            // The seeded "unedited hangar" apply: same modules back in.
            ship.Reequip(ship.Engine, ship.Shield,
                ship.Weapons.PrimaryMountPrefab, ship.Weapons.SecondaryMountPrefab);
            yield return null; // a (wrong) deferred mount destroy would land here

            Assert.IsTrue(primaryBefore, "unchanged primary mount not destroyed");
            Assert.AreSame(primaryBefore, ship.Weapons.Primary, "unchanged primary keeps its live mount instance");
            Assert.AreSame(secondaryBefore, ship.Weapons.Secondary, "unchanged secondary keeps its live mount instance");
            Assert.AreSame(sensorBefore, ship.Targeting, "targeting cache still points at the same live sensor");
        }

        [UnityTest]
        public IEnumerator Reequip_WhileInactive_AwakesCleanlyOnRevive()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            // Death path: the ship is deactivated on death, THEN the hangar applies a new loadout, so the fresh mounts instantiate under an inactive parent (Awake deferred).
            ship.gameObject.SetActive(false);

            var rippers = LoadWeapon<Rippers>(RippersPrefabPath);
            var missiles = LoadWeapon<Missiles>(MissilesPrefabPath);
            ship.Reequip(ship.Engine, ship.Shield, rippers, missiles);

            Assert.IsNotNull(ship.Targeting, "sensor tracked from the new mounts even while inactive");
            Assert.AreEqual(0, ship.Weapons.ReadoutContext.Readouts(WeaponSlot.Primary).Count,
                "pre-Awake readouts read empty rather than throwing");

            // The production revive path resets weapons BEFORE reactivating, so it must tolerate never-awakened mounts.
            ship.ResetShip();
            yield return null;

            Assert.IsTrue(ship.gameObject.activeSelf, "ship revived");
            Assert.Greater(ship.Weapons.ReadoutContext.Readouts(WeaponSlot.Primary).Count, 0,
                "readouts built after Awake — the empty pre-Awake read did not stick");
            Assert.AreSame(ship.Targeting, ship.Weapons.Secondary.GetComponent<Combat.Targeting.LockOnSensor>(),
                "targeting cache points at the live missile sensor after revive");
        }

        [UnityTest]
        public IEnumerator Reequip_UnarmedChassis_IgnoresWeaponSlots()
        {
            var junkerPrefab = TestAssets.LoadShipPrefab(JunkerPrefabPath);
            Assert.IsNotNull(junkerPrefab, "junker prefab loaded");
            ship = Object.Instantiate(junkerPrefab);
            ship.Initialize(0, 0, Projectiles); // commander-less; wires movement like Factory.CreateShip does
            yield return null;

            Assert.IsNull(ship.Weapons, "junker carries no WeaponsController");

            var lasers = LoadWeapon<Lasers>(LasersPrefabPath);
            var missiles = LoadWeapon<Missiles>(MissilesPrefabPath);
            ship.Reequip(ship.Engine, ship.Shield, lasers, missiles);

            Assert.IsNull(ship.Weapons, "weapon slots ignored — the chassis decides which slots exist");
            Assert.IsNull(ship.Targeting, "no sensor appears on an unarmed chassis");
        }
    }
}
