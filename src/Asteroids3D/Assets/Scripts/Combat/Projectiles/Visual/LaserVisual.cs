using UnityEngine;

namespace Combat.Projectile.Visual
{
    [RequireComponent(typeof(Laser))]
    public class LaserVisual : MonoBehaviour
    {
        [Header("Fade Settings")]
        [SerializeField] private AnimationCurve fadeCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.7f, 0f),
            new Keyframe(1f, 1f)
        );

        private Laser laser;
        private Renderer[] renderers;
        private Material[] materials;
        private Color[] originalColors;

        private void Awake()
        {
            laser = GetComponent<Laser>();
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            var count = renderers.Length;
            materials = new Material[count];
            originalColors = new Color[count];
            for (var i = 0; i < count; i++)
            {
                var renderer = renderers[i];
                if (!renderer) continue;
                var material = renderer.material;
                materials[i] = material;
                originalColors[i] = material.color;
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
            if (materials == null || originalColors == null) return;
            var count = materials.Length;
            for (var i = 0; i < count; i++)
            {
                var material = materials[i];
                if (!material) continue;
                var color = originalColors[i];
                color.a *= alphaFactor;
                material.color = color;
            }
        }

        private void ResetColors()
        {
            if (materials == null || originalColors == null) return;
            var count = materials.Length;
            for (var i = 0; i < count; i++)
            {
                var material = materials[i];
                if (!material) continue;
                material.color = originalColors[i];
            }
        }
    }
}
