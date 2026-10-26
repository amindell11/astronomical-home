using System.Collections.Generic;
using UnityEngine;
using Substrate;
using Substrate.Presentation;

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
        private Ship ship;
        private CanvasGroup hud;

        public IReadOnlyList<Renderer> Silhouette => silhouette;

        private void Awake()
        {
            visuals = GetComponentsInChildren<IShipVisual>(true);
            ship = GetComponentInParent<Ship>();
            hud = gameObject.AddComponent<CanvasGroup>();
        }

        public void SetHudOpacity(float opacity) => hud.alpha = opacity;

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
