using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UI.Projection
{
    /// <summary>
    /// Each bound camera holds its passes: a screen composite for a HUD camera pair and an image
    /// output for each surface the camera draws. Each pass owns its textures and history.
    /// </summary>
    public sealed class CanopyProjectionFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader projectionShader;
        private readonly Dictionary<Camera, List<CanopyProjectionPass>> passes = new();

        internal Shader ProjectionShader => projectionShader;

        public override void Create() { }

        internal void Bind(Camera camera, CanopyProjectionPass pass)
        {
            if (!passes.TryGetValue(camera, out var list))
                passes.Add(camera, list = new List<CanopyProjectionPass>());
            list.Add(pass);
        }

        internal void Unbind(Camera camera, CanopyProjectionPass pass)
        {
            var list = passes[camera];
            list.Remove(pass);
            if (list.Count == 0)
                passes.Remove(camera);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!passes.TryGetValue(renderingData.cameraData.camera, out var list))
                return;
            foreach (var pass in list)
                renderer.EnqueuePass(pass);
        }
    }
}
