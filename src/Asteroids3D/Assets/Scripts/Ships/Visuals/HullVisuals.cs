using System.Collections.Generic;
using Damage;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;

namespace Ships.Visuals
{
    /// <summary>
    /// Owns hull hit flashes and held keystone colors across the authored hull surfaces,
    /// and re-reads its colors on every wear.
    /// A held color takes priority over damage, including on hulls whose other damage effects are disabled.
    /// </summary>
    public sealed class HullVisuals : MonoBehaviour, IShipVisual, IWearsLivery
    {
        [SerializeField] private Color flashColor = new(1f, 0.65f, 0.32f);
        [SerializeField, Min(0.07f)] private float flashTime = 0.36f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int DamageFlash = Shader.PropertyToID("_DamageFlash");
        private static readonly int ContourColor = Shader.PropertyToID("_ContourColor");
        private readonly List<Renderer> surfaces = new();
        private readonly List<Color> surfaceColors = new();
        private readonly List<Renderer> contours = new();
        private readonly List<Color> contourColors = new();
        private MaterialPropertyBlock block;
        private ShipVisualRig rig;
        private Renderer[] renderers;
        private IDamageEvents source;
        private bool subscribed;
        private bool flashHeld;
        private float elapsed = float.PositiveInfinity;

        private void Awake()
        {
            rig = GetComponentInParent<ShipVisualRig>(true);
            renderers = rig.GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            Scan();
        }

        private void Scan()
        {
            surfaces.Clear();
            surfaceColors.Clear();
            contours.Clear();
            contourColors.Clear();
            surfaces.AddRange(rig.Silhouette);
            foreach (var surface in surfaces)
                surfaceColors.Add(Worn(surface, BaseColor));
            // The hangar preview strips rig parts between Awake and its wear.
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.sharedMaterial || !renderer.sharedMaterial.HasProperty(ContourColor)) continue;
                contours.Add(renderer);
                contourColors.Add(Worn(renderer, ContourColor));
            }
        }

        // A restyled slot carries the ship's own color in its slot block.
        private Color Worn(Renderer renderer, int color)
        {
            renderer.GetPropertyBlock(block, 0);
            if (!block.HasColor(color)) renderer.GetPropertyBlock(block);
            return block.HasColor(color) ? block.GetColor(color) : renderer.sharedMaterial.GetColor(color);
        }

        public void Wear(in ShipLivery livery) => Scan();

        public void Bind(in ShipView view)
        {
            Unsubscribe();
            source = view.Damage;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseFlash();
        }

        private void Subscribe()
        {
            if (subscribed || source == null) return;
            source.OnDamaged += TriggerFlash;
            source.Health.OnValueChanged += OnHealthChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            source.OnDamaged -= TriggerFlash;
            source.Health.OnValueChanged -= OnHealthChanged;
            subscribed = false;
        }

        private void OnHealthChanged(float current, float previous, float max)
        {
            if (current < max || flashHeld) return;
            elapsed = float.PositiveInfinity;
            ApplyDamageFlash(0f);
        }

        private void TriggerFlash(AppliedDamage damage)
        {
            if (damage.Hull <= 0f || flashHeld) return;
            elapsed = 0f;
        }

        private void Update()
        {
            if (flashHeld || elapsed >= flashTime) return;
            elapsed += Time.deltaTime;
            var attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.035f));
            var decay = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 0.06f) / (flashTime - 0.06f)));
            ApplyDamageFlash(attack * decay);
        }

        public void HoldFlash(Color color)
        {
            flashHeld = true;
            elapsed = float.PositiveInfinity;
            ApplyDamageFlash(0f);
            for (var i = 0; i < surfaces.Count; i++) SetSurface(i, color, 1f);
        }

        public void ReleaseFlash()
        {
            flashHeld = false;
            elapsed = float.PositiveInfinity;
            ApplyDamageFlash(0f);
        }

        private void ApplyDamageFlash(float strength)
        {
            for (var i = 0; i < surfaces.Count; i++)
                SetSurface(i, Color.Lerp(surfaceColors[i], flashColor, strength * 0.25f), strength * 0.25f);
            for (var i = 0; i < contours.Count; i++)
            {
                var contour = contours[i];
                if (!contour) continue;
                contour.GetPropertyBlock(block);
                block.SetColor(ContourColor, Color.Lerp(contourColors[i], flashColor, strength));
                contour.SetPropertyBlock(block);
            }
        }

        private void SetSurface(int index, Color color, float strength)
        {
            var surface = surfaces[index];
            if (!surface) return;
            surface.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            if (surface.sharedMaterial.HasProperty(DamageFlash)) block.SetFloat(DamageFlash, strength);
            surface.SetPropertyBlock(block);
        }
    }
}
