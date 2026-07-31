using System.Collections;
using Combat.Conditions;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>Thin smoke: overheat and recovery through the real fire path; detailed heat logic is pinned in Tests.EditMode/HeatEditModeTests.</summary>
    [Category("Weapons")]
    public class HeatOverheatStressPlayModeTests : PlayModeWorldFixture
    {
        protected override bool AccelerateTime => true;

        private Ship ship;
        private Ship combatShip;
        private Commander commanderPrefab;
        private Heat primaryHeat;

        private sealed class ContinuousFireCommander : Commander
        {
            public bool enablePrimaryFire = true;
            private IWeapons weapons;

            public override void Initialize(in ShipControl control) => weapons = control.WeaponActuator;

            private void FixedUpdate() =>
                weapons?.Fire(WeaponSlot.Primary, new WeaponCommand { pressed = enablePrimaryFire, held = enablePrimaryFire });
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            var shipPrefab = TestAssets.LoadShip2Prefab();

            Assert.IsNotNull(shipPrefab, "Ship_2 prefab failed to load");

            commanderPrefab = new GameObject("ContinuousFireCommanderPrefab").AddComponent<ContinuousFireCommander>();
            ship = Factory.CreateShip(shipPrefab, commanderPrefab, team: 0, decisionSeed: 0, projectiles: Projectiles,
                                      position: Vector3.zero, rotation: Quaternion.identity);
            combatShip = ship;

            Assert.IsNotNull(combatShip.Weapons, "Ship must be armed (WeaponsController present)");
            Assert.IsNotNull(combatShip.Weapons?.Primary, "Primary weapon not instantiated");

            primaryHeat = combatShip.Weapons.Primary.GetComponent<Heat>();
            Assert.IsNotNull(primaryHeat, "Heat component missing on primary weapon");
#else
            Assert.Ignore("HeatOverheatStressPlayModeTests requires Unity Editor assets.");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            if (commanderPrefab != null)
                DestroyTestObject(commanderPrefab.gameObject);

            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Weapon_OverheatsThenRecovers_ThroughRealFireFlow()
        {
            var commander = ship.Commander as ContinuousFireCommander;
            Assert.IsNotNull(commander, "Commander cast failed");
            var weaponGo = combatShip.Weapons.Primary.gameObject;

            commander.enablePrimaryFire = true;
            var elapsed = 0f;
            while (!primaryHeat.Overheated && elapsed < 4f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                Assert.IsTrue(weaponGo.activeSelf, $"Primary weapon GameObject deactivated during fire at t={elapsed:F2}s");
            }
            Assert.IsTrue(primaryHeat.Overheated, "Weapon should overheat under continuous fire");

            // Stop firing; it must recover rather than latch overheated. Full lockout drains to
            // zero: penalty 1.5s + 100 heat / 30 per s ≈ 4.9s on the production laser curve.
            commander.enablePrimaryFire = false;
            elapsed = 0f;
            while (primaryHeat.Overheated && elapsed < 6f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
            Assert.IsFalse(primaryHeat.Overheated, "Weapon should recover after firing stops (not stuck overheated)");
            Assert.IsTrue(weaponGo.activeSelf, "Primary weapon GameObject should remain active after recovery");
        }
    }
}
