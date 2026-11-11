using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UI.Projection
{
    /// <summary>
    /// The image-output counterpart of <see cref="CanopyProjection"/>. Its pass runs on the host
    /// camera before that camera draws, writes the processed input premultiplied, and
    /// <see cref="Output"/> keeps its identity across input resizes.
    /// </summary>
    public sealed class CanopyImage : MonoBehaviour, ICanopySource
    {
        [Tooltip("The quality renderers' canopy feature, which runs the image pass.")]
        [SerializeField] private CanopyProjectionFeature feature;

        private RenderTexture output;
        private RTHandle outputHandle;
        private RTHandle inputHandle;
        private RenderTexture lastInput;
        private Camera host;
        private Func<RenderTexture> input;
        private Func<CanopySettings> settings;
        private CanopyProjectionPass pass;

        public RenderTexture Output => output;
        internal RTHandle OutputHandle => outputHandle;

        private void Awake()
        {
            if (!feature || !feature.ProjectionShader)
                throw new InvalidOperationException($"CanopyImage on '{name}' needs its feature and shader.");
            var descriptor = new RenderTextureDescriptor(1, 1)
            {
                graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB,
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1
            };
            output = new RenderTexture(descriptor)
            {
                name = "CanopyImage",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            output.Create();
            outputHandle = RTHandles.Alloc(output);
        }

        public void Initialize(Camera host, Func<RenderTexture> input, Func<CanopySettings> settings)
        {
            if (!host) throw new ArgumentNullException(nameof(host));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (this.host) throw new InvalidOperationException("CanopyImage is already initialized.");
            this.host = host;
            this.input = input;
            this.settings = settings;
            Bind();
        }

        private void Bind()
        {
            pass = CanopyProjectionPass.ToImage(this, feature.ProjectionShader);
            feature.Bind(host, pass);
        }

        private void OnEnable()
        {
            if (host) Bind();
        }

        private void OnDisable()
        {
            if (pass == null) return;
            feature.Unbind(host, pass);
            pass.Dispose();
            pass = null;
        }

        private void OnDestroy()
        {
            inputHandle?.Release();
            outputHandle?.Release();
            output.Release();
            Destroy(output);
        }

        internal void Prepare()
        {
            var current = input();
            if (!current)
                throw new InvalidOperationException($"CanopyImage on '{name}' has no input.");
            if (current != lastInput)
            {
                inputHandle?.Release();
                inputHandle = RTHandles.Alloc(new RenderTargetIdentifier(current), "Canopy image input");
                lastInput = current;
            }
            if (output.width != current.width || output.height != current.height)
            {
                outputHandle.Release();
                output.Release();
                output.width = current.width;
                output.height = current.height;
                output.Create();
                outputHandle = RTHandles.Alloc(output);
            }
        }

        RenderTexture ICanopySource.Ink => lastInput;
        RTHandle ICanopySource.InkColor => inputHandle;
        CanopySettings ICanopySource.Settings => settings();
        bool ICanopySource.EffectsEnabled => true;
        float ICanopySource.TransitionAge => -1f;
        float ICanopySource.HitAge => -1f;
    }
}
