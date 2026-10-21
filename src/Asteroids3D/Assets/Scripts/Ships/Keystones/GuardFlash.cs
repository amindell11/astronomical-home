using System;
using System.Collections.Generic;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// The Guard Dash tell: the hull holds a white flash for exactly the guard window. Rides the kit
    /// prefab, so the ship's presentation policy reaches it like any other part of the ship.
    /// </summary>
    public sealed class GuardFlash : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private GuardDash guard;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int DamageFlash = Shader.PropertyToID("_DamageFlash");

        private IReadOnlyList<Renderer> hull = Array.Empty<Renderer>();
        private Color[] heldColors = Array.Empty<Color>();
        private float[] heldFlash = Array.Empty<float>();
        private MaterialPropertyBlock block;
        private bool lit;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            var ship = GetComponentInParent<Ship>();
            if (!ship || !ship.VisualRig) return;
            hull = ship.VisualRig.Silhouette;
            heldColors = new Color[hull.Count];
            heldFlash = new float[hull.Count];
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable() => Light(false);

        private void LateUpdate() => Light(guard.Guarding);

        private void Light(bool on)
        {
            if (on == lit) return;
            lit = on;
            for (var i = 0; i < hull.Count; i++)
            {
                var renderer = hull[i];
                if (!renderer) continue;
                renderer.GetPropertyBlock(block);
                if (on)
                {
                    heldColors[i] = block.HasColor(BaseColor) ? block.GetColor(BaseColor) : renderer.sharedMaterial.HasProperty(BaseColor) ? renderer.sharedMaterial.GetColor(BaseColor) : Color.white;
                    heldFlash[i] = block.HasFloat(DamageFlash) ? block.GetFloat(DamageFlash) : 0f;
                    block.SetColor(BaseColor, Color.white);
                    block.SetFloat(DamageFlash, 1f);
                }
                else
                {
                    block.SetColor(BaseColor, heldColors[i]);
                    block.SetFloat(DamageFlash, heldFlash[i]);
                }
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
