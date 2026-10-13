#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering
{
    [Category("Camera")]
    public sealed class MinimapWiringEditModeTests
    {
        private const string MinimapCamPath = "Assets/Prefabs/Cameras/Minimap Cam.prefab";
        private const string MinimapRendererPath = "Assets/Settings/Rendering/MinimapRenderer.asset";
        private const int OpaqueQueueType = 0;

        [Test]
        public void EveryQualityTier_GivesTheMinimapCamTheMinimapRenderer()
        {
            var cam = AssetDatabase.LoadAssetAtPath<GameObject>(MinimapCamPath);
            var cameraData = cam.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "UniversalAdditionalCameraData");
            var rendererIndex = new SerializedObject(cameraData).FindProperty("m_RendererIndex").intValue;
            var minimapRenderer = AssetDatabase.LoadMainAssetAtPath(MinimapRendererPath);
            Assert.That(minimapRenderer, Is.Not.Null);

            for (var tier = 0; tier < QualitySettings.names.Length; tier++)
            {
                var pipeline = QualitySettings.GetRenderPipelineAssetAt(tier);
                var renderers = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
                Assert.That(rendererIndex, Is.LessThan(renderers.arraySize),
                    $"{QualitySettings.names[tier]} ({pipeline.name}) has no renderer slot {rendererIndex}");
                Assert.That(renderers.GetArrayElementAtIndex(rendererIndex).objectReferenceValue, Is.EqualTo(minimapRenderer),
                    $"{QualitySettings.names[tier]} ({pipeline.name}) slot {rendererIndex}");
            }
        }

        // RenderObjects filters on the renderer's own material queue, even with an override material.
        [Test]
        public void MinimapLayerRenderers_SourceMaterialPassesTheirMinimapPassFilter()
        {
            var passes = AssetDatabase.LoadAllAssetsAtPath(MinimapRendererPath)
                .Where(a => a && a.GetType().Name == "RenderObjects")
                .Select(a => new SerializedObject(a).FindProperty("settings.filterSettings"))
                .Select(f => (mask: f.FindPropertyRelative("LayerMask").intValue,
                              opaque: f.FindPropertyRelative("RenderQueueType").enumValueIndex == OpaqueQueueType))
                .ToList();
            Assert.That(passes, Is.Not.Empty);

            var failures = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Visuals" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var layerBit = 1 << renderer.gameObject.layer;
                    var matching = passes.Where(p => (p.mask & layerBit) != 0).ToList();
                    if (matching.Count == 0) continue;

                    var opaque = matching[0].opaque;
                    var range = opaque ? RenderQueueRange.opaque : RenderQueueRange.transparent;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (!material)
                            failures.Add($"{path}: {renderer.name} has no material");
                        else if (material.renderQueue < range.lowerBound || material.renderQueue > range.upperBound)
                            failures.Add($"{path}: {renderer.name} material {material.name} queue {material.renderQueue} outside {(opaque ? "opaque" : "transparent")}");
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
    }
}
#endif
