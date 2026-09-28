#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Scenarios.Drawn
{
    public sealed class DrawnInkStudy : IDisposable
    {
        private readonly ScriptableRendererData rendererData;
        private readonly FullScreenPassRendererFeature feature;
        private readonly Material material;

        public DrawnInkStudy(float strength)
        {
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            rendererData = pipeline.rendererDataList[0];
            var shader = Shader.Find("Hidden/Astronomical/Comparison/Screen Ink");
            if (!shader || !shader.isSupported)
                throw new InvalidOperationException("The screen ink study shader must compile.");
            material = new Material(shader);
            Strength = strength;
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "Temporary drawn ink study";
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.Create();
            rendererData.rendererFeatures.Add(feature);
            rendererData.SetDirty();
        }

        public float Strength
        {
            set => material.SetFloat("_InkStrength", value);
        }

        public void Dispose()
        {
            rendererData.rendererFeatures.Remove(feature);
            rendererData.SetDirty();
            feature.Dispose();
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(material);
        }
    }
}
#endif
