using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Travel
{
    /// <summary>
    /// Draws each bound camera's sector jump image over its post-processed frame. Every renderer lists
    /// this feature before the canopy feature: both passes run at the same event and URP keeps
    /// equal-event passes in feature order, so the HUD composites over the jump image.
    /// </summary>
    public sealed class SectorJumpFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader jumpShader;
        private readonly Dictionary<Camera, SectorJumpPass> passes = new();

        internal Shader JumpShader => jumpShader;

        public override void Create() { }

        internal void Bind(Camera camera, SectorJumpPass pass) => passes.Add(camera, pass);
        internal void Unbind(Camera camera) => passes.Remove(camera);

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (passes.TryGetValue(renderingData.cameraData.camera, out var pass))
                renderer.EnqueuePass(pass);
        }
    }
}
