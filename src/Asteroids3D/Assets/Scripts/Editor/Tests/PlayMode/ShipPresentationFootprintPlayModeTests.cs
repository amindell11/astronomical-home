using System.Collections;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// Verifies the ship visual-layer decoupling at the prefab level: the sim <c>Ship_1</c> prefab
    /// carries <b>no</b> presentation (particles, lights, audio, canvases, renderers) — only its
    /// collider and logic — while the extracted <c>Ship_1_VisualRig</c> prefab carries the visuals.
    /// Flipped successor to the Step 0 baseline that documented presentation living on the sim prefab.
    ///
    /// Asserted against the prefab assets, not a spawned instance: a live ship also carries
    /// runtime-spawned <i>weapon</i> models/audio (a separate subsystem, handled in a later PR), which
    /// are unrelated to the ship's own visual rig.
    /// </summary>
    [Category("Integration")]
    [Category("Presentation")]
    public class ShipPresentationFootprintPlayModeTests : PlayModeWorldFixture
    {
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";
        private const string Ship1RigPath = "Assets/Prefabs/Ships/Ship_1_VisualRig.prefab";

        /// <summary>The sim ship prefab is presentation-free but keeps its collider.</summary>
        [UnityTest]
        public IEnumerator SimShipPrefab_IsFreeOfPresentation()
        {
            yield return null;
#if UNITY_EDITOR
            var sim = AssetDatabase.LoadAssetAtPath<GameObject>(Ship1Path);
            Assert.IsNotNull(sim, "Ship_1 prefab failed to load");

            Assert.AreEqual(0, sim.GetComponentsInChildren<ParticleSystem>(true).Length,
                "Sim ship prefab must carry no particle systems");
            Assert.AreEqual(0, sim.GetComponentsInChildren<Light>(true).Length,
                "Sim ship prefab must carry no lights");
            Assert.AreEqual(0, sim.GetComponentsInChildren<AudioSource>(true).Length,
                "Sim ship prefab must carry no audio sources");
            Assert.AreEqual(0, sim.GetComponentsInChildren<Canvas>(true).Length,
                "Sim ship prefab must carry no canvases");
            Assert.AreEqual(0, sim.GetComponentsInChildren<MeshRenderer>(true).Length,
                "Sim ship prefab must carry no renderers");

            Assert.Greater(sim.GetComponentsInChildren<Collider>(true).Length, 0,
                "Sim ship prefab must keep its collider");
#else
            Assert.Ignore("Requires the Unity Editor (uses AssetDatabase).");
#endif
        }

        /// <summary>The extracted visual rig prefab carries the presentation the sim shed.</summary>
        [UnityTest]
        public IEnumerator VisualRigPrefab_CarriesPresentation()
        {
            yield return null;
#if UNITY_EDITOR
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Ship1RigPath);
            Assert.IsNotNull(rig, "Ship_1_VisualRig prefab failed to load");

            Assert.Greater(rig.GetComponentsInChildren<ParticleSystem>(true).Length, 0,
                "Rig should carry the thruster/reactor particles");
            Assert.Greater(rig.GetComponentsInChildren<AudioSource>(true).Length, 0,
                "Rig should carry the engine/damage audio sources");
            Assert.Greater(rig.GetComponentsInChildren<Canvas>(true).Length, 0,
                "Rig should carry the shield/lock UI canvases");
            Assert.Greater(rig.GetComponentsInChildren<MeshRenderer>(true).Length, 0,
                "Rig should carry the hull/minimap renderers");
#endif
        }
    }
}
