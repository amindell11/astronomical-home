using System;
using UI.Projection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    /// <summary>
    /// One console screen. Its camera draws a canvas into a texture, the shared canopy effect
    /// processes that texture, and the result is the glass emission.
    /// </summary>
    public sealed class HangarDisplay : MonoBehaviour
    {
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

        [SerializeField] private Camera displayCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanopyImage image;
        [SerializeField] private Vector2Int resolution;

        private RenderTexture texture;
        private MaterialPropertyBlock block;
        private MeshRenderer glass;
        private Camera host;
        private Func<CanopySettings> settings;
        private bool started;
        private bool wired;

        public Camera DisplayCamera => displayCamera;
        public Canvas Canvas => canvas;
        public Vector2Int Resolution => resolution;
        public RenderTexture Texture => texture;

        private void Awake()
        {
            var scaler = canvas ? canvas.GetComponent<CanvasScaler>() : null;
            if (!displayCamera || !canvas || !scaler || !image || resolution.x < 1 || resolution.y < 1)
                throw new InvalidOperationException(
                    $"HangarDisplay on '{name}' needs a camera, canvas, scaler, canopy image and resolution.");
            if (canvas.GetComponent<GraphicRaycaster>())
                throw new InvalidOperationException($"HangarDisplay on '{name}' must not raycast on its own.");
            Configure(displayCamera, canvas, scaler, resolution);
            texture = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32)
            {
                name = name + "RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            displayCamera.targetTexture = texture;
            block = new MaterialPropertyBlock();
        }

        public void Initialize(Camera host, MeshRenderer glass, Func<CanopySettings> settings)
        {
            if (!host) throw new ArgumentNullException(nameof(host));
            if (!glass) throw new ArgumentNullException(nameof(glass));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            this.host = host;
            this.glass = glass;
            this.settings = settings;
            if (started) WireImage();
        }

        // Start, so CanopyImage.Awake has run and its OnEnable did not bind.
        private void Start()
        {
            started = true;
            WireImage();
        }

        private void WireImage()
        {
            if (wired || !host) return;
            wired = true;
            image.Initialize(host, () => texture, settings);
        }

        private void LateUpdate()
        {
            glass.GetPropertyBlock(block);
            block.SetTexture(EmissionMapId, image.Output);
            glass.SetPropertyBlock(block);
        }

        private void OnDestroy()
        {
            if (displayCamera) displayCamera.targetTexture = null;
            if (!texture) return;
            texture.Release();
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
            texture = null;
        }

        internal static void Configure(Camera camera, Canvas canvas, CanvasScaler scaler, Vector2Int resolution)
        {
            camera.orthographic = true;
            camera.orthographicSize = resolution.y * .5f / 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 5;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 2f;
            camera.depth = -10f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = resolution;
            scaler.matchWidthOrHeight = .5f;
        }
    }
}
