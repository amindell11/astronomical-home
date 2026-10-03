using Game;
using Substrate.Sectors;
using Substrate.Sessions;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Bootstrap")]
    public class SessionContractsEditModeTests
    {
        [Test]
        public void ISector_DeclaresItsLifecycleMembers()
        {
            foreach (var name in new[] { "Initialize", "Setup", "Teardown" })
                Assert.IsNotNull(typeof(ISector).GetMethod(name), $"ISector must declare {name}");
        }

        [Test]
        public void SectorSettings_HasExpectedProperties()
        {
            var type = typeof(SectorSettings);
            Assert.IsNotNull(type.GetProperty("DisplayName"), "Must have DisplayName");
            Assert.IsNotNull(type.GetProperty("DifficultySeed"), "Must have DifficultySeed");
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
        public void Session_HoldsNoSceneIdentityContainerOrRig()
        {
            var type = typeof(Session);
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(type),
                "Session is a plain object, not a scene component");
            Assert.IsNull(type.GetProperty("Services"),
                "the services are named one by one — a session holds no service container");
            Assert.IsNull(type.GetProperty("Rig"),
                "the player is the host's, not the session's — a session holds no rig");
        }

        [Test]
        public void Session_TakesOnlyItsSubstrateAtConstruction()
        {
            var constructors = typeof(Session).GetConstructors();
            Assert.AreEqual(1, constructors.Length, "Session has a single composition root");
            Assert.AreEqual(4, constructors[0].GetParameters().Length,
                "Session(profile, root, units, objectives) — no rig, no policy");

            Assert.IsNull(typeof(Session).GetProperty("OnSectorComplete"),
                "the sector-complete hook rides the load call, not a settable property");
            Assert.IsNull(typeof(Session).GetProperty("OnPlayerDeath"),
                "the player-death hook belongs to the player rig, not the session");
        }

        [Test]
        public void PlayerRig_DeclaresNoRestartEvent()
        {
            Assert.IsNull(typeof(PlayerRig).GetEvent("RestartRequested"),
                "PlayerRig must not declare a RestartRequested event");
        }
    }
}
