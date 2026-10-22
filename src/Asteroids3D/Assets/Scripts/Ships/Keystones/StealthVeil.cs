using System;
using System.Collections.Generic;
using Ships.Visuals;
using Substrate;
using Substrate.Presentation;
using UI.PlayerState;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// Stealth's tell: while the kit is active the hull dithers out through the hull shader's dissolve,
    /// to half for the pilot's own ship and nearly gone for an enemy, whose minimap marker and status
    /// bars also hide, so the player loses what the AI loses.
    /// </summary>
    public sealed class StealthVeil : MonoBehaviour, IPresentationPart
    {
        private static readonly int Visibility = Shader.PropertyToID("_DebrisVisibility");

        [SerializeField] private Keystone keystone;

        [Tooltip("How much of the hull still draws while concealed, on the player's own ship.")]
        [SerializeField, Range(0f, 1f)] private float ownVisibility = 0.5f;

        [Tooltip("How much of the hull still draws while concealed, on an enemy ship.")]
        [SerializeField, Range(0f, 1f)] private float enemyVisibility = 0.15f;

        private Ship ship;
        private IReadOnlyList<Renderer> hull = Array.Empty<Renderer>();
        private readonly List<GameObject> enemyMarks = new();
        private MaterialPropertyBlock block;
        private bool veiled;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            ship = GetComponentInParent<Ship>();
            if (!ship || !ship.VisualRig) return;
            hull = ship.VisualRig.Silhouette;
            foreach (var marker in ship.VisualRig.GetComponentsInChildren<MinimapShipMarker>(true)) enemyMarks.Add(marker.gameObject);
            foreach (var bar in ship.VisualRig.GetComponentsInChildren<StatusBarUI>(true)) enemyMarks.Add(bar.gameObject);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable() => Veil(false);

        private void LateUpdate() => Veil(keystone.Active);

        private void Veil(bool on)
        {
            if (on == veiled || !ship) return;
            veiled = on;
            var isPlayer = ship.CompareTag(TagNames.Player);
            var visibility = on ? (isPlayer ? ownVisibility : enemyVisibility) : 1f;
            foreach (var renderer in hull)
            {
                if (!renderer) continue;
                renderer.GetPropertyBlock(block);
                block.SetFloat(Visibility, visibility);
                renderer.SetPropertyBlock(block);
            }
            if (isPlayer) return;
            foreach (var mark in enemyMarks)
                if (mark) mark.SetActive(!on);
        }
    }
}
