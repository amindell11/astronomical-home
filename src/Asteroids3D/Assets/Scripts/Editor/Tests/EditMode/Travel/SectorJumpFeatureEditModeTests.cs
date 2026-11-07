using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Travel
{
    [Category("Sectors")]
    public sealed class SectorJumpFeatureEditModeTests
    {
        [TestCase("Assets/Settings/Rendering/URP-Balanced-Renderer.asset")]
        [TestCase("Assets/Settings/Rendering/URP-HighFidelity-Renderer.asset")]
        [TestCase("Assets/Settings/Rendering/URP-Performant-Renderer.asset")]
        public void QualityRenderer_ListsTheJumpFeatureImmediatelyBeforeTheCanopyFeature(string path)
        {
            var list = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path)).FindProperty("m_RendererFeatures");
            var features = Enumerable.Range(0, list.arraySize)
                .Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue)
                .Select(feature => feature ? feature.GetType().Name : "missing")
                .ToList();
            var canopy = features.IndexOf("CanopyProjectionFeature");
            Assert.That(canopy, Is.GreaterThan(0), $"{path}: {string.Join(", ", features)}");
            Assert.That(features[canopy - 1], Is.EqualTo("SectorJumpFeature"), path);
        }
    }
}
