using Game.Presentation;
using UnityEngine;

namespace Combat.Projectile.Visual
{
    [RequireComponent(typeof(Laser))]
    public class LaserVisual : MonoBehaviour, IPresentationPart
    {
        // Written to both common main-color names so one block serves either shader family; the unused id is ignored.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Fade Settings")]
        [SerializeField] private AnimationCurve fadeCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.7f, 0f),
            new Keyframe(1f, 1f)
        );

        private Laser laser;
        private Renderer[] renderers;
        private Color[] originalColors;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            laser = GetComponent<Laser>();
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();

            var count = renderers.Length;
            originalColors = new Color[count];
            for (var i = 0; i < count; i++)
            {
                var renderer = renderers[i];
                if (renderer && renderer.sharedMaterial)
                    originalColors[i] = renderer.sharedMaterial.color;
            }
        }

        private void OnEnable()
        {
            if (laser) laser.ReturnedToPool += ResetColors;
            ResetColors();
        }

        private void OnDisable()
        {
            if (laser) laser.ReturnedToPool -= ResetColors;
            ResetColors();
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Update()
        {
            if (!laser || renderers == null || renderers.Length == 0) return;
            var maxDistance = laser.MaxDistance;
            if (maxDistance <= 0f) return;

            var normalized = Mathf.Clamp01(laser.DistanceTraveled / maxDistance);
            var alphaFactor = 1f - fadeCurve.Evaluate(normalized);
            ApplyFade(alphaFactor);
        }

        private void ApplyFade(float alphaFactor)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer) continue;
                var color = originalColors[i];
                color.a *= alphaFactor;
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }

        private void ResetColors()
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer) renderer.SetPropertyBlock(null);
        }
    }
}
