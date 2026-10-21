using Game;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Bootstrap
{
    [Category("Bootstrap")]
    public class PresentationOffBootstrapEditModeTests
    {
        private GameHost host;

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host.gameObject);
        }

        [Test]
        public void BuildHost_TurnsPresentationOff_AndAssignsTheRigCameraAndSector()
        {
            host = PresentationOffBootstrap.BuildHost();

            Assert.IsFalse(host.sessionProfile.presentation, "presentation is off from the first compose");
            Assert.IsTrue(host.playerRig, "player rig assigned");
            Assert.IsTrue(host.observerCamPrefab, "observer camera assigned");
            Assert.IsTrue(host.sessionProfile.sectorEntry.prefab, "sector prefab assigned");
            Assert.IsTrue(host.sessionProfile.sectorEntry.config, "sector config assigned");
        }
    }
}
