using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.Travel
{
    internal sealed class SectorJumpPass : ScriptableRenderPass
    {
        private readonly Material material;
        private readonly MaterialPropertyBlock properties = new();

        internal SectorJumpPass(Material material)
        {
            this.material = material;
            properties.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));
            // URP keeps an after-post intermediate only for passes at exactly this event.
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            using (var builder = graph.AddRasterRenderPass<PassData>("Sector jump", out var data))
            {
                data.material = material;
                data.properties = properties;
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0, MeshTopology.Triangles,
                        3, 1, pass.properties));
            }
        }

        private sealed class PassData
        {
            public Material material;
            public MaterialPropertyBlock properties;
        }
    }
}
