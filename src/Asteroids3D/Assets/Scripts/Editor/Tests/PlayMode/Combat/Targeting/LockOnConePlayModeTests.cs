using System.Collections;
using Combat.Targeting;
using NUnit.Framework;
using Ships;
using Ships.Registry;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    [Category("Targeting")]
    public class LockOnConePlayModeTests : PlayModeWorldFixture
    {
        private const string VanguardPrefabPath = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const float TargetRange = 3f;
        private const float TargetBearingDeg = 25f;

        private ShipRegistry registry;
        private Ship shooter;
        private Ship target;

        public override void SetUp()
        {
            base.SetUp();
            registry = new ShipRegistry();
        }

        public override void TearDown()
        {
            DestroyTestObject(shooter);
            DestroyTestObject(target);
            registry.Dispose();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TargetInConeFromShipCentre_StartsLock_ThoughOutsideItFromMount()
        {
            var prefab = TestAssets.Load<Ship>(VanguardPrefabPath);
            Assert.IsNotNull(prefab, "Vanguard prefab loads");

            shooter = Factory.CreateShip(prefab, null, 0, 0, Projectiles, Vector3.zero, Quaternion.identity);
            var bearing = TargetBearingDeg * Mathf.Deg2Rad;
            var targetPos = shooter.transform.position
                + shooter.transform.up * (TargetRange * Mathf.Cos(bearing))
                + shooter.transform.right * (TargetRange * Mathf.Sin(bearing));
            target = Factory.CreateShip(prefab, null, 1, 0, Projectiles, targetPos, Quaternion.identity);
            registry.ActiveShips.Add(shooter);
            registry.ActiveShips.Add(target);

            var sensor = shooter.Targeting;
            Assert.IsNotNull(sensor, "Vanguard mounts missiles");
            var halfCone = sensor.lockOnConeAngle / 2f;
            Assert.Greater(Vector3.Angle(shooter.transform.up, targetPos - sensor.transform.position), halfCone,
                "test premise: measured from the missile mount, the target sits outside the lock-on cone");

            sensor.SetRegistry(registry);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(LockState.Locking, sensor.State,
                "a target inside the cone measured from the ship's centre starts a lock and holds it");
        }
    }
}
