using System.Collections;
using AI;
using Combat.Targeting;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode integration tests for Ship composition lifecycle refactor.
    /// 
    /// CONTEXT:
    /// After the refactor:
    /// - Ship.Initialize() calls RefreshChildReferences() which sets ship.Targeting
    /// - Factory.CreateShip fires postInitialize callback AFTER ship.Initialize()
    /// - Therefore ship.Targeting is guaranteed non-null after Factory.CreateShip returns
    /// - WireShipDependencies can safely call ship.Targeting.SetRegistry()
    /// - LockOnSensor.Start() enables itself only if registry is set
    /// 
    /// These tests verify that ship.Targeting is properly wired and functional
    /// after Factory.CreateShip, with and without registry injection.
    /// </summary>
    [Category("Regression")]
    [Category("Integration")]
    public class LockOnRegistryWiringPlayModeTests : PlayModeWorldFixture
    {
        private Ship testShip;
        private CombatShip combatShip;
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

        /// <summary>
        /// Test 1: After Factory.CreateShip (no postInitialize), ship.Targeting should be non-null.
        /// This verifies that RefreshChildReferences() successfully cached the runtime child.
        /// </summary>
        [UnityTest]
        public IEnumerator Ship1_AfterFactory_TargetingIsNotNull()
        {
#if UNITY_EDITOR
            // Load Ship_1 prefab and test pilot
            var shipPrefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab");
            testPilot = TestAssets.LoadTestPilotMpc();
            var settings = TestAssets.LoadDefaultShipSettings();

            Assert.IsNotNull(shipPrefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(testPilot, "TestPilotMPC prefab failed to load");
            Assert.IsNotNull(settings, "Default ship settings failed to load");

            // Create ship without postInitialize callback
            testShip = Factory.CreateShip(
                shipPrefab,
                testPilot,
                settings,
                team: 0,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                postInitialize: null);

            Assert.IsNotNull(testShip, "Factory.CreateShip should return non-null ship");
            combatShip = testShip as CombatShip;
            Assert.IsNotNull(combatShip, "Ship must be a CombatShip");

            yield return null; // Wait one frame for initialization to complete

            // VERIFY: ship.Targeting is non-null after Factory.CreateShip
            Assert.IsNotNull(combatShip.Targeting,
                "ship.Targeting must be non-null after Factory.CreateShip " +
                "(RefreshChildReferences should have cached the LockOnSensor child)");
#else
            Assert.Ignore("LockOnRegistryWiringPlayModeTests requires Unity Editor assets.");
            yield break;
#endif
        }

        /// <summary>
        /// Test 2: With registry injection via postInitialize callback,
        /// LockOnSensor should have registry and be enabled.
        /// </summary>
        [UnityTest]
        public IEnumerator Ship1_WithRegistryInjection_LockOnSensorHasRegistryAndIsEnabled()
        {
#if UNITY_EDITOR
            // Load Ship_1 prefab and test pilot
            var shipPrefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab");
            testPilot = TestAssets.LoadTestPilotMpc();
            var settings = TestAssets.LoadDefaultShipSettings();

            Assert.IsNotNull(shipPrefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(testPilot, "TestPilotMPC prefab failed to load");
            Assert.IsNotNull(settings, "Default ship settings failed to load");

            // Create stub registry
            var stubRegistry = new StubRegistry();

            // Create ship WITH postInitialize callback that injects registry
            testShip = Factory.CreateShip(
                shipPrefab,
                testPilot,
                settings,
                team: 0,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                postInitialize: s => (s as CombatShip)?.Targeting?.SetRegistry(stubRegistry));

            Assert.IsNotNull(testShip, "Factory.CreateShip should return non-null ship");
            combatShip = testShip as CombatShip;
            Assert.IsNotNull(combatShip, "Ship must be a CombatShip");

            yield return null; // Wait one frame for Start() to run

            // VERIFY: ship.Targeting has registry
            Assert.IsTrue(combatShip.Targeting.HasRegistry,
                "LockOnSensor.HasRegistry must be true after SetRegistry was called in postInitialize");

            // VERIFY: LockOnSensor is enabled (Start() should not disable it)
            Assert.IsTrue(combatShip.Targeting.enabled,
                "LockOnSensor must be enabled when registry is set " +
                "(Start() should not disable it when HasRegistry is true)");
#else
            Assert.Ignore("LockOnRegistryWiringPlayModeTests requires Unity Editor assets.");
            yield break;
#endif
        }

        /// <summary>
        /// Test 3: Without registry injection, LockOnSensor should exist but be disabled.
        /// LockOnSensor.Start() disables itself when registry is null.
        /// </summary>
        [UnityTest]
        public IEnumerator Ship1_WithoutRegistryInjection_LockOnSensorIsDisabled()
        {
#if UNITY_EDITOR
            // Load Ship_1 prefab and test pilot
            var shipPrefab = TestAssets.LoadShipPrefab("Assets/Prefabs/Ships/Ship_1.prefab");
            testPilot = TestAssets.LoadTestPilotMpc();
            var settings = TestAssets.LoadDefaultShipSettings();

            Assert.IsNotNull(shipPrefab, "Ship_1 prefab failed to load");
            Assert.IsNotNull(testPilot, "TestPilotMPC prefab failed to load");
            Assert.IsNotNull(settings, "Default ship settings failed to load");

            // Create ship WITHOUT postInitialize (no registry injection)
            testShip = Factory.CreateShip(
                shipPrefab,
                testPilot,
                settings,
                team: 0,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                postInitialize: null);

            Assert.IsNotNull(testShip, "Factory.CreateShip should return non-null ship");
            combatShip = testShip as CombatShip;
            Assert.IsNotNull(combatShip, "Ship must be a CombatShip");

            yield return null; // Wait one frame for Start() to run

            // VERIFY: ship.Targeting is non-null
            Assert.IsNotNull(combatShip.Targeting,
                "ship.Targeting must be non-null even without registry injection");

            // VERIFY: LockOnSensor is disabled (Start() disables when registry is null)
            Assert.IsFalse(combatShip.Targeting.enabled,
                "LockOnSensor must be disabled when no registry is set " +
                "(Start() should disable itself when HasRegistry is false)");
#else
            Assert.Ignore("LockOnRegistryWiringPlayModeTests requires Unity Editor assets.");
            yield break;
#endif
        }

        /// <summary>
        /// Minimal IShipRegistry stub for testing.
        /// Copied pattern from GameContextDecouplingEditModeTests.
        /// </summary>
        private sealed class StubRegistry : IShipRegistry
        {
            public bool TryGetShipId(Collider collider, out ShipId id)
            {
                id = ShipId.Invalid;
                return false;
            }

            public bool TryGetShip(ShipId id, out Ship ship)
            {
                ship = null;
                return false;
            }

            public bool TryGetShip(Collider collider, out Ship ship, ShipId? excludeId = null)
            {
                ship = null;
                return false;
            }

            public bool IsFriendly(ShipId a, ShipId b) => false;
            public bool IsHostile(ShipId a, ShipId b) => false;
            public int GetTeam(ShipId id) => -1;
        }
    }
}
