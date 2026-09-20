using UnityEngine;

namespace Cameras.Starfield
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StarfieldParallax : MonoBehaviour
    {
        private static readonly int SpacingResponseId = Shader.PropertyToID("_SpacingZoomResponse");
        private static readonly int ReferenceSizeId = Shader.PropertyToID("_ZoomReferenceSize");
        private static readonly int ParallaxScaleId = Shader.PropertyToID("_ParallaxScale");
        private static readonly int CorrectionId = Shader.PropertyToID("_ParallaxCorrectionWS");

        [SerializeField] private Camera viewCamera;

        private SpriteRenderer starRenderer;
        private Material starMaterial;
        private MaterialPropertyBlock properties;
        private Transform cameraTransform;
        private Vector3 previousPosition;
        private Vector3 correction;

        private void Awake()
        {
            starRenderer = GetComponent<SpriteRenderer>();
            starMaterial = starRenderer.sharedMaterial;
            properties = new MaterialPropertyBlock();
            cameraTransform = viewCamera.transform;
            previousPosition = cameraTransform.position;
            correction = Vector3.zero;
        }

        private void LateUpdate()
        {
            var position = cameraTransform.position;
            var zoom = starMaterial.GetFloat(ReferenceSizeId) * Mathf.Abs(viewCamera.projectionMatrix.m11);
            var spacingScale = Mathf.Pow(zoom, 1 - starMaterial.GetFloat(SpacingResponseId));
            var movementScale = spacingScale * starMaterial.GetFloat(ParallaxScaleId);
            // Integrate travel so zoom and scale adjustments cannot reposition the pattern.
            correction += (position - previousPosition) * (movementScale - 1);
            previousPosition = position;
            starRenderer.GetPropertyBlock(properties);
            properties.SetVector(CorrectionId, correction);
            starRenderer.SetPropertyBlock(properties);
        }
    }
}
