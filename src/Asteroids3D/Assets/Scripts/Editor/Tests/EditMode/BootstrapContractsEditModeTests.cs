using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Bootstrap;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using Player;
using Ships;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Bootstrap")]
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
            Assert.AreEqual(3, parameters.Length);
            Assert.AreEqual(typeof(IGameServices), parameters[0].ParameterType);
            Assert.AreEqual(typeof(SectorSettings), parameters[1].ParameterType);
            Assert.AreEqual(typeof(Ship), parameters[2].ParameterType,
                "Initialize must accept the injected session-rig player as its third parameter");
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

        // --- Sector is the single concrete play-sector and implements ISector ---

        [Test]
        public void Sector_IsConcrete()
        {
            Assert.IsFalse(typeof(Sector).IsAbstract,
                "Sector is now the single concrete play-sector (Combat/Arena/Testbench are prefabs of it)");
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
            // Per-entry/per-session overridable config. Scene identity (sceneName/loadScene) is
            // sector-type intrinsic and lives on the Sector template, not here.
            var type = typeof(SectorSettings);
            Assert.IsNotNull(type.GetProperty("DisplayName"), "Must have DisplayName");
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

        // --- Lifecycle primitives (bootstrap/session decoupling) ---

        [Test]
        public void MainGameManager_ExposesDriverAgnosticLifecyclePrimitives()
        {
            // The primitives are the seam an RL/headless driver reuses; each takes the explicit
            // per-session container (not a process singleton) and returns a coroutine.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            foreach (var name in new[] { "ComposeSession", "LoadSector", "UnloadSector", "TeardownSession" })
            {
                var method = typeof(MainGameManager).GetMethod(name, flags);
                Assert.IsNotNull(method, $"MainGameManager must expose lifecycle primitive {name}");
                Assert.AreEqual(typeof(IEnumerator), method.ReturnType,
                    $"{name} must be a coroutine (IEnumerator)");
                var parameters = method.GetParameters();
                Assert.GreaterOrEqual(parameters.Length, 1, $"{name} must take the session container");
                Assert.AreEqual(typeof(GameSession), parameters[0].ParameterType,
                    $"{name} must take GameSession as its first parameter (per-instance shaping)");
            }
        }

        [Test]
        public void GameSession_ExposesPerSessionState()
        {
            var type = typeof(GameSession);
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(type),
                "GameSession is a plain container, not a scene object");
            Assert.IsNotNull(type.GetProperty("Services"), "GameSession must expose Services");
            Assert.IsNotNull(type.GetProperty("ActiveSector"), "GameSession must expose ActiveSector");
            Assert.IsNotNull(type.GetProperty("Rig"), "GameSession must expose Rig");
            Assert.IsNotNull(type.GetProperty("Presentation"), "GameSession must expose Presentation");

            var hook = type.GetProperty("OnSectorComplete");
            Assert.IsNotNull(hook, "GameSession must expose the OnSectorComplete policy hook");
            Assert.AreEqual(typeof(Action<SectorResult>), hook.PropertyType);
            Assert.IsTrue(hook.CanWrite, "OnSectorComplete is the driver-settable policy seam");
        }

        [Test]
        public void ComposeSession_CarriesNoResetPolicy()
        {
            // Plan C: composition is policy-free. The reset trigger is wired by the driver via
            // PlayerRig.RestartRequested, never passed through the composition primitive.
            var method = typeof(MainGameManager).GetMethod("ComposeSession",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "MainGameManager must expose ComposeSession");
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length,
                "ComposeSession must take only the session container — no policy callbacks");
            Assert.AreEqual(typeof(GameSession), parameters[0].ParameterType);
        }

        [Test]
        public void PlayerRig_DeclaresRestartAsEventNotCallback()
        {
            // The rig only DECLARES that its death policy requested a restart; the driver decides
            // what a restart means. Build therefore takes no restart callback.
            var ev = typeof(PlayerRig).GetEvent("RestartRequested");
            Assert.IsNotNull(ev, "PlayerRig must declare RestartRequested event");
            Assert.AreEqual(typeof(Action), ev.EventHandlerType);

            var build = typeof(PlayerRig).GetMethod("Build");
            Assert.IsNotNull(build, "PlayerRig must expose Build");
            var parameters = build.GetParameters();
            Assert.AreEqual(2, parameters.Length,
                "Build must take (services, buildPlayer) only — death policy is not a Build argument");
            Assert.AreEqual(typeof(IGameServices), parameters[0].ParameterType);
            Assert.AreEqual(typeof(bool), parameters[1].ParameterType);
        }

        [Test]
        public void MainGameManager_DoesNotLookUpSiblingServicesOutsideAwake()
        {
            // Injection hygiene (plan §A): the sibling MonoBehaviour services are cached in Awake;
            // no GetComponent calls mid-lifecycle. The only two lookups allowed in the file are the
            // Awake cache assignments.
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "Scripts", "Game", "Bootstrap", "MainGameManager.cs"));
            StringAssert.Contains("unitService = GetComponent<UnitService>();", source);
            StringAssert.Contains("objectiveService = GetComponent<ObjectiveService>();", source);
            var lookups = source.Split(new[] { "GetComponent<" }, StringSplitOptions.None).Length - 1;
            Assert.AreEqual(2, lookups,
                "MainGameManager must contain exactly the two Awake cache lookups");
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
