using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UI.Projection
{
    /// <summary>
    /// Shared by the quality renderers. Each registered camera pair owns its textures and history,
    /// so the feature can serve independent viewports without sharing their HUD light.
    /// </summary>
    public sealed class CanopyProjectionFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader projectionShader;
        private readonly Dictionary<Camera, CanopyProjection> sources = new();

        internal Shader ProjectionShader => projectionShader;

        public override void Create() { }

        internal void Bind(Camera camera, CanopyProjection source) => sources.Add(camera, source);
        internal void Unbind(Camera camera) => sources.Remove(camera);

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (sources.TryGetValue(renderingData.cameraData.camera, out var source))
                renderer.EnqueuePass(source.Pass);
        }
    }
}
