using System.Collections;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Verifies <see cref="Ship.Reequip"/> swaps modules and re-resolves the ship end-to-end: the
    /// flattened <see cref="ResolvedShipStats"/>, the MPC <see cref="Movement.Dynamics"/>, and the live
    /// movement rigidbody all reflect the new engine, and a shield swap updates the resolved shield cap.
    /// This is the runtime substrate the hangar's between-run loadout change
    /// (<see cref="Game.Bootstrap.GameState.Hangar"/> → <see cref="Player.PlayerRig.ApplyLoadout"/>) drives.
    /// </summary>
    public class ShipReequipPlayModeTests : PlayModeWorldFixture
    {
        private const string RacerEnginePath = "Assets/Settings/Ships/Engines/Racer_Engine.asset";
        private const string HaulerEnginePath = "Assets/Settings/Ships/Engines/Hauler_Engine.asset";
        private const string BulwarkShieldPath = "Assets/Settings/Ships/Shields/Bulwark_Shield.asset";

        private Ship ship;

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Reequip_Engine_ReResolvesStatsDynamicsAndMovement()
        {
            ship = ShipTestFactory.CreateDefaultShip();
            Assert.IsNotNull(ship, "ship created");
            yield return null; // let Awake/Initialize/Start settle

            var racer = TestAssets.LoadEngineModule(RacerEnginePath);
            var hauler = TestAssets.LoadEngineModule(HaulerEnginePath);
            Assert.IsNotNull(racer, "racer engine asset loaded");
            Assert.IsNotNull(hauler, "hauler engine asset loaded");
            Assert.AreNotEqual(racer.maxSpeed, hauler.maxSpeed, "the two sidegrades differ in top speed");

            var baselineSpeed = ship.Stats.maxSpeed;
            var baselineRadius = ship.Stats.shipRadius;

            // Swap to the racer engine → every consumer picks up the new top speed.
            ship.Reequip(racer, ship.Shield);
            Assert.AreEqual(racer.maxSpeed, ship.Stats.maxSpeed, 1e-3f, "stats reflect racer top speed");
            Assert.AreEqual(racer.maxSpeed, ship.Dynamics.maxSpeed, 1e-3f, "MPC dynamics rebuilt with racer top speed");
            Assert.AreEqual(racer.maxSpeed, ship.Rigidbody.maxLinearVelocity, 1e-3f,
                "live rigidbody re-applied to racer top speed");
            Assert.AreNotEqual(baselineSpeed, ship.Stats.maxSpeed, "top speed actually changed from the baseline");
            Assert.AreEqual(baselineRadius, ship.Stats.shipRadius, 1e-4f,
                "collision radius is geometry-derived and carries across a module swap");

            // A second swap proves it is not a one-shot latch.
            ship.Reequip(hauler, ship.Shield);
            Assert.AreEqual(hauler.maxSpeed, ship.Stats.maxSpeed, 1e-3f, "stats reflect hauler top speed after 2nd swap");
            Assert.AreEqual(hauler.maxSpeed, ship.Rigidbody.maxLinearVelocity, 1e-3f,
                "rigidbody re-applied to hauler top speed after 2nd swap");
        }

        [UnityTest]
        public IEnumerator Reequip_Shield_UpdatesResolvedShieldCap()
        {
            ship = ShipTestFactory.CreateDefaultShip();
            Assert.IsNotNull(ship, "ship created");
            yield return null;

            var bulwark = TestAssets.LoadShieldModule(BulwarkShieldPath);
            Assert.IsNotNull(bulwark, "bulwark shield asset loaded");
            Assert.AreNotEqual(ship.Stats.maxShield, bulwark.maxShield, "bulwark differs from the baseline shield cap");

            ship.Reequip(ship.Engine, bulwark);
            Assert.AreEqual(bulwark.maxShield, ship.Stats.maxShield, 1e-3f, "stats reflect the bulwark shield cap");
            Assert.AreEqual(1f, ship.ShieldPct, 1e-3f, "vitals refill on a between-run equip swap");
        }
    }
}
