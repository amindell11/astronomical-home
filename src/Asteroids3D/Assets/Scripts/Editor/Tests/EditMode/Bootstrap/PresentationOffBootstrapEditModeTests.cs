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
            if (!host) return;
            Object.DestroyImmediate(host.sessionProfile.run);
            Object.DestroyImmediate(host.gameObject);
        }

        [Test]
        public void BuildHost_TurnsPresentationOff_AndAssignsTheRigCameraAndSector()
        {
            host = PresentationOffBootstrap.BuildHost();

            Assert.IsFalse(host.sessionProfile.presentation, "presentation is off from the first compose");
            Assert.IsTrue(host.playerRig, "player rig assigned");
            Assert.IsTrue(host.observerCamPrefab, "observer camera assigned");
            Assert.That(host.sessionProfile.run.Sectors, Has.Count.EqualTo(1), "one headless sector");
            Assert.IsTrue(host.sessionProfile.run.Sectors[0].prefab, "sector prefab assigned");
        }
    }
}
