using UnityEngine;
using Substrate;
using Substrate.Presentation;

namespace Ships.Presentation
{
    /// <summary>
    /// Root of a ship's visual rig (hull renderer, particles, light, audio, canvases), authored as a
    /// child of the ship prefab. On <see cref="Start"/> it discovers its parent <see cref="Ship"/> and
    /// fans that ship's <see cref="ShipView"/> out to every <see cref="IShipVisual"/> child. The unit
    /// service applies the session's presentation policy to the whole ship when it wires it, and this
    /// part owns the cheapest gate available: deactivating the subtree, so a dark rig never reaches
    /// <see cref="Start"/>, binds, renders, ticks, or plays audio while the ship stays fully simulated.
    /// </summary>
    public sealed class ShipVisualRig : MonoBehaviour, IPresentationPart
    {
        private IShipVisual[] visuals;

        private void Awake() => visuals = GetComponentsInChildren<IShipVisual>(true);

        public void ApplyPresentation(bool visible) => gameObject.SetActive(visible);

        // Self-wire from the parent ship once all sim components have Awoken. Runs only when this
        // subtree is active — a darkened rig never reaches Start.
        private void Start()
        {
            var ship = GetComponentInParent<Ship>();
            if (!ship) return;

            Bind(new ShipView(
                ship.transform,
                ship.Damage,
                () => ship.Movement.CurrentCommand,
                ship.Lock,
                ship.CompareTag(TagNames.Player)));
        }

        public void Bind(in ShipView view)
        {
            foreach (var visual in visuals)
                visual.Bind(view);
        }
    }
}
