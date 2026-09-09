using System;
using System.Collections;
using Game;
using Substrate.Sectors;
using Substrate.Sessions;
using Cameras;
using NUnit.Framework;
using Ships;
using UnityEngine;
using Ships.Registry;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using Substrate.Services.Objectives;

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
            Assert.AreEqual(6, parameters.Length);
            Assert.AreEqual(typeof(IUnitService), parameters[0].ParameterType);
            Assert.AreEqual(typeof(IObjectiveService), parameters[1].ParameterType);
            Assert.AreEqual(typeof(bool), parameters[2].ParameterType,
                "Initialize must accept the session's presentation policy as its third parameter");
            Assert.AreEqual(typeof(SectorSettings), parameters[3].ParameterType);
            Assert.AreEqual(typeof(SessionFrame), parameters[4].ParameterType,
                "Initialize must accept the session's in-plane frame as its fifth parameter");
            Assert.AreEqual(typeof(Ship), parameters[5].ParameterType,
                "Initialize must accept the host-injected player as its sixth parameter");
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
                foreach (var parameter in method.GetParameters())
                    Assert.IsTrue(parameter.IsOptional,
                        $"{name} drives the session it belongs to, so every argument is optional");
            }
        }

        [Test]
        public void Session_ExposesPerSessionState()
        {
            var type = typeof(Session);
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(type),
                "Session is a plain object, not a scene component");
            Assert.AreEqual(typeof(IUnitService), type.GetProperty("Units")?.PropertyType,
                "Session must expose its unit service");
            Assert.AreEqual(typeof(IProjectileService), type.GetProperty("Projectiles")?.PropertyType,
                "Session must expose its projectile service");
            Assert.AreEqual(typeof(IObjectiveService), type.GetProperty("Objectives")?.PropertyType,
                "Session must expose its objective service");
            Assert.IsNull(type.GetProperty("Services"),
                "the services are named one by one — a session holds no service container");
            Assert.IsNotNull(type.GetProperty("ActiveSector"), "Session must expose ActiveSector");
            Assert.IsNull(type.GetProperty("Rig"),
                "the player is the host's, not the session's — a session holds no rig");
            Assert.AreEqual(typeof(SessionFrame), type.GetProperty("Frame")?.PropertyType,
                "Session must expose its in-plane Frame");
            // Presentation policy rides SessionProfile to the sector and spawn seams (plus the
            // interim GameSettings.PresentationEnabled global for ship rigs), never Session state.
        }

        [Test]
        public void Session_TakesOnlyItsSubstrateAtConstruction()
        {
            var constructors = typeof(Session).GetConstructors();
            Assert.AreEqual(1, constructors.Length, "Session has a single composition root");
            var parameters = constructors[0].GetParameters();
            Assert.AreEqual(4, parameters.Length,
                "Session(profile, root, units, objectives) — no rig, no policy");
            Assert.AreEqual(typeof(SessionProfile), parameters[0].ParameterType);
            Assert.AreEqual(typeof(Transform), parameters[1].ParameterType);
            Assert.AreEqual(typeof(UnitService), parameters[2].ParameterType);
            Assert.AreEqual(typeof(ObjectiveService), parameters[3].ParameterType);

            Assert.IsNull(typeof(Session).GetProperty("OnSectorComplete"),
                "the sector-complete hook rides the load call, not a settable property");
            Assert.IsNull(typeof(Session).GetProperty("OnPlayerDeath"),
                "the player-death hook belongs to the player rig, not the session");
        }

        [Test]
        public void Session_TakesTheSectorCompleteHookOnTheLoadCall()
        {
            var parameters = typeof(Session).GetMethod("LoadSector").GetParameters();
            Assert.AreEqual(2, parameters.Length,
                "LoadSector(hero, onSectorComplete)");
            Assert.AreEqual(typeof(Ship), parameters[0].ParameterType);
            Assert.AreEqual(typeof(Action<SectorResult>), parameters[1].ParameterType,
                "the sector-complete hook binds for the life of one load");
            foreach (var parameter in parameters)
                Assert.IsTrue(parameter.IsOptional, $"{parameter.Name} must be optional");
        }

        [Test]
        public void PlayerRig_TakesInjectedDeathCallback_NoRestartEvent()
        {
            Assert.IsNull(typeof(PlayerRig).GetEvent("RestartRequested"),
                "PlayerRig must not declare a RestartRequested event");

            var build = typeof(PlayerRig).GetMethod("Build");
            Assert.IsNotNull(build, "PlayerRig must expose Build");
            var parameters = build.GetParameters();
            Assert.AreEqual(6, parameters.Length,
                "Build must take (units, objectives, presentationEnabled, observer, frame, onPlayerDeath)");
            Assert.AreEqual(typeof(IUnitService), parameters[0].ParameterType);
            Assert.AreEqual(typeof(IObjectiveService), parameters[1].ParameterType);
            Assert.AreEqual(typeof(bool), parameters[2].ParameterType);
            Assert.AreEqual(typeof(ObserverCam), parameters[3].ParameterType);
            Assert.AreEqual(typeof(SessionFrame), parameters[4].ParameterType);
            Assert.AreEqual(typeof(Action<ShipId, Damage.DamageInfo>), parameters[5].ParameterType);
        }
    }
}
