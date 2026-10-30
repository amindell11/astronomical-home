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
        private readonly CanopyProjection source;
        private readonly Material material;
        private readonly RTHandle[] history = new RTHandle[2];
        private int previous;
        private bool historyValid;

        internal CanopyProjectionPass(CanopyProjection source, Shader shader)
        {
            this.source = source;
            material = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        internal void ResetHistory() => historyValid = false;

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var descriptor = source.Ink.rt.descriptor;
            // Floating-point history keeps faint afterglow decaying.
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            for (var i = 0; i < history.Length; i++)
                if (RenderingUtils.ReAllocateHandleIfNeeded(ref history[i], descriptor, FilterMode.Bilinear,
                        TextureWrapMode.Clamp, name: $"Canopy history {i}"))
                    historyValid = false;

            var ink = graph.ImportTexture(source.InkColor, new RenderTargetInfo
            {
                width = source.Ink.rt.width,
                height = source.Ink.rt.height,
                volumeDepth = 1,
                msaaSamples = 1,
                format = source.Ink.rt.graphicsFormat
            });
            var old = graph.ImportTexture(history[previous]);
            var next = graph.ImportTexture(history[1 - previous]);
            var settings = source.Profile.baseSettings;
            using (var builder = graph.AddRasterRenderPass<PassData>("Canopy phosphor history", out var data))
            {
                Fill(data, settings);
                data.previous = old;
                data.decay = historyValid && source.PersonalityEnabled && settings.afterglow > 0f
                    ? Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(.001f, settings.afterglow)) : 0f;
                builder.UseTexture(ink, AccessFlags.Read);
                builder.UseTexture(old, AccessFlags.Read);
                builder.SetRenderAttachment(next, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) => Draw(pass, context, 0));
            }

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
                builder.UseTexture(ink, AccessFlags.Read);
                builder.UseTexture(next, AccessFlags.Read);
                builder.UseTexture(scene, AccessFlags.Read);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) => Draw(pass, context, 1));
            }
            resources.cameraColor = output;
            previous = 1 - previous;
            historyValid = true;
        }

        private void Fill(PassData data, CanopySettings settings)
        {
            data.scene = TextureHandle.nullHandle;
            data.decay = 0f;
            data.material = material;
            data.ink = source.Ink.rt;
            data.settings = settings;
            data.resolution = new Vector4(source.Ink.rt.width, source.Ink.rt.height, 0f, 0f);
            data.time = Time.unscaledTime;
            data.enabled = source.PersonalityEnabled;
            data.appearanceAge = source.AppearanceAge;
            data.transitionAge = source.TransitionAge;
            data.hitAge = source.HitAge;
        }

        private static void Draw(PassData data, RasterGraphContext context, int passIndex)
        {
            var properties = data.properties;
            properties.Clear();
            properties.SetTexture("_HudInk", data.ink);
            properties.SetTexture("_PreviousInk", data.previous);
            if (data.scene.IsValid()) properties.SetTexture("_SceneImage", data.scene);
            properties.SetVector("_Resolution", data.resolution);
            properties.SetFloat("_Decay", data.decay);
            properties.SetFloat("_Clock", data.time);
            properties.SetFloat("_PersonalityEnabled", data.enabled ? 1f : 0f);
            properties.SetFloat("_AppearAge", data.appearanceAge);
            properties.SetFloat("_TransitionAge", data.transitionAge);
            properties.SetFloat("_HitAge", data.hitAge);
            properties.SetVector("_Beam", new Vector4(data.settings.glow, data.settings.aberration,
                data.settings.curvature, data.settings.beamJitter));
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
