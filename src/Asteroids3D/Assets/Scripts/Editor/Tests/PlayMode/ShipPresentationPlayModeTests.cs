using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Ships;
using Ships.Presentation;
using Ships.Visuals;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
using UI.PlayerState;
using Ships.Registry;

namespace Tests.PlayMode
{
    /// <summary>
    /// Tests the ship presentation layer: a ship prefab carries its visual rig as an embedded child
    /// that self-binds its visuals to the ship, and damage-driven visuals behave across death/respawn.
    /// </summary>
    [Category("Ships")]
    public class ShipPresentationPlayModeTests : PlayModeWorldFixture
    {
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";

        private Ship ship;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            var prefab = TestAssets.LoadShipPrefab(Ship1Path);
            Assert.IsNotNull(prefab, "Ship_1 prefab failed to load");
            ship = Factory.CreateShip(prefab, null, 0, 0, projectiles: null, Vector3.zero, Quaternion.identity);
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

        /// <summary>The ship prefab embeds its rig under the ship, and the rig self-binds its visuals.</summary>
        [UnityTest]
        public IEnumerator EmbeddedRig_ParentsUnderShip_AndWiresVisuals()
        {
            var rig = ship.GetComponentInChildren<ShipVisualRig>(true);
            Assert.IsNotNull(rig, "Ship_1 should embed a ShipVisualRig child");
            Assert.AreEqual(ship.transform, rig.transform.parent, "Rig should be parented under the ship");

            yield return null; // let the embedded rig self-bind in Start

            Assert.IsNotNull(ship.GetComponentInChildren<HullVisuals>(true), "Rig should contribute HullVisuals");
            Assert.IsNotNull(ship.GetComponentInChildren<StatusBarUI>(true), "Rig should contribute StatusBarUI");
            Assert.IsNotNull(ship.GetComponentInChildren<LockOnIndicator>(true), "Rig should contribute LockOnIndicator");
        }

        /// <summary>
        /// Smoke behavior across death/respawn, driven through the embedded, self-bound rig: hidden at
        /// full health, shown below 50%, hidden again after a respawn.
        /// </summary>
        [UnityTest]
        public IEnumerator SmokeTrail_HiddenAtFull_ShownWhenDamaged_HiddenAfterRespawn()
        {
            yield return null; // let the embedded rig self-bind

            var damage = ship.Damage;
            var smoke = GetSmokeObject(ship);
            Assert.IsNotNull(smoke, "Hull smoke ParticleSystem not found on rig");

            Assert.IsFalse(smoke.activeSelf, "Smoke should start hidden at full health");

            // Deplete shield, then drop health below the smoke threshold.
            damage.TakeDamage(Hit(damage.Shield.CurrentValue + 25f));
            yield return null;
            damage.TakeDamage(Hit(damage.Health.MaxValue * 0.6f));
            yield return null;

            Assert.Less(damage.Health.Pct, 0.5f, "Health should be below 50%");
            Assert.IsTrue(smoke.activeSelf, "Smoke should be visible below 50% health");

            // Kill + respawn.
            damage.TakeDamage(Hit(damage.Health.MaxValue + 25f));
            yield return null;
            Assert.IsFalse(ship.gameObject.activeSelf, "Ship should be inactive after lethal damage");

            ship.ResetShip();
            yield return null;
            Assert.IsTrue(ship.gameObject.activeSelf, "Ship should be active after reset");
            Assert.AreEqual(1f, damage.Health.Pct, 0.01f, "Health should be restored after reset");
            Assert.IsFalse(smoke.activeSelf, "Smoke should be hidden again after respawn");
        }

        private static Damage.DamageInfo Hit(float amount) =>
            new(amount, Damage.DamageKind.Laser, ShipId.Invalid, 1f, Vector3.zero, Vector3.zero);

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
