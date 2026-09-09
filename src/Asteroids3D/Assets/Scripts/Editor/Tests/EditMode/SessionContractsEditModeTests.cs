using System;
using System.Collections;
using Game.Play;
using Game.Sectors;
using Game.Services;
using Game.Sessions;
using NUnit.Framework;
using Player;
using Ships;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Bootstrap")]
    public class SessionContractsEditModeTests
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
            Assert.AreEqual(typeof(SessionFrame), parameters[2].ParameterType,
                "Initialize must accept the session's in-plane frame as its third parameter");
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
        public void GameSessionHost_HasCurrentStateProperty()
        {
            var prop = typeof(GameSessionHost).GetProperty("CurrentState");
            Assert.IsNotNull(prop, "GameSessionHost must expose CurrentState");
            Assert.AreEqual(typeof(GameState), prop.PropertyType);
        }

        [Test]
        public void GameSessionHost_HasOnGameStateChangedEvent()
        {
            var ev = typeof(GameSessionHost).GetEvent("OnGameStateChanged");
            Assert.IsNotNull(ev, "GameSessionHost must declare OnGameStateChanged event");
        }

        [Test]
        public void Session_ExposesLifecycleCoroutines()
        {
            foreach (var name in new[] { "Compose", "LoadSector", "UnloadSector", "Teardown" })
            {
                var method = typeof(Session).GetMethod(name);
                Assert.IsNotNull(method, $"Session must expose lifecycle step {name}");
                Assert.AreEqual(typeof(IEnumerator), method.ReturnType,
                    $"{name} must be a coroutine (IEnumerator)");
                Assert.IsEmpty(method.GetParameters(),
                    $"{name} drives the session it belongs to, so it takes no arguments");
            }
        }

        [Test]
        public void Session_ExposesPerSessionState()
        {
            var type = typeof(Session);
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(type),
                "Session is a plain object, not a scene component");
            Assert.IsNotNull(type.GetProperty("Services"), "Session must expose Services");
            Assert.IsNotNull(type.GetProperty("ActiveSector"), "Session must expose ActiveSector");
            Assert.IsNotNull(type.GetProperty("Rig"), "Session must expose Rig");
            Assert.AreEqual(typeof(SessionFrame), type.GetProperty("Frame")?.PropertyType,
                "Session must expose its in-plane Frame");
            // Presentation policy rides SessionProfile to the GameServices/spawn seams (plus the
            // interim GameSettings.PresentationEnabled global for ship rigs), never Session state.
        }

        [Test]
        public void Session_TakesItsPolicyHooksAtConstruction()
        {
            var constructors = typeof(Session).GetConstructors();
            Assert.AreEqual(1, constructors.Length, "Session has a single composition root");
            var parameters = constructors[0].GetParameters();
            Assert.AreEqual(7, parameters.Length,
                "Session(profile, root, units, objectives, rig, onSectorComplete, onPlayerDeath)");
            Assert.AreEqual(typeof(SessionProfile), parameters[0].ParameterType);
            Assert.AreEqual(typeof(Transform), parameters[1].ParameterType);
            Assert.AreEqual(typeof(UnitService), parameters[2].ParameterType);
            Assert.AreEqual(typeof(ObjectiveService), parameters[3].ParameterType);
            Assert.AreEqual(typeof(SessionRig), parameters[4].ParameterType);
            Assert.AreEqual(typeof(Action<SectorResult>), parameters[5].ParameterType,
                "the sector-complete hook is injected, never settable after construction");
            Assert.AreEqual(typeof(Action<ShipId, Damage.DamageInfo>), parameters[6].ParameterType,
                "the player-death hook is injected, never settable after construction");

            Assert.IsNull(typeof(Session).GetProperty("OnSectorComplete"),
                "policy hooks are constructor parameters, not settable properties");
            Assert.IsNull(typeof(Session).GetProperty("OnPlayerDeath"),
                "policy hooks are constructor parameters, not settable properties");
        }

        [Test]
        public void SessionRig_TakesInjectedDeathCallback_NoRestartEvent()
        {
            Assert.IsNull(typeof(SessionRig).GetEvent("RestartRequested"),
                "SessionRig must not declare a RestartRequested event");

            var build = typeof(SessionRig).GetMethod("Build");
            Assert.IsNotNull(build, "SessionRig must expose Build");
            var parameters = build.GetParameters();
            Assert.AreEqual(4, parameters.Length,
                "Build must take (services, buildPlayer, frame, onPlayerDeath)");
            Assert.AreEqual(typeof(IGameServices), parameters[0].ParameterType);
            Assert.AreEqual(typeof(bool), parameters[1].ParameterType);
            Assert.AreEqual(typeof(SessionFrame), parameters[2].ParameterType);
            Assert.AreEqual(typeof(Action<ShipId, Damage.DamageInfo>), parameters[3].ParameterType);
        }
    }
}
