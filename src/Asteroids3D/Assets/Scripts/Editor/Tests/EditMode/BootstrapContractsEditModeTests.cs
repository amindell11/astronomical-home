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
        [Test]
        public void ISector_HasOnSectorCompleteEvent()
        {
            var ev = typeof(ISector).GetEvent("OnSectorComplete");
            Assert.IsNotNull(ev, "ISector must declare OnSectorComplete event");
            Assert.AreEqual(typeof(Action<SectorResult>), ev.EventHandlerType);
        }

        [Test]
        public void ISector_HasInitializeMethod()
        {
            var method = typeof(ISector).GetMethod("Initialize");
            Assert.IsNotNull(method, "ISector must declare Initialize method");

            var parameters = method.GetParameters();
            Assert.AreEqual(3, parameters.Length);
            Assert.AreEqual(typeof(IGameServices), parameters[0].ParameterType);
            Assert.AreEqual(typeof(SectorSettings), parameters[1].ParameterType);
            Assert.AreEqual(typeof(Ship), parameters[2].ParameterType,
                "Initialize must accept the injected session-rig player as its third parameter");
        }

        [Test]
        public void ISector_HasSetupAndTeardownCoroutines()
        {
            var setup = typeof(ISector).GetMethod("Setup");
            Assert.IsNotNull(setup, "ISector must declare Setup");
            Assert.AreEqual(typeof(IEnumerator), setup.ReturnType);

            var teardown = typeof(ISector).GetMethod("Teardown");
            Assert.IsNotNull(teardown, "ISector must declare Teardown");
            Assert.AreEqual(typeof(IEnumerator), teardown.ReturnType);
        }

        [Test]
        public void Sector_IsConcrete()
        {
            Assert.IsFalse(typeof(Sector).IsAbstract,
                "Sector is the single concrete play-sector (Combat/Arena/Testbench are prefabs of it)");
        }

        [Test]
        public void Sector_ImplementsISector()
        {
            Assert.IsTrue(typeof(ISector).IsAssignableFrom(typeof(Sector)),
                "Sector must implement ISector");
        }

        [Test]
        public void Sector_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(Sector)),
                "Sector must extend MonoBehaviour");
        }

        [Test]
        public void GameServices_ImplementsIGameServices()
        {
            Assert.IsTrue(typeof(IGameServices).IsAssignableFrom(typeof(GameServices)),
                "GameServices must implement IGameServices");
        }

        [Test]
        public void GameServices_ExposesAllServiceInterfaces()
        {
            var props = typeof(IGameServices).GetProperties();
            var expected = new[]
            {
                (nameof(IGameServices.UnitService), typeof(IUnitService)),
                (nameof(IGameServices.Projectiles), typeof(IProjectileService)),
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
                new GameServices(null, null, null, null, null, null, null),
                "GameServices constructor must reject null services");
        }

        [Test]
        public void SectorSettings_IsScriptableObject()
        {
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(typeof(SectorSettings)),
                "SectorSettings must extend ScriptableObject");
        }

        [Test]
        public void SectorSettings_HasExpectedProperties()
        {
            var type = typeof(SectorSettings);
            Assert.IsNotNull(type.GetProperty("DisplayName"), "Must have DisplayName");
            Assert.IsNotNull(type.GetProperty("DifficultySeed"), "Must have DifficultySeed");
            Assert.IsNotNull(type.GetProperty("Locale"), "Must have Locale");
        }

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

        [Test]
        public void GameDriver_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(GameDriver)),
                "GameDriver must extend MonoBehaviour");
        }

        [Test]
        public void SessionHost_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(SessionHost)),
                "SessionHost must extend MonoBehaviour");
        }

        [Test]
        public void GameDriver_HasCurrentStateProperty()
        {
            var prop = typeof(GameDriver).GetProperty("CurrentState");
            Assert.IsNotNull(prop, "GameDriver must expose CurrentState");
            Assert.AreEqual(typeof(GameState), prop.PropertyType);
        }

        [Test]
        public void GameDriver_HasOnGameStateChangedEvent()
        {
            var ev = typeof(GameDriver).GetEvent("OnGameStateChanged");
            Assert.IsNotNull(ev, "GameDriver must declare OnGameStateChanged event");
        }

        [Test]
        public void SessionHost_ImplementsSessionPrimitivesSeam()
        {
            Assert.IsTrue(typeof(ISessionPrimitives).IsAssignableFrom(typeof(SessionHost)),
                "SessionHost must implement ISessionPrimitives (the driver-agnostic seam)");
        }

        [Test]
        public void SessionHost_ExposesDriverAgnosticLifecyclePrimitives()
        {
            // The primitives are the seam an RL/headless driver reuses; each takes the explicit per-session container (not a process singleton), ApplyLoadout being the one non-coroutine.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            foreach (var name in new[] { "ComposeSession", "LoadSector", "UnloadSector", "TeardownSession" })
            {
                var method = typeof(SessionHost).GetMethod(name, flags);
                Assert.IsNotNull(method, $"SessionHost must expose lifecycle primitive {name}");
                Assert.AreEqual(typeof(IEnumerator), method.ReturnType,
                    $"{name} must be a coroutine (IEnumerator)");
                var parameters = method.GetParameters();
                Assert.GreaterOrEqual(parameters.Length, 1, $"{name} must take the session container");
                Assert.AreEqual(typeof(GameSession), parameters[0].ParameterType,
                    $"{name} must take GameSession as its first parameter (per-instance shaping)");
            }

            var applyLoadout = typeof(SessionHost).GetMethod("ApplyLoadout", flags);
            Assert.IsNotNull(applyLoadout, "SessionHost must expose ApplyLoadout");
            var applyParams = applyLoadout.GetParameters();
            Assert.AreEqual(1, applyParams.Length);
            Assert.AreEqual(typeof(GameSession), applyParams[0].ParameterType);
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
            // Presentation is a global GameSettings.PresentationEnabled toggle, not per-session state.

            var hook = type.GetProperty("OnSectorComplete");
            Assert.IsNotNull(hook, "GameSession must expose the OnSectorComplete policy hook");
            Assert.AreEqual(typeof(Action<SectorResult>), hook.PropertyType);
            Assert.IsTrue(hook.CanWrite, "OnSectorComplete is the driver-settable policy seam");
        }

        [Test]
        public void ComposeSession_CarriesNoResetPolicy()
        {
            // Reset policy is injected via GameSession.OnPlayerDeath, never passed as a compose parameter.
            var method = typeof(SessionHost).GetMethod("ComposeSession",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "SessionHost must expose ComposeSession");
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length,
                "ComposeSession must take only the session container — no policy callbacks");
            Assert.AreEqual(typeof(GameSession), parameters[0].ParameterType);
        }

        [Test]
        public void SessionRig_TakesInjectedDeathCallback_NoRestartEvent()
        {
            // Death policy is injected as a callback via Build; the rig wires it onto the player.
            Assert.IsNull(typeof(SessionRig).GetEvent("RestartRequested"),
                "SessionRig must not declare a RestartRequested event");

            var hook = typeof(GameSession).GetProperty("OnPlayerDeath");
            Assert.IsNotNull(hook, "GameSession must expose the OnPlayerDeath policy hook");
            Assert.AreEqual(typeof(Action<ShipId, ShipId>), hook.PropertyType);
            Assert.IsTrue(hook.CanWrite, "OnPlayerDeath is the driver-settable policy seam");

            var build = typeof(SessionRig).GetMethod("Build");
            Assert.IsNotNull(build, "SessionRig must expose Build");
            var parameters = build.GetParameters();
            Assert.AreEqual(3, parameters.Length,
                "Build must take (services, buildPlayer, onPlayerDeath)");
            Assert.AreEqual(typeof(IGameServices), parameters[0].ParameterType);
            Assert.AreEqual(typeof(bool), parameters[1].ParameterType);
            Assert.AreEqual(typeof(Action<ShipId, ShipId>), parameters[2].ParameterType);
        }

        [Test]
        public void SessionHost_DoesNotLookUpSiblingServicesOutsideAwake()
        {
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "Scripts", "Game", "Bootstrap", "SessionHost.cs"));
            StringAssert.Contains("unitService = GetComponent<UnitService>();", source);
            StringAssert.Contains("objectiveService = GetComponent<ObjectiveService>();", source);
            StringAssert.Contains("navFieldService = GetComponent<NavFieldService>();", source);
            var lookups = source.Split(new[] { "GetComponent<" }, StringSplitOptions.None).Length - 1;
            Assert.AreEqual(3, lookups,
                "SessionHost must contain exactly the three Awake cache lookups");
        }

        [Test]
        public void SessionHost_DoesNotReferenceTheDriver()
        {
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "Scripts", "Game", "Bootstrap", "SessionHost.cs"));
            StringAssert.DoesNotContain("GameDriver", source,
                "SessionHost must not reference GameDriver (the seam points up only)");
        }

        [Test]
        public void GameDriver_Awake_CallsDontDestroyOnLoad()
        {
            var method = typeof(GameDriver).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "GameDriver must have an Awake method");

            var body = method.GetMethodBody();
            Assert.IsNotNull(body, "Awake must have a method body");
            var il = body.GetILAsByteArray();
            Assert.IsNotNull(il, "Awake must have IL bytes");
            Assert.IsTrue(il.Length > 0, "Awake IL must not be empty");
        }
    }
}
