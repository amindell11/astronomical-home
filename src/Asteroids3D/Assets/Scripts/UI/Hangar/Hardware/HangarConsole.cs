using System;
using UnityEngine;

namespace UI.Hangar.Hardware
{
    public sealed class HangarConsole : MonoBehaviour
    {
        public const float ReferenceAspect = 16f / 9f;

        [SerializeField] private Camera hardwareCamera;
        [SerializeField] private float referenceVerticalFov = 34.11393f;
        [SerializeField] private MeshRenderer mainScreenGlass;
        [SerializeField] private MeshRenderer detailScreenGlass;
        [SerializeField] private Transform selectorKnob;
        [SerializeField] private Transform previousButton;
        [SerializeField] private Transform nextButton;
        [SerializeField] private Transform launchButton;
        [SerializeField] private MeshRenderer projectorLens;

        private RenderTexture texture;

        public Camera HardwareCamera => hardwareCamera;
        public MeshRenderer MainScreenGlass => mainScreenGlass;
        public MeshRenderer DetailScreenGlass => detailScreenGlass;
        public Transform SelectorKnob => selectorKnob;
        public Transform PreviousButton => previousButton;
        public Transform NextButton => nextButton;
        public Transform LaunchButton => launchButton;
        public MeshRenderer ProjectorLens => projectorLens;
        public Texture Texture => texture;

        private void Awake()
        {
            if (!hardwareCamera || !mainScreenGlass || !detailScreenGlass || !selectorKnob ||
                !previousButton || !nextButton || !launchButton || !projectorLens)
                throw new InvalidOperationException("HangarConsole needs its camera, glass, controls and projector lens.");
        }

        public void Resize(Vector2Int pixels)
        {
            var aspect = (float)pixels.x / pixels.y;
            var halfHeight = Mathf.Tan(referenceVerticalFov * Mathf.Deg2Rad * .5f);
            hardwareCamera.fieldOfView = 2f * Mathf.Atan(halfHeight *
                Mathf.Max(1f, ReferenceAspect / aspect)) * Mathf.Rad2Deg;
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
