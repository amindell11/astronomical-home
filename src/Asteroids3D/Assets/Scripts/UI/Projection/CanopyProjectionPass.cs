using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace UI.Projection
{
    internal sealed class CanopyProjectionPass : ScriptableRenderPass, IDisposable
    {
        private readonly ICanopySource source;
        private readonly CanopyImage image;
        private readonly Material material;
        private static readonly string[] HistoryNames = { "Canopy history 0", "Canopy history 1" };
        private readonly RTHandle[] history = new RTHandle[2];
        private int previous;
        private bool historyValid;

        private CanopyProjectionPass(ICanopySource source, Shader shader, CanopyImage image)
        {
            this.source = source;
            this.image = image;
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        internal static CanopyProjectionPass ToScreen(ICanopySource source, Shader shader)
        {
            var pass = new CanopyProjectionPass(source, shader, null);
            pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            pass.requiresIntermediateTexture = true;
            return pass;
        }

        internal static CanopyProjectionPass ToImage(CanopyImage image, Shader shader)
        {
            var pass = new CanopyProjectionPass(image, shader, image);
            pass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            pass.requiresIntermediateTexture = false;
            return pass;
        }

        internal void ResetHistory() => historyValid = false;

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            if (image) image.Prepare();
            var descriptor = source.Ink.descriptor;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            // Floating-point history keeps faint afterglow decaying.
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            for (var i = 0; i < history.Length; i++)
                if (RenderingUtils.ReAllocateHandleIfNeeded(ref history[i], descriptor, FilterMode.Bilinear,
                        TextureWrapMode.Clamp, name: HistoryNames[i]))
                    historyValid = false;

            var ink = graph.ImportTexture(source.InkColor, new RenderTargetInfo
            {
                width = source.Ink.width,
                height = source.Ink.height,
                volumeDepth = 1,
                msaaSamples = 1,
                format = source.Ink.graphicsFormat
            });
            var old = graph.ImportTexture(history[previous]);
            var next = graph.ImportTexture(history[1 - previous]);
            var settings = source.Settings;
            using (var builder = graph.AddRasterRenderPass<PassData>("Canopy phosphor history", out var data))
            {
                Fill(data, settings);
                data.previous = old;
                data.decay = historyValid && source.EffectsEnabled && settings.afterglow > 0f
                    ? Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(.001f, settings.afterglow)) : 0f;
                builder.UseTexture(ink, AccessFlags.Read);
                builder.UseTexture(old, AccessFlags.Read);
                builder.SetRenderAttachment(next, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) => Draw(pass, context, 0));
            }

            var w = Mathf.Max(1, source.Ink.width / 4);
            var h = Mathf.Max(1, source.Ink.height / 4);
            var haloDescriptor = new TextureDesc(w, h)
            {
                colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = false,
                name = "Canopy halo A"
            };
            var haloA = graph.CreateTexture(haloDescriptor);
            haloDescriptor.name = "Canopy halo B";
            var haloB = graph.CreateTexture(haloDescriptor);
            using (var builder = graph.AddRasterRenderPass<PassData>("Canopy halo prefilter", out var data))
            {
                Fill(data, settings);
                builder.UseTexture(ink, AccessFlags.Read);
                builder.SetRenderAttachment(haloA, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) => Draw(pass, context, 2));
            }
            using (var builder = graph.AddRasterRenderPass<PassData>("Canopy halo blur X", out var data))
            {
                Fill(data, settings);
                data.halo = haloA;
                data.haloTexel = new Vector4(1f / w, 0f, 0f, 0f);
                builder.UseTexture(haloA, AccessFlags.Read);
                builder.SetRenderAttachment(haloB, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) => Draw(pass, context, 3));
            }
            using (var builder = graph.AddRasterRenderPass<PassData>("Canopy halo blur Y", out var data))
            {
                Fill(data, settings);
                data.halo = haloB;
                data.haloTexel = new Vector4(0f, 1f / h, 0f, 0f);
                builder.UseTexture(haloB, AccessFlags.Read);
                builder.SetRenderAttachment(haloA, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) => Draw(pass, context, 3));
            }

            if (image == null)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var scene = resources.activeColorTexture;
                var outputDescriptor = graph.GetTextureDesc(scene);
                outputDescriptor.name = "Canopy composite";
                outputDescriptor.clearBuffer = false;
                var output = graph.CreateTexture(outputDescriptor);
                using (var builder = graph.AddRasterRenderPass<PassData>("Canopy projection", out var data))
                {
                    Fill(data, settings);
                    data.previous = next;
                    data.scene = scene;
                    data.halo = haloA;
                    builder.UseTexture(ink, AccessFlags.Read);
                    builder.UseTexture(next, AccessFlags.Read);
                    builder.UseTexture(scene, AccessFlags.Read);
                    builder.UseTexture(haloA, AccessFlags.Read);
                    builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) => Draw(pass, context, 1));
                }
                resources.cameraColor = output;
            }
            else
            {
                var target = graph.ImportTexture(image.OutputHandle);
                using (var builder = graph.AddRasterRenderPass<PassData>("Canopy image", out var data))
                {
                    Fill(data, settings);
                    data.previous = next;
                    data.halo = haloA;
                    builder.UseTexture(ink, AccessFlags.Read);
                    builder.UseTexture(next, AccessFlags.Read);
                    builder.UseTexture(haloA, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) => Draw(pass, context, 4));
                }
            }
            previous = 1 - previous;
            historyValid = true;
        }

        private void Fill(PassData data, CanopySettings settings)
        {
            data.previous = TextureHandle.nullHandle;
            data.scene = TextureHandle.nullHandle;
            data.halo = TextureHandle.nullHandle;
            data.haloTexel = Vector4.zero;
            data.decay = 0f;
            data.material = material;
            data.ink = source.Ink;
            data.settings = settings;
            data.resolution = new Vector4(source.Ink.width, source.Ink.height, 0f, 0f);
            data.time = Time.unscaledTime;
            data.enabled = source.EffectsEnabled;
            data.appearanceAge = -1f;
            data.transitionAge = source.TransitionAge;
            data.hitAge = source.HitAge;
        }

        private static void Draw(PassData data, RasterGraphContext context, int passIndex)
        {
            var properties = data.properties;
            properties.Clear();
            properties.SetTexture("_HudInk", data.ink);
            if (data.previous.IsValid()) properties.SetTexture("_PreviousInk", data.previous);
            if (data.scene.IsValid()) properties.SetTexture("_SceneImage", data.scene);
            if (data.halo.IsValid()) properties.SetTexture("_HaloLight", data.halo);
            properties.SetVector("_Resolution", data.resolution);
            properties.SetVector("_Halo", new Vector4(data.settings.haloReach, data.settings.haloStrength,
                data.settings.textGlow, data.settings.halation));
            properties.SetVector("_Bleed", new Vector4(data.settings.signalBleed, data.settings.bleedLength, 0f, 0f));
            properties.SetVector("_CoolTint", (Vector4)data.settings.coolTint);
            properties.SetVector("_HaloTexel", data.haloTexel);
            properties.SetFloat("_Decay", data.decay);
            properties.SetFloat("_Clock", data.time);
            properties.SetFloat("_PersonalityEnabled", data.enabled ? 1f : 0f);
            properties.SetFloat("_AppearAge", data.appearanceAge);
            properties.SetFloat("_TransitionAge", data.transitionAge);
            properties.SetFloat("_HitAge", data.hitAge);
            properties.SetVector("_Beam", new Vector4(data.settings.glow, data.settings.aberration,
                data.settings.beamJitter, 0f));
            properties.SetVector("_Phosphor", new Vector4(data.settings.scanlines, data.settings.flicker,
                data.settings.warmth, data.settings.wear));
            properties.SetVector("_Glass", new Vector4(data.settings.glassDistortion,
                data.settings.damageShudder, data.settings.transitionGlitch, 0f));
            properties.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));
            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, passIndex, MeshTopology.Triangles,
                3, 1, properties);
        }

        public void Dispose()
        {
            foreach (var texture in history) texture?.Release();
            CoreUtils.Destroy(material);
        }

        private sealed class PassData
        {
            public Material material;
            public readonly MaterialPropertyBlock properties = new();
            public Texture ink;
            public TextureHandle previous;
            public TextureHandle scene;
            public TextureHandle halo;
            public Vector4 haloTexel;
            public CanopySettings settings;
            public Vector4 resolution;
            public float decay;
            public float time;
            public bool enabled;
            public float appearanceAge;
            public float transitionAge;
            public float hitAge;
        }
    }
}
