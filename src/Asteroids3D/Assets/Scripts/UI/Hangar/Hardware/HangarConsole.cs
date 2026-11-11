using System;
using UI.Hangar.Hologram;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// The hangar console's own world, camera and composite image.
    /// The hologram projector is its part at the lens.
    /// </summary>
    public sealed class HangarConsole : MonoBehaviour
    {
        public const float ReferenceAspect = 16f / 9f;
        // Below the hangar menu's canvas.
        public const int CompositeSortingOrder = 50;
        private const float TallerViewVerticalBias = .36f;
        private const float TallerViewOverscan = 1.01f;

        [SerializeField] private Camera hardwareCamera;
        [SerializeField] private float referenceVerticalFov = 34.11393f;
        [SerializeField] private MeshRenderer mainScreenGlass;
        [SerializeField] private MeshRenderer detailScreenGlass;
        [SerializeField] private Transform selectorKnob;
        [SerializeField] private Transform previousButton;
        [SerializeField] private Transform nextButton;
        [SerializeField] private Transform launchButton;
        [SerializeField] private MeshRenderer projectorLens;
        [SerializeField] private RawImage compositeImage;
        [SerializeField] private HangarHologramProjector projector;

        private RenderTexture texture;
        private readonly Vector3[] corners = new Vector3[4];

        public Camera HardwareCamera => hardwareCamera;
        public MeshRenderer MainScreenGlass => mainScreenGlass;
        public MeshRenderer DetailScreenGlass => detailScreenGlass;
        public Transform SelectorKnob => selectorKnob;
        public Transform PreviousButton => previousButton;
        public Transform NextButton => nextButton;
        public Transform LaunchButton => launchButton;
        public MeshRenderer ProjectorLens => projectorLens;
        public HangarHologramProjector Projector => projector;
        public Texture Texture => texture;

        private void Awake()
        {
            if (!hardwareCamera || !mainScreenGlass || !detailScreenGlass || !selectorKnob ||
                !previousButton || !nextButton || !launchButton || !projectorLens || !compositeImage || !projector)
                throw new InvalidOperationException(
                    "HangarConsole needs its camera, glass, controls, projector lens, composite image and projector.");
        }

        private void LateUpdate()
        {
            compositeImage.rectTransform.GetWorldCorners(corners);
            Resize(Vector2Int.CeilToInt(corners[2] - corners[0]));
            compositeImage.texture = texture;
        }

        public void Resize(Vector2Int pixels)
        {
            var aspect = (float)pixels.x / pixels.y;
            var halfHeight = Mathf.Tan(referenceVerticalFov * Mathf.Deg2Rad * .5f);
            hardwareCamera.fieldOfView = 2f * Mathf.Atan(halfHeight *
                Mathf.Max(1f, ReferenceAspect / aspect)) * Mathf.Rad2Deg;
            var projection = Matrix4x4.Perspective(hardwareCamera.fieldOfView, aspect,
                hardwareCamera.nearClipPlane, hardwareCamera.farClipPlane);
            if (aspect < ReferenceAspect)
            {
                // Keep both finite silhouette edges beyond the frame in taller views.
                projection.m12 = -(1f - aspect / ReferenceAspect) * TallerViewVerticalBias;
                projection.m00 *= TallerViewOverscan;
                projection.m11 *= TallerViewOverscan;
            }
            hardwareCamera.projectionMatrix = projection;
            projector.Fit(hardwareCamera);
            if (texture && texture.width == pixels.x && texture.height == pixels.y) return;

            ReleaseTexture();
            texture = new RenderTexture(pixels.x, pixels.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "HangarConsoleRT",
                antiAliasing = 2
            };
            hardwareCamera.targetTexture = texture;
        }

        private void OnDestroy() => ReleaseTexture();

        private void ReleaseTexture()
        {
            hardwareCamera.targetTexture = null;
            if (!texture) return;
            texture.Release();
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
            texture = null;
        }
    }
}
