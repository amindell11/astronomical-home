using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UI.Projection
{
    [RequireComponent(typeof(Camera), typeof(UniversalAdditionalCameraData))]
    public sealed class CanopyProjection : MonoBehaviour
    {
        [SerializeField] private CanopyProjectionFeature feature;
        [SerializeField] private CanopyProjectionProfile profile;

        private Camera inkCamera;
        private Camera sceneCamera;
        private RTHandle ink;
        private RTHandle inkColor;
        private bool bound;
        private Func<CanopyState> observeState;
        private CanopyStateBlend blend;

        public Camera Camera => inkCamera;
        public CanopyProjectionProfile Profile => profile;
        public bool PersonalityEnabled { get; set; } = true;
        public bool EffectsEnabled => PersonalityEnabled && profile.personalityEnabled;
        public CanopyForceMode ForceMode { get; set; }
        public CanopyState ActiveState => blend.State;
        public CanopySettings Settings => blend.Settings;
        public float AppearanceAge { get; set; } = -1f;
        public float TransitionAge { get; set; } = -1f;
        public float HitAge { get; set; } = -1f;
        internal CanopyProjectionPass Pass { get; private set; }
        internal RTHandle Ink => ink;
        internal RTHandle InkColor => inkColor;

        private void Awake()
        {
            inkCamera = GetComponent<Camera>();
            var cameraData = GetComponent<UniversalAdditionalCameraData>();
            if (!feature || !feature.ProjectionShader || !profile)
                throw new InvalidOperationException($"CanopyProjection on '{name}' needs its feature, shader and profile.");
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            inkCamera.clearFlags = CameraClearFlags.SolidColor;
            inkCamera.backgroundColor = Color.clear;
            inkCamera.allowHDR = false;
            inkCamera.allowMSAA = false;
        }

        public void Initialize(Camera camera, CanopyProjectionProfile profileOverride = null,
            Func<CanopyState> observeState = null)
        {
            if (!camera) throw new ArgumentNullException(nameof(camera));
            if (sceneCamera) throw new InvalidOperationException("CanopyProjection is already initialized.");
            sceneCamera = camera;
            if (profileOverride) profile = profileOverride;
            this.observeState = observeState;
            blend = new CanopyStateBlend(profile);
            Bind();
            SynchronizeCamera();
        }

        private void Bind()
        {
            Pass = new CanopyProjectionPass(this, feature.ProjectionShader);
            feature.Bind(sceneCamera, this);
            inkCamera.enabled = true;
            bound = true;
        }

        private void OnEnable()
        {
            if (sceneCamera) Bind();
        }

        private void LateUpdate()
        {
            if (!sceneCamera) return;
            var state = ForceMode == CanopyForceMode.Automatic
                ? observeState?.Invoke() ?? CanopyState.Base : (CanopyState)((int)ForceMode - 1);
            blend.Tick(state, Time.unscaledDeltaTime);
            SynchronizeCamera();
        }

        private void SynchronizeCamera()
        {
            inkCamera.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
            inkCamera.orthographic = sceneCamera.orthographic;
            inkCamera.orthographicSize = sceneCamera.orthographicSize;
            inkCamera.fieldOfView = sceneCamera.fieldOfView;
            inkCamera.nearClipPlane = sceneCamera.nearClipPlane;
            inkCamera.farClipPlane = sceneCamera.farClipPlane;
            inkCamera.aspect = sceneCamera.aspect;
            inkCamera.depth = sceneCamera.depth - 1f;
            var width = sceneCamera.pixelWidth;
            var height = sceneCamera.pixelHeight;
            if (ink != null && ink.rt.width == width && ink.rt.height == height) return;
            inkCamera.targetTexture = null;
            inkColor?.Release();
            ink?.Release();
            var descriptor = new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB,
                depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt,
                msaaSamples = 1
            };
            var target = new RenderTexture(descriptor)
            {
                name = "HudInk", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            target.Create();
            ink = RTHandles.Alloc(target, transferOwnership: true);
            // RenderGraph imports color and depth as separate resources.
            inkColor = RTHandles.Alloc(new RenderTargetIdentifier(target), "HudInk color");
            Pass.ResetHistory();
            inkCamera.targetTexture = ink.rt;
        }

        private void OnDisable()
        {
            if (!bound) return;
            feature.Unbind(sceneCamera);
            bound = false;
            inkCamera.enabled = false;
            inkCamera.targetTexture = null;
            inkColor?.Release();
            inkColor = null;
            ink?.Release();
            ink = null;
            Pass.Dispose();
            Pass = null;
        }
    }
}
