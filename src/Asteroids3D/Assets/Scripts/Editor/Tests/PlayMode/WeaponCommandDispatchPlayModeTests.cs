using System.Collections;
using Combat.Projectile;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// BUG REPRODUCTION: Both ships cannot fire weapons (player + AI) and no errors are emitted.
    /// Source of truth: Project Board + user report.
    ///
    /// Characterization intent:
    /// - If a commander continuously requests primary/secondary fire,
    ///   the ship should dispatch those commands to WeaponsController and trigger OnFire.
    /// - Current regression: command bits are present but weapon fire is never invoked.
    /// </summary>
    [Category("Weapons")]
    public class WeaponCommandDispatchPlayModeTests : PlayModeWorldFixture
    {
        private Ship ship;
        private Ship combatShip;
        private Commander commanderPrefab;

        private sealed class AlwaysFireCommander : Commander
        {
            private IWeapons weapons;
            public bool Fired { get; private set; }

            public override void Initialize(in ShipControl control)
            {
                weapons = control.WeaponActuator;
            }

            private void FixedUpdate()
            {
                if (weapons == null) return;
                weapons.Fire(WeaponSlot.Primary, new WeaponCommand { pressed = true, held = true });
                weapons.Fire(WeaponSlot.Secondary, new WeaponCommand { pressed = true, held = true });
                Fired = true;
            }
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            var shipPrefab = TestAssets.LoadShip2Prefab();

            Assert.IsNotNull(shipPrefab, "Ship_2 prefab failed to load");

            var commanderGo = new GameObject("AlwaysFireCommanderPrefab");
            commanderPrefab = commanderGo.AddComponent<AlwaysFireCommander>();

            ship = Factory.CreateShip(shipPrefab, commanderPrefab, team: 0, decisionSeed: 0, projectiles: Projectiles, position: Vector3.zero, rotation: Quaternion.identity);
            combatShip = ship;
            Assert.IsNotNull(ship, "Ship failed to instantiate");
            Assert.IsNotNull(combatShip.Weapons, "Ship must be armed (WeaponsController present)");
            Assert.IsNotNull(combatShip.Weapons.Primary, "Primary weapon mount not instantiated");
            Assert.IsNotNull(combatShip.Weapons.Secondary, "Secondary weapon mount not instantiated");
#else
            Assert.Ignore("WeaponCommandDispatchPlayModeTests requires Unity Editor assets.");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            if (ship != null)
                Object.DestroyImmediate(ship.gameObject);

            if (commanderPrefab != null)
                DestroyTestObject(commanderPrefab.gameObject);

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator PrimaryFireCommand_DispatchesToWeapon_OnFireIsRaised()
        {
            var fireCount = 0;
            combatShip.Weapons.Primary.OnFire += () => fireCount++;

            yield return new WaitForFixedUpdate();
            Assert.IsTrue((ship.Commander as AlwaysFireCommander)?.Fired ?? false,
                "Setup sanity: commander should be pushing fire commands.");

            var elapsed = 0f;
            const float timeoutSec = 1.0f;
            while (fireCount == 0 && elapsed < timeoutSec)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            Assert.Greater(fireCount, 0,
                "Expected primary weapon OnFire to be raised when primaryFire command is true. " +
                "Regression likely in Ship command -> WeaponsController dispatch path.");
        }

        [UnityTest]
        public IEnumerator SecondaryFireCommand_DispatchesToWeapon_OnFireIsRaised()
        {
            var fireCount = 0;
            combatShip.Weapons.Secondary.OnFire += () => fireCount++;

            yield return new WaitForFixedUpdate();
            Assert.IsTrue((ship.Commander as AlwaysFireCommander)?.Fired ?? false,
                "Setup sanity: commander should be pushing fire commands.");

            var elapsed = 0f;
            const float timeoutSec = 1.0f;
            while (fireCount == 0 && elapsed < timeoutSec)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            Assert.Greater(fireCount, 0,
                "Expected secondary weapon OnFire to be raised when secondaryFire command is true. " +
                "Regression likely in Ship command -> WeaponsController dispatch path.");
        }
    }
}
