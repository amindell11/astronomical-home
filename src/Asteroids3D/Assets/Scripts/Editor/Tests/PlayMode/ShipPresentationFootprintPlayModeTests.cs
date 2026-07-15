using System.Collections;
using NUnit.Framework;
using Ships.Presentation;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// Verifies ship visuals live on the ship prefab (prefab-centric model): the <c>Ship_1</c> prefab
    /// embeds its visual rig — particles, audio, canvases, renderers — as a child, alongside its
    /// collider and logic. The rig also still exists as a reusable <c>Ship_1_VisualRig</c> prefab asset.
    /// Successor to the decoupling-era footprint test that asserted the sim prefab was presentation-free.
    ///
    /// Asserted against the prefab asset, not a spawned instance: a live ship also carries runtime-spawned
    /// <i>weapon</i> models/audio (a separate subsystem), unrelated to the ship's own visual rig.
    /// </summary>
    [Category("Ships")]
    public class ShipPresentationFootprintPlayModeTests : PlayModeWorldFixture
    {
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";
        private const string Ship1RigPath = "Assets/Prefabs/Ships/Ship_1_VisualRig.prefab";

        /// <summary>The ship prefab embeds its visual rig (presentation) as a child, keeping its collider.</summary>
        [UnityTest]
        public IEnumerator SimShipPrefab_EmbedsVisualRig()
        {
            yield return null;
#if UNITY_EDITOR
            var sim = AssetDatabase.LoadAssetAtPath<GameObject>(Ship1Path);
            Assert.IsNotNull(sim, "Ship_1 prefab failed to load");

            Assert.IsNotNull(sim.GetComponentInChildren<ShipVisualRig>(true),
                "Ship prefab should embed a ShipVisualRig child");
            Assert.Greater(sim.GetComponentsInChildren<ParticleSystem>(true).Length, 0,
                "Ship prefab should carry the rig's particle systems");
            Assert.Greater(sim.GetComponentsInChildren<Canvas>(true).Length, 0,
                "Ship prefab should carry the rig's shield/lock UI canvases");
            Assert.Greater(sim.GetComponentsInChildren<MeshRenderer>(true).Length, 0,
                "Ship prefab should carry the rig's renderers");

            Assert.Greater(sim.GetComponentsInChildren<Collider>(true).Length, 0,
                "Ship prefab must keep its collider");
#else
            Assert.Ignore("Requires the Unity Editor (uses AssetDatabase).");
#endif
        }

        /// <summary>The visual rig prefab still exists as a reusable asset carrying the presentation.</summary>
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
