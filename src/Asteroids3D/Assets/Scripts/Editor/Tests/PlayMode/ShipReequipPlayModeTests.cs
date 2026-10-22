using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Verifies <see cref="Ship.Reequip"/> swaps modules and re-resolves the ship: a shield swap
    /// updates the resolved shield cap and refills vitals, and the geometry-derived collision radius
    /// carries across. This is the runtime substrate the hangar's between-run loadout change
    /// (the hangar step → <see cref="Game.PlayerRig.ApplyLoadout"/>) drives.
    /// </summary>
    public class ShipReequipPlayModeTests : PlayModeWorldFixture
    {
        private const string BulwarkShieldPath = "Assets/Settings/Ships/Shields/Bulwark_Shield.asset";

        private Ship ship;

        /// <summary>The ship's current weapon modules — "keep weapons as they are" for module swaps.</summary>
        private static Combat.Weapons.WeaponComponent CurrentPrimary(Ship s) =>
            s.Weapons ? s.Weapons.PrimaryMountPrefab : null;
        private static Combat.Weapons.WeaponComponent CurrentSecondary(Ship s) =>
            s.Weapons ? s.Weapons.SecondaryMountPrefab : null;

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Reequip_Shield_UpdatesResolvedShieldCap()
        {
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            Assert.IsNotNull(ship, "ship created");
            yield return null; // let Awake/Initialize/Start settle

            var bulwark = TestAssets.Load<ShieldModule>(BulwarkShieldPath);
            Assert.IsNotNull(bulwark, "bulwark shield asset loaded");
            Assert.AreNotEqual(ship.Stats.maxShield, bulwark.maxShield, "bulwark differs from the baseline shield cap");
            var baselineRadius = ship.Stats.shipRadius;

            ship.Reequip(bulwark, CurrentPrimary(ship), CurrentSecondary(ship));
            Assert.AreEqual(bulwark.maxShield, ship.Stats.maxShield, 1e-3f, "stats reflect the bulwark shield cap");
            Assert.AreEqual(1f, ship.ShieldPct, 1e-3f, "vitals refill on a between-run equip swap");
            Assert.AreEqual(baselineRadius, ship.Stats.shipRadius, 1e-4f,
                "collision radius is geometry-derived and carries across a module swap");
        }
    }
}
