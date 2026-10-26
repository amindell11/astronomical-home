using Game.Preview;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Bootstrap
{
    [Category("Bootstrap")]
    public class PreviewHostBootstrapEditModeTests
    {
        private PreviewHost host;

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host.gameObject);
        }

        [TestCase(PreviewMode.Inspection)]
        [TestCase(PreviewMode.Playtest)]
        public void BuildHost_AssignsModeAndTheRigCameraSectorAndFeel(PreviewMode mode)
        {
            host = PreviewHostBootstrap.BuildHost(PreviewSubjectKind.RunRecap, mode);

            Assert.AreEqual(mode, host.Mode);
            Assert.IsTrue(host.gameObject.activeSelf, "the configured host is ready for play mode");
            Assert.IsTrue(host.sessionProfile.presentation, "the preview shows presentation");
            Assert.IsTrue(host.playerRigPrefab, "player rig assigned");
            Assert.IsTrue(host.observerCamPrefab, "observer camera assigned");
            Assert.IsTrue(host.sessionProfile.sectors[0].prefab, "sector prefab assigned");
            Assert.IsTrue(host.sessionProfile.sectors[0].config, "sector config assigned");
            Assert.IsTrue(host.recapFeel, "recap feel profile assigned");
        }
    }
}
