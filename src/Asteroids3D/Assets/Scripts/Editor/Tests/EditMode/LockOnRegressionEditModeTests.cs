using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Regression tests for Ship composition lifecycle refactor.
    /// 
    /// CONTEXT:
    /// - WeaponsController [DefaultExecutionOrder(-95)] runs before Ship [DefaultExecutionOrder(-90)]
    /// - WeaponsController.Awake() instantiates weapon children (including TargetingComputer)
    /// - Ship.Awake() then safely caches Targeting/Colliders via GetComponentInChildren (children exist)
    /// - Factory.CreateShip renamed preInitialize → postInitialize, fires AFTER ship.Initialize()
    /// - Net effect: ship.Targeting is valid after Factory.CreateShip returns; no Initialize() traversal
    /// 
    /// These tests verify the correct post-refactor state via pure source file string checks.
    /// No type references or reflection - only File.ReadAllText + StringAssert.
    /// </summary>
    [Category("Regression")]
    public class LockOnRegressionEditModeTests
    {
        [Test]
        public void WeaponsController_HasEarlierExecutionOrderThanShip()
        {
            var wcSource   = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Ships", "Weapons", "WeaponsController.cs"));
            var shipSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Ships", "Ship.cs"));

            StringAssert.Contains("[DefaultExecutionOrder(-95)]", wcSource,
                "WeaponsController must run before Ship (order -95) so weapon children exist when Ship.Awake() caches them");
            StringAssert.Contains("[DefaultExecutionOrder(-90)]", shipSource,
                "Ship must run after WeaponsController (order -90)");
        }

        [Test]
        public void WeaponsController_InstantiatesWeaponsAtRuntime_InAwake()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Ships", "Weapons", "WeaponsController.cs"));
            
            StringAssert.Contains("Instantiate(primaryMount", source,
                "WeaponsController must instantiate primary weapon at runtime in Awake");
            StringAssert.Contains("Instantiate(secondaryMount", source,
                "WeaponsController must instantiate secondary weapon at runtime in Awake");
        }

        // NOTE: After the Ship/CombatShip split, weapon and targeting caching moved to CombatShip.
        // Ship base still caches Colliders in Awake; CombatShip caches Weapons + Targeting.
        [Test]
        public void CombatShip_CachesChildRefsInAwake()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Ships", "CombatShip.cs"));

            // CombatShip.Awake() MUST cache runtime children
            var awakeStart = source.IndexOf("protected override void Awake()");
            Assert.Greater(awakeStart, -1, "CombatShip must have an Awake override");
            var awakeEnd = source.IndexOf("protected override", awakeStart + 1);
            if (awakeEnd == -1) awakeEnd = source.Length;
            var awakeBody = source.Substring(awakeStart, awakeEnd - awakeStart);

            StringAssert.Contains("GetComponentInChildren", awakeBody,
                "CombatShip.Awake() must cache Targeting in Awake (children exist because WeaponsController runs first)");
            StringAssert.Contains("GetComponent<WeaponsController>", awakeBody,
                "CombatShip.Awake() must cache WeaponsController");
        }

        [Test]
        public void Ship_InitializeDoesNotCallGetComponentInChildren()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Ships", "Ship.cs"));

            // Initialize() must NOT contain GetComponentInChildren (forbidden by AGENTS.md)
            var initStart = source.IndexOf("public virtual void Initialize(");
            Assert.Greater(initStart, -1, "Ship must have a virtual Initialize method");
            var initEnd   = source.IndexOf("\n        public ", initStart + 1);
            if (initEnd == -1) initEnd = source.Length;
            var initBody = source.Substring(initStart, initEnd - initStart);

            StringAssert.DoesNotContain("GetComponentInChildren", initBody,
                "Ship.Initialize() must NOT call GetComponentInChildren (forbidden in Initialize by AGENTS.md)");
            StringAssert.DoesNotContain("RefreshChildReferences", initBody,
                "Ship.Initialize() must not delegate to RefreshChildReferences — expensive lookups belong in Awake only");
        }

        [Test]
        public void Factory_PostInitializeCallback_FiresAfterInitialize()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Ships", "Factory.cs"));
            
            // Renamed from preInitialize to postInitialize
            StringAssert.Contains("postInitialize", source,
                "Factory must use 'postInitialize' parameter name (renamed from 'preInitialize')");
            
            // Verify ordering: ship.Initialize appears BEFORE postInitialize?.Invoke
            var initializeIndex = source.IndexOf("ship.Initialize");
            var callbackIndex = source.IndexOf("postInitialize?.Invoke");
            
            Assert.Greater(initializeIndex, 0, "Factory.CreateShip must call ship.Initialize");
            Assert.Greater(callbackIndex, 0, "Factory.CreateShip must invoke postInitialize callback");
            Assert.Less(initializeIndex, callbackIndex,
                "ship.Initialize() must be called BEFORE postInitialize callback (callback needs ship.Targeting to be valid)");
        }

        [Test]
        public void UnitService_WireShipDependencies_CanUseShipTargetingDirectly()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Game", "Services", "Units", "UnitService.cs"));
            
            // After refactor, CombatShip.Targeting is used via safe cast
            StringAssert.Contains("Targeting?.SetRegistry", source,
                "UnitService.WireShipDependencies must wire targeting via CombatShip cast");
        }

        [Test]
        public void MissilesPrefab_TargetingComputer_WeaponFieldIsNotSerialized()
        {
            var prefabContent = File.ReadAllText(Path.Combine(Application.dataPath, "Prefabs", "Weapons", "Missiles.prefab"));
            
            // TargetingComputer.weapon should be {fileID: 0} (not serialized, set at runtime)
            StringAssert.Contains("weapon: {fileID: 0}", prefabContent,
                "Missiles.prefab TargetingComputer.weapon field must not be serialized (will be set at runtime via SetWeapon)");
        }
    }
}
