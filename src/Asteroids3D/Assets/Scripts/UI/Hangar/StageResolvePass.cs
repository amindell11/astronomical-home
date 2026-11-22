using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace UI.Hangar
{
    internal sealed class StageResolvePass : ScriptableRenderPass
    {
        private readonly Material material;

        internal StageResolvePass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var source = resources.activeColorTexture;
            var outputDescriptor = graph.GetTextureDesc(source);
            outputDescriptor.clearBuffer = false;
            var output = graph.CreateTexture(outputDescriptor);
            using (var builder = graph.AddRasterRenderPass<PassData>("Stage resolve", out var data))
            {
                data.material = material;
                data.source = source;
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1f, 1f, 0f, 0f), pass.material, 0));
            }
            resources.cameraColor = output;
        }

        private sealed class PassData
        {
            public Material material;
            public TextureHandle source;
        }
    }
}
