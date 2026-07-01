using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Presentation;
using Ships;
using Ships.Presentation;
using Ships.Visuals;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// Tests the ship presentation layer: the visual rig attaches under a sim ship (exactly as the
    /// game-tier <see cref="PresentationInstaller"/> does) and its visuals wire up through the ship,
    /// and damage-driven visuals still behave across death/respawn. Ships built directly by the test
    /// factory are presentation-free until a rig is attached.
    /// </summary>
    [Category("Integration")]
    [Category("Presentation")]
    public class ShipPresentationPlayModeTests : PlayModeWorldFixture
    {
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";

        private Ship ship;
        private ShipVisualRig rig;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            GameSettings.SetVfxEnabled(true);
            var settings = TestAssets.LoadDefaultShipSettings();
            var prefab = TestAssets.LoadShipPrefab(Ship1Path);
            Assert.IsNotNull(settings, "Default ship settings failed to load");
            Assert.IsNotNull(prefab, "Ship_1 prefab failed to load");
            ship = Factory.CreateShip(prefab, null, settings, 0, Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(ship, "Ship_1 failed to instantiate");
#else
            Assert.Ignore("ShipPresentationPlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        /// <summary>The rig is presentation-absent until attached, then parents under the ship and wires up.</summary>
        [UnityTest]
        public IEnumerator AttachRig_AddsRigUnderShip_AndWiresVisuals()
        {
            Assert.AreEqual(0, ship.GetComponentsInChildren<HullVisuals>(true).Length,
                "Sim ship should have no visuals before a rig is attached");

            rig = PresentationInstaller.AttachRigTo(ship);
            yield return null;

            Assert.IsNotNull(rig, "AttachRigTo should return a rig for a ship carrying a ShipVisualBinding");
            Assert.AreEqual(ship.transform, rig.transform.parent, "Rig should be parented under the ship");
            Assert.IsNotNull(ship.GetComponentInChildren<HullVisuals>(true), "Rig should contribute HullVisuals");
            Assert.IsNotNull(ship.GetComponentInChildren<ShieldUI>(true), "Rig should contribute ShieldUI");
            Assert.IsNotNull(ship.GetComponentInChildren<LockOnIndicator>(true), "Rig should contribute LockOnIndicator");
        }

        /// <summary>
        /// Smoke behavior across death/respawn, now driven through the attached rig: hidden at full
        /// health, shown below 50%, hidden again after a respawn. (Moved from ShipRespawnDamage.)
        /// </summary>
        [UnityTest]
        public IEnumerator SmokeTrail_HiddenAtFull_ShownWhenDamaged_HiddenAfterRespawn()
        {
            rig = PresentationInstaller.AttachRigTo(ship);
            Assert.IsNotNull(rig, "Rig should attach");
            yield return null;

            var damage = ship.Damage;
            var smoke = GetSmokeObject(ship);
            Assert.IsNotNull(smoke, "Hull smoke ParticleSystem not found on rig");

            Assert.IsFalse(smoke.activeSelf, "Smoke should start hidden at full health");

            // Deplete shield, then drop health below the smoke threshold.
            damage.TakeDamage(damage.Shield.CurrentValue + 25f, 1f, Vector3.zero, Vector3.zero, null);
            yield return null;
            damage.TakeDamage(damage.Health.MaxValue * 0.6f, 1f, Vector3.zero, Vector3.zero, null);
            yield return null;

            Assert.Less(damage.Health.Pct, 0.5f, "Health should be below 50%");
            Assert.IsTrue(smoke.activeSelf, "Smoke should be visible below 50% health");

            // Kill + respawn.
            damage.TakeDamage(damage.Health.MaxValue + 25f, 1f, Vector3.zero, Vector3.zero, null);
            yield return null;
            Assert.IsFalse(ship.gameObject.activeSelf, "Ship should be inactive after lethal damage");

            ship.ResetShip();
            yield return null;
            Assert.IsTrue(ship.gameObject.activeSelf, "Ship should be active after reset");
            Assert.AreEqual(1f, damage.Health.Pct, 0.01f, "Health should be restored after reset");
            Assert.IsFalse(smoke.activeSelf, "Smoke should be hidden again after respawn");
        }

        private static GameObject GetSmokeObject(Ship ship)
        {
            var hull = ship.GetComponentInChildren<HullVisuals>(true);
            if (!hull) return null;
            var smokeField = typeof(HullVisuals).GetField("smoke", BindingFlags.Instance | BindingFlags.NonPublic);
            var smoke = smokeField?.GetValue(hull) as ParticleSystem;
            return smoke ? smoke.gameObject : null;
        }
    }
}
