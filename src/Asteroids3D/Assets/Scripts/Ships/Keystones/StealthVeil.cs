using System;
using System.Collections.Generic;
using FMODUnity;
using Ships.Keystones.Kits;
using Substrate;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// Stealth's tell: while the kit is active the hull dithers out through the hull shader's dissolve,
    /// to half for the pilot's own ship and nearly gone for an enemy. Over the kit's tell the hull
    /// shimmers between that concealed value and a brighter one, and a one-shot plays as the shimmer
    /// starts, unless the sound is unset. An enemy's minimap marker and
    /// status bars hide themselves off the same concealment flag.
    /// </summary>
    public sealed class StealthVeil : MonoBehaviour, IPresentationPart
    {
        private static readonly int Visibility = Shader.PropertyToID("_DebrisVisibility");

        [SerializeField] private Stealth keystone;

        [Tooltip("How much of the hull still draws while concealed, on the player's own ship.")]
        [SerializeField, Range(0f, 1f)] private float ownVisibility = 0.5f;

        [Tooltip("How much of the hull still draws while concealed, on an enemy ship.")]
        [SerializeField, Range(0f, 1f)] private float enemyVisibility = 0.15f;

        [Tooltip("How much of the hull draws on the bright half of the tell.")]
        [SerializeField, Range(0f, 1f)] private float tellVisibility = 0.8f;

        [Tooltip("Times per second the tell swaps the hull between its concealed value and the bright value.")]
        [SerializeField] private float tellHz = 12f;

        [Tooltip("Plays once as the tell starts. Empty until authored.")]
        [SerializeField] private EventReference tellSound;

        private Ship ship;
        private IReadOnlyList<Renderer> hull = Array.Empty<Renderer>();
        private MaterialPropertyBlock block;
        private bool telling;
        private float drawn = 1f;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            ship = GetComponentInParent<Ship>();
            if (!ship || !ship.VisualRig) return;
            hull = ship.VisualRig.Silhouette;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            telling = false;
            Draw(1f);
        }

        private void LateUpdate()
        {
            if (!ship) return;
            var now = keystone.Telling;
            if (now && !telling) PlayTell();
            telling = now;
            Draw(HullVisibility(now));
        }

        private float HullVisibility(bool tellingNow)
        {
            if (!keystone.Active) return 1f;
            var concealed = ship.CompareTag(TagNames.Player) ? ownVisibility : enemyVisibility;
            if (!tellingNow) return concealed;
            var bright = Mathf.Repeat(Time.time * tellHz, 1f) < 0.5f;
            return bright ? tellVisibility : concealed;
        }

        private void Draw(float visibility)
        {
            if (visibility == drawn || !ship) return;
            drawn = visibility;
            foreach (var renderer in hull)
            {
                if (!renderer) continue;
                renderer.GetPropertyBlock(block);
                block.SetFloat(Visibility, visibility);
                renderer.SetPropertyBlock(block);
            }
        }

        private void PlayTell()
        {
            if (tellSound.IsNull) return;
            var instance = RuntimeManager.CreateInstance(tellSound);
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(ship.transform, ship.Rigidbody));
            instance.start();
            instance.release();
        }
    }
}
