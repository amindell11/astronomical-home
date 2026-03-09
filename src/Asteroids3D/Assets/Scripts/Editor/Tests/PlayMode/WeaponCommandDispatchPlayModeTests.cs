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
    [Category("Integration")]
    [Category("Weapons")]
    public class WeaponCommandDispatchPlayModeTests : PlayModeWorldFixture
    {
        private Ship ship;
        private Commander commanderPrefab;

        private sealed class AlwaysFireCommander : Commander
        {
            public override void InitializeCommander(Ship ship)
            {
                cachedCommand = default;
            }

            private void Update()
            {
                cachedCommand.primaryFire = true;
                cachedCommand.secondaryFire = true;
            }
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            var settings = TestAssets.LoadDefaultShipSettings();
            var shipPrefab = TestAssets.LoadShip2Prefab();

            Assert.IsNotNull(settings, "Default ship settings asset failed to load");
            Assert.IsNotNull(shipPrefab, "Ship_2 prefab failed to load");

            var commanderGo = new GameObject("AlwaysFireCommanderPrefab");
            commanderPrefab = commanderGo.AddComponent<AlwaysFireCommander>();

            ship = Factory.CreateShip(shipPrefab, commanderPrefab, settings, team: 0, position: Vector3.zero, rotation: Quaternion.identity);
            Assert.IsNotNull(ship, "Ship failed to instantiate");
            Assert.IsNotNull(ship.Weapons, "WeaponsController missing on ship");
            Assert.IsNotNull(ship.Weapons.Primary, "Primary weapon mount not instantiated");
            Assert.IsNotNull(ship.Weapons.Secondary, "Secondary weapon mount not instantiated");
#else
            Assert.Ignore("WeaponCommandDispatchPlayModeTests requires Unity Editor assets.");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            // Destroy any in-flight projectiles before the ship so they cannot
            // trigger collisions against a half-destroyed Shooter reference.
            // Without this, deferred Object.Destroy of the ship lets lingering
            // projectiles fire OnTriggerEnter with a destroyed IShooter,
            // causing MissingReferenceException in ApplySplashDamage.
            foreach (var proj in Object.FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None))
                Object.DestroyImmediate(proj.gameObject);

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
            ship.Weapons.Primary.OnFire += () => fireCount++;

            yield return null;
            Assert.IsTrue(ship.CurrentCommand.primaryFire,
                "Setup sanity: commander should be requesting primary fire.");

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
            ship.Weapons.Secondary.OnFire += () => fireCount++;

            yield return null;
            Assert.IsTrue(ship.CurrentCommand.secondaryFire,
                "Setup sanity: commander should be requesting secondary fire.");

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
