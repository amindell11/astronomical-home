using System.Collections;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Step 0 baseline for the visual-layer decoupling program: documents that presentation
    /// (particles, real-time lights, audio sources, world-space canvases, renderers) currently lives
    /// <b>directly on the sim ship prefab</b>. This is the state we are refactoring away from.
    ///
    /// This suite is intentionally a moving target: once the visual-rig split lands (Step 3), these
    /// assertions flip — the sim ship prefab should carry <b>zero</b> presentation components with
    /// presentation off, and the extracted rig should carry them. Until then, asserting their presence
    /// pins the starting point and proves the test can actually see the footprint it will later verify
    /// is gone. Exact counts are logged (not asserted) so they survive incidental art tweaks.
    /// </summary>
    [Category("Integration")]
    [Category("Presentation")]
    public class ShipPresentationFootprintPlayModeTests : PlayModeWorldFixture
    {
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";

        private Ship ship;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#if UNITY_EDITOR
            var settings = TestAssets.LoadDefaultShipSettings();
            var prefab = TestAssets.LoadShipPrefab(Ship1Path);
            Assert.IsNotNull(settings, "Default ship settings failed to load");
            Assert.IsNotNull(prefab, "Ship_1 prefab failed to load");
            ship = Factory.CreateShip(prefab, null, settings, 0, Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(ship, "Ship_1 test ship failed to instantiate");
#else
            Assert.Ignore("ShipPresentationFootprintPlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        /// <summary>
        /// BASELINE: the sim ship prefab currently embeds its full presentation footprint. Each of these
        /// component families is what a later PR moves into a detachable/gated visual rig.
        /// </summary>
        [UnityTest]
        public IEnumerator Ship1_CurrentlyEmbedsPresentationOnSimPrefab()
        {
            yield return null; // let Awake/OnEnable run

            var particles = ship.GetComponentsInChildren<ParticleSystem>(true).Length;
            var lights = ship.GetComponentsInChildren<Light>(true).Length;
            var audioSources = ship.GetComponentsInChildren<AudioSource>(true).Length;
            var canvases = ship.GetComponentsInChildren<Canvas>(true).Length;
            var renderers = ship.GetComponentsInChildren<MeshRenderer>(true).Length;

            Debug.Log($"[ShipPresentationFootprint] Ship_1 baseline — particles:{particles} lights:{lights} " +
                      $"audioSources:{audioSources} canvases:{canvases} meshRenderers:{renderers}");

            Assert.Greater(particles, 0, "Baseline: Ship_1 embeds particle systems (thrusters/reactor)");
            Assert.Greater(lights, 0, "Baseline: Ship_1 embeds a real-time light (thruster glow)");
            Assert.Greater(audioSources, 0, "Baseline: Ship_1 embeds audio sources (engine/damage)");
            Assert.Greater(canvases, 0, "Baseline: Ship_1 embeds world-space canvases (shield UI)");
            Assert.Greater(renderers, 0, "Baseline: Ship_1 embeds mesh renderers (hull/minimap)");
        }
    }
}
