using System;
using System.Collections.Generic;
using UnityEngine;
using Substrate;
using Substrate.Presentation;
using Ships.Visuals;

namespace Ships.Presentation
{
    /// <summary>
    /// Root of a ship's visual rig (hull renderer, particles, light, audio, canvases), authored as a
    /// child of the ship prefab. It binds the ship's view to each visual and fades rig canvases.
    /// The unit service applies the session's presentation policy when it wires the ship, and this
    /// part owns the cheapest gate available: deactivating the subtree, so a dark rig never reaches
    /// <see cref="Start"/>, binds, renders, ticks, or plays audio while the ship stays fully simulated.
    /// </summary>
    public sealed class ShipVisualRig : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Every hull renderer. The hangar preview frames the ship from their stored mesh bounds.")]
        [SerializeField] private Renderer[] silhouette;

        private IShipVisual[] visuals;
        private IWearsLivery[] liveryParts;
        private Ship ship;
        private CanvasGroup hud;
        private MinimapShipMarker minimapMarker;

        public IReadOnlyList<Renderer> Silhouette => silhouette;

        private void Awake()
        {
            visuals = GetComponentsInChildren<IShipVisual>(true);
            liveryParts = GetComponentsInChildren<IWearsLivery>(true);
            ship = GetComponentInParent<Ship>();
            hud = gameObject.AddComponent<CanvasGroup>();
            minimapMarker = GetComponentInChildren<MinimapShipMarker>(true);
        }

        public void SetHudOpacity(float opacity) => hud.alpha = opacity;

        // A dark rig never woke, so it has no marker to pulse.
        public void PulseMinimapMarker(bool pulsing)
        {
            if (minimapMarker) minimapMarker.Pulsing = pulsing;
        }

        public void Wear(in ShipLivery livery)
        {
            if (livery.Hull.Count > 0) PaintHull(livery.Hull);
            foreach (var part in liveryParts)
                part.Wear(livery);
        }

        private void PaintHull(IReadOnlyList<Material> hull)
        {
            var slots = 0;
            foreach (var surface in silhouette)
                slots += surface.sharedMaterials.Length;
            if (slots != hull.Count)
                throw new InvalidOperationException(
                    $"'{name}' has {slots} hull material slots; the livery paints {hull.Count}.");

            var next = 0;
            foreach (var surface in silhouette)
            {
                var materials = surface.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = hull[next++];
                surface.sharedMaterials = materials;
            }
        }

        public void ApplyPresentation(bool visible) => gameObject.SetActive(visible);

        private void Start()
        {
            if (!ship) return;

            Bind(new ShipView(
                ship.transform,
                ship.Damage,
                () => ship.Movement.CurrentCommand,
                ship.Lock,
                ship.CompareTag(TagNames.Player),
                ship.Body,
                () => ship.BoostAvailable));
        }

        public void Bind(in ShipView view)
        {
            foreach (var visual in visuals)
                visual.Bind(view);
        }
    }
}
