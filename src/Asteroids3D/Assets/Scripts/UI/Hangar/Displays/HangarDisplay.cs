using System;
using UI.Projection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    /// <summary>
    /// One console screen. Its camera draws a canvas into a texture, the shared canopy effect
    /// processes that texture at the size the glass shows on the host camera, and the result is the
    /// glass emission. One brightness level drives the glass and its spill light together: a
    /// phosphor flicker, a flare on each glitch, and the collapse to black of a power-down.
    /// </summary>
    public sealed class HangarDisplay : MonoBehaviour
    {
        public const float PowerDownSeconds = .5f;
        private const float GlitchSeconds = .35f;
        private const float FlickerDepth = .06f;
        private const float FlareGain = .6f;
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        private static readonly int EmissionTintId = Shader.PropertyToID("_EmissionTint");

        [SerializeField] private Camera displayCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanopyImage image;
        [SerializeField] private Light spill;
        [SerializeField] private Vector2Int resolution;

        private RenderTexture texture;
        private MaterialPropertyBlock block;
        private MeshRenderer glass;
        private Camera host;
        private Func<CanopySettings> settings;
        private float spillIntensity;
        private float glitchAt = float.NegativeInfinity;
        private float powerDownAt = float.NegativeInfinity;
        private bool started;
        private bool wired;

        public Camera DisplayCamera => displayCamera;
        public Canvas Canvas => canvas;
        public Vector2Int Resolution => resolution;
        public RenderTexture Texture => texture;
        public float Level { get; private set; } = 1f;
        public bool PoweringDown => !float.IsNegativeInfinity(powerDownAt);
        internal bool Glitching => Time.unscaledTime - glitchAt < GlitchSeconds;

        private void Awake()
        {
            var scaler = canvas ? canvas.GetComponent<CanvasScaler>() : null;
            if (!displayCamera || !canvas || !scaler || !image || !spill || resolution.x < 1 || resolution.y < 1)
                throw new InvalidOperationException(
                    $"HangarDisplay on '{name}' needs a camera, canvas, scaler, canopy image, spill light and resolution.");
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
            spillIntensity = spill.intensity;
        }

        public void Initialize(Camera host, MeshRenderer glass, Func<CanopySettings> settings)
        {
            if (!host) throw new ArgumentNullException(nameof(host));
            if (!glass) throw new ArgumentNullException(nameof(glass));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            this.host = host;
            this.glass = glass;
            this.settings = settings;
            spill.color = glass.sharedMaterial.GetColor(EmissionTintId);
            if (started) WireImage();
        }

        public void Glitch() => glitchAt = Time.unscaledTime;

        public void PowerDown()
        {
            if (PoweringDown) return;
            powerDownAt = Time.unscaledTime;
            Glitch();
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
            image.Initialize(host, () => texture, settings, Screen, TransitionAge);
        }

        private void LateUpdate()
        {
            Level = LevelNow(Time.unscaledTime);
            glass.GetPropertyBlock(block);
            block.SetTexture(EmissionMapId, image.Output);
            block.SetFloat(EmissionStrengthId, Level);
            glass.SetPropertyBlock(block);
            spill.intensity = spillIntensity * Level;
        }

        private float LevelNow(float now)
        {
            var level = 1f - settings().flicker * FlickerDepth * Mathf.PerlinNoise(now * 9f, resolution.x * .37f);
            var glitchAge = now - glitchAt;
            if (glitchAge < GlitchSeconds) level *= 1f + FlareGain * (1f - glitchAge / GlitchSeconds);
            if (!PoweringDown) return level;
            var collapse = 1f - Mathf.Clamp01((now - powerDownAt) / PowerDownSeconds);
            return level * collapse * collapse;
        }

        private float TransitionAge()
        {
            var age = Time.unscaledTime - glitchAt;
            return age < GlitchSeconds ? age : -1f;
        }

        // The glass's screen footprint on the host camera, from its bounds' corners.
        private CanopyScreen Screen()
        {
            var bounds = glass.bounds;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector2 pixel = host.WorldToScreenPoint(corner);
                min = Vector2.Min(min, pixel);
                max = Vector2.Max(max, pixel);
            }
            return new CanopyScreen(max - min, new Vector2(host.pixelWidth, host.pixelHeight));
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
