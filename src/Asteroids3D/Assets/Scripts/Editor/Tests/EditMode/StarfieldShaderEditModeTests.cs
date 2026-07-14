using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Sectors")]
    public class StarfieldShaderEditModeTests
    {
        private const string MaterialPath = "Assets/Visuals/Materials/StarFieldMaterial.mat";
        private const string WorldPrefabPath = "Assets/Prefabs/World/World.prefab";
        private const string StandalonePrefabPath = "Assets/Prefabs/MiscObjects/StarField.prefab";

        private static Material LoadMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, $"Starfield material missing at {MaterialPath}.");
            return material;
        }

        [Test]
        public void Material_UsesStreamlinedProceduralShaderContract()
        {
            var material = LoadMaterial();

            Assert.AreEqual("Custom/StarField", material.shader.name);
            Assert.AreEqual(2950, material.renderQueue,
                "The starfield must render after the skybox and before ordinary transparent effects.");

            var properties = new[]
            {
                "_Seed", "_StarDensity", "_CellScale", "_StarSizeMin", "_StarSizeMax",
                "_PositionJitter", "_ParallaxFar", "_ParallaxNear", "_NearLayerShare",
                "_ColorCool", "_ColorWarm", "_WarmColorShare", "_Brightness", "_HaloSize",
                "_HaloStrength", "_TwinkleAmount", "_TwinkleSpeed"
            };

            foreach (var property in properties)
                Assert.IsTrue(material.HasProperty(property), $"Starfield shader is missing {property}.");

            Assert.IsFalse(material.HasProperty("_CullDistance"));
            Assert.IsFalse(material.HasProperty("_GridDensity"));
            Assert.IsFalse(material.HasProperty("_ParallaxStrength"));
            Assert.IsFalse(material.HasProperty("_StartingOffset"));
        }

        [Test]
        public void Material_DefaultsDescribeAValidDepthRange()
        {
            var material = LoadMaterial();

            Assert.Greater(material.GetFloat("_CellScale"), 0);
            Assert.Greater(material.GetFloat("_StarSizeMin"), 0);
            Assert.GreaterOrEqual(material.GetFloat("_StarSizeMax"), material.GetFloat("_StarSizeMin"));
            Assert.GreaterOrEqual(material.GetFloat("_ParallaxNear"), material.GetFloat("_ParallaxFar"));
            Assert.That(material.GetFloat("_StarDensity"), Is.InRange(0f, 1f));
            Assert.That(material.GetFloat("_NearLayerShare"), Is.InRange(0f, 1f));
        }

        [TestCase(WorldPrefabPath)]
        [TestCase(StandalonePrefabPath)]
        public void AuthoredStarfields_UseProductionMaterial(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"Starfield prefab missing at {prefabPath}.");

            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            Assert.IsNotNull(renderer, $"No SpriteRenderer found in {prefabPath}.");
            Assert.AreSame(LoadMaterial(), renderer.sharedMaterial);
        }
    }
}
