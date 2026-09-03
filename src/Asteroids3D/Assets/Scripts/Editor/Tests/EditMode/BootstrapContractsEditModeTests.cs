using System;
using System.Collections;
using System.Reflection;
using Game.Session;
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
            Assert.AreEqual(4, parameters.Length);
            Assert.AreEqual(typeof(IGameServices), parameters[0].ParameterType);
            Assert.AreEqual(typeof(SectorSettings), parameters[1].ParameterType);
            Assert.AreEqual(typeof(WorldHandle), parameters[2].ParameterType,
                "Initialize must accept the composition-root world handle as its third parameter");
            Assert.AreEqual(typeof(Ship), parameters[3].ParameterType,
                "Initialize must accept the injected session-rig player as its fourth parameter");
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
        public void GameServices_Constructor_RejectsNullServices()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GameServices(null, null, null, null, null, null),
                "GameServices constructor must reject null services");
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
            // These primitives are the seam an RL/headless driver reuses.
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
            // Presentation policy rides SessionProfile → GameServices/spawn seams (plus the interim
            // GameSettings.PresentationEnabled global for ship rigs), never GameSession state.

            var hook = type.GetProperty("OnSectorComplete");
            Assert.IsNotNull(hook, "GameSession must expose the OnSectorComplete policy hook");
            Assert.AreEqual(typeof(Action<SectorResult>), hook.PropertyType);
            Assert.IsTrue(hook.CanWrite, "OnSectorComplete is the driver-settable policy seam");
        }

        [Test]
        public void ComposeSession_CarriesNoResetPolicy()
        {
            // Reset policy lives on GameSession.OnPlayerDeath instead.
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
            Assert.IsNull(typeof(SessionRig).GetEvent("RestartRequested"),
                "SessionRig must not declare a RestartRequested event");

            var hook = typeof(GameSession).GetProperty("OnPlayerDeath");
            Assert.IsNotNull(hook, "GameSession must expose the OnPlayerDeath policy hook");
            Assert.AreEqual(typeof(Action<ShipId, Damage.DamageInfo>), hook.PropertyType);
            Assert.IsTrue(hook.CanWrite, "OnPlayerDeath is the driver-settable policy seam");

            var build = typeof(SessionRig).GetMethod("Build");
            Assert.IsNotNull(build, "SessionRig must expose Build");
            var parameters = build.GetParameters();
            Assert.AreEqual(3, parameters.Length,
                "Build must take (services, buildPlayer, onPlayerDeath)");
            Assert.AreEqual(typeof(IGameServices), parameters[0].ParameterType);
            Assert.AreEqual(typeof(bool), parameters[1].ParameterType);
            Assert.AreEqual(typeof(Action<ShipId, Damage.DamageInfo>), parameters[2].ParameterType);
        }

    }
}
