using System.Collections;
using AI;
using Combat.Targeting;
using NUnit.Framework;
using Ships;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>ship.Targeting is wired and functional after Factory.CreateShip, with and without registry injection; the sensor self-disables without a registry.</summary>
    [Category("Targeting")]
    public class LockOnRegistryWiringPlayModeTests : PlayModeWorldFixture
    {
        private Ship testShip;
        private Ship combatShip;
        private AICommander testPilot;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [TearDown]
        public override void TearDown()
        {
            if (testShip != null)
                DestroyTestObject(testShip);
            
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Ship1_AfterFactory_TargetingIsNotNull()
        {
#if UNITY_EDITOR
            var shipPrefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab");
            testPilot = TestAssets.LoadTestPilotMpc();

            Assert.IsNotNull(shipPrefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(testPilot, "TestPilotMPC prefab failed to load");

            testShip = Factory.CreateShip(
                shipPrefab,
                testPilot,
                team: 0,
                decisionSeed: 0,
                projectiles: Projectiles,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                postInitialize: null);

            Assert.IsNotNull(testShip, "Factory.CreateShip should return non-null ship");
            combatShip = testShip;
            Assert.IsNotNull(combatShip.Targeting, "Ship must have a lock-on sensor (armed)");

            yield return null; // Wait one frame for initialization to complete

            Assert.IsNotNull(combatShip.Targeting,
                "ship.Targeting must be non-null after Factory.CreateShip " +
                "(RefreshChildReferences should have cached the LockOnSensor child)");
#else
            Assert.Ignore("LockOnRegistryWiringPlayModeTests requires Unity Editor assets.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Ship1_WithRegistryInjection_LockOnSensorHasRegistryAndIsEnabled()
        {
#if UNITY_EDITOR
            var shipPrefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab");
            testPilot = TestAssets.LoadTestPilotMpc();

            Assert.IsNotNull(shipPrefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(testPilot, "TestPilotMPC prefab failed to load");

            var stubRegistry = new StubShipRegistry();

            testShip = Factory.CreateShip(
                shipPrefab,
                testPilot,
                team: 0,
                decisionSeed: 0,
                projectiles: Projectiles,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                postInitialize: s => s.Targeting?.SetRegistry(stubRegistry));

            Assert.IsNotNull(testShip, "Factory.CreateShip should return non-null ship");
            combatShip = testShip;
            Assert.IsNotNull(combatShip.Targeting, "Ship must have a lock-on sensor (armed)");

            yield return null; // Wait one frame for Start() to run

            Assert.IsTrue(combatShip.Targeting.HasRegistry,
                "LockOnSensor.HasRegistry must be true after SetRegistry was called in postInitialize");

            Assert.IsTrue(combatShip.Targeting.enabled,
                "LockOnSensor must be enabled when registry is set " +
                "(Start() should not disable it when HasRegistry is true)");
#else
            Assert.Ignore("LockOnRegistryWiringPlayModeTests requires Unity Editor assets.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Ship1_WithoutRegistryInjection_LockOnSensorIsDisabled()
        {
#if UNITY_EDITOR
            var shipPrefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab");
            testPilot = TestAssets.LoadTestPilotMpc();

            Assert.IsNotNull(shipPrefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(testPilot, "TestPilotMPC prefab failed to load");

            testShip = Factory.CreateShip(
                shipPrefab,
                testPilot,
                team: 0,
                decisionSeed: 0,
                projectiles: Projectiles,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                postInitialize: null);

            Assert.IsNotNull(testShip, "Factory.CreateShip should return non-null ship");
            combatShip = testShip;
            Assert.IsNotNull(combatShip.Targeting, "Ship must have a lock-on sensor (armed)");

            yield return null; // Wait one frame for Start() to run

            Assert.IsNotNull(combatShip.Targeting,
                "ship.Targeting must be non-null even without registry injection");

            Assert.IsFalse(combatShip.Targeting.enabled,
                "LockOnSensor must be disabled when no registry is set " +
                "(Start() should disable itself when HasRegistry is false)");
#else
            Assert.Ignore("LockOnRegistryWiringPlayModeTests requires Unity Editor assets.");
            yield break;
#endif
        }

    }
}
