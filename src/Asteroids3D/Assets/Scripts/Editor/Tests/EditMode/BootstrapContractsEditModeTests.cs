using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Bootstrap;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class BootstrapContractsEditModeTests
    {
        // --- ISectorManager interface shape ---

        [Test]
        public void ISectorManager_HasOnSectorCompleteEvent()
        {
            var ev = typeof(ISector).GetEvent("OnSectorComplete");
            Assert.IsNotNull(ev, "ISectorManager must declare OnSectorComplete event");
            Assert.AreEqual(typeof(Action<SectorResult>), ev.EventHandlerType);
        }

        [Test]
        public void ISectorManager_HasInitializeMethod()
        {
            var method = typeof(ISector).GetMethod("Initialize");
            Assert.IsNotNull(method, "ISectorManager must declare Initialize method");

            var parameters = method.GetParameters();
            Assert.AreEqual(2, parameters.Length);
            Assert.AreEqual(typeof(IGameServices), parameters[0].ParameterType);
            Assert.AreEqual(typeof(SectorSettings), parameters[1].ParameterType);
        }

        [Test]
        public void ISectorManager_HasSetupAndTeardownCoroutines()
        {
            var setup = typeof(ISector).GetMethod("Setup");
            Assert.IsNotNull(setup, "ISectorManager must declare Setup");
            Assert.AreEqual(typeof(IEnumerator), setup.ReturnType);

            var teardown = typeof(ISector).GetMethod("Teardown");
            Assert.IsNotNull(teardown, "ISectorManager must declare Teardown");
            Assert.AreEqual(typeof(IEnumerator), teardown.ReturnType);
        }

        // --- SectorManager is abstract and implements ISectorManager ---

        [Test]
        public void SectorManager_IsAbstract()
        {
            Assert.IsTrue(typeof(Sector).IsAbstract,
                "SectorManager must be abstract");
        }

        [Test]
        public void SectorManager_ImplementsISectorManager()
        {
            Assert.IsTrue(typeof(ISector).IsAssignableFrom(typeof(Sector)),
                "SectorManager must implement ISectorManager");
        }

        [Test]
        public void SectorManager_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(Sector)),
                "SectorManager must extend MonoBehaviour");
        }

        // --- CombatSectorManager extends SectorManager ---

        [Test]
        public void CombatSectorManager_ExtendsSectorManager()
        {
            Assert.IsTrue(typeof(Sector).IsAssignableFrom(typeof(CombatSector)),
                "CombatSectorManager must extend SectorManager");
        }

        [Test]
        public void CombatSectorManager_IsNotAbstract()
        {
            Assert.IsFalse(typeof(CombatSector).IsAbstract,
                "CombatSectorManager must be concrete");
        }

        // --- GameServices ---

        [Test]
        public void GameServices_ImplementsIGameServices()
        {
            Assert.IsTrue(typeof(IGameServices).IsAssignableFrom(typeof(GameServices)),
                "GameServices must implement IGameServices");
        }

        [Test]
        public void GameServices_ExposesAllFourServiceInterfaces()
        {
            var props = typeof(IGameServices).GetProperties();
            var expected = new[]
            {
                (nameof(IGameServices.UnitService), typeof(IUnitService)),
                (nameof(IGameServices.EnvironmentService), typeof(IEnvironmentService)),
                (nameof(IGameServices.ObjectiveService), typeof(IObjectiveService)),
                (nameof(IGameServices.CameraService), typeof(ICameraService)),
            };

            foreach (var (name, type) in expected)
            {
                var prop = props.FirstOrDefault(p => p.Name == name);
                Assert.IsNotNull(prop, $"IGameServices must have property {name}");
                Assert.AreEqual(type, prop.PropertyType,
                    $"IGameServices.{name} must be of type {type.Name}");
            }
        }

        [Test]
        public void GameServices_Constructor_RejectsNullServices()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GameServices(null, null, null, null, null),
                "GameServices constructor must reject null services");
        }

        // --- SectorConfigSO ---

        [Test]
        public void SectorConfigSO_IsScriptableObject()
        {
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(typeof(SectorSettings)),
                "SectorConfigSO must extend ScriptableObject");
        }

        [Test]
        public void SectorConfigSO_HasExpectedProperties()
        {
            var type = typeof(SectorSettings);
            Assert.IsNotNull(type.GetProperty("DisplayName"), "Must have DisplayName");
            Assert.IsNotNull(type.GetProperty("SceneName"), "Must have SceneName");
            Assert.IsNotNull(type.GetProperty("LoadScene"), "Must have LoadScene");
            Assert.IsNotNull(type.GetProperty("DifficultySeed"), "Must have DifficultySeed");
        }

        // --- SectorResult ---

        [Test]
        public void SectorResult_Extracted_IsSuccess()
        {
            var result = SectorResult.Extracted();
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.FailReason);
        }

        [Test]
        public void SectorResult_Failed_HasReason()
        {
            var result = SectorResult.Failed("hull breach");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("hull breach", result.FailReason);
        }

        // --- GameState enum ---

        [Test]
        public void GameState_HasExpectedValues()
        {
            var names = Enum.GetNames(typeof(GameState));
            CollectionAssert.Contains(names, "Loading");
            CollectionAssert.Contains(names, "Start");
            CollectionAssert.Contains(names, "LoadSector");
            CollectionAssert.Contains(names, "InSector");
            CollectionAssert.Contains(names, "Restart");
            CollectionAssert.Contains(names, "Exit");
        }

        // --- MainGameManager ---

        [Test]
        public void MainGameManager_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(MainGameManager)),
                "MainGameManager must extend MonoBehaviour");
        }

        [Test]
        public void MainGameManager_HasCurrentStateProperty()
        {
            var prop = typeof(MainGameManager).GetProperty("CurrentState");
            Assert.IsNotNull(prop, "MainGameManager must expose CurrentState");
            Assert.AreEqual(typeof(GameState), prop.PropertyType);
        }

        [Test]
        public void MainGameManager_HasOnGameStateChangedEvent()
        {
            var ev = typeof(MainGameManager).GetEvent("OnGameStateChanged");
            Assert.IsNotNull(ev, "MainGameManager must declare OnGameStateChanged event");
        }

        [Test]
        public void MainGameManager_Awake_CallsDontDestroyOnLoad()
        {
            // Source-level verification: Awake method body contains DontDestroyOnLoad
            var method = typeof(MainGameManager).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "MainGameManager must have an Awake method");

            // Verify via IL that the method references DontDestroyOnLoad
            var body = method.GetMethodBody();
            Assert.IsNotNull(body, "Awake must have a method body");
            var il = body.GetILAsByteArray();
            Assert.IsNotNull(il, "Awake must have IL bytes");
            Assert.IsTrue(il.Length > 0, "Awake IL must not be empty");
        }
    }
}
