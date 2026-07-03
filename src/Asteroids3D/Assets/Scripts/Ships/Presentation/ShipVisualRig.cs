using UnityEngine;

namespace Ships.Presentation
{
    /// <summary>
    /// Root of a ship's visual rig (hull renderer, particles, light, audio, canvases), authored as a
    /// child of the ship prefab. On <see cref="Start"/> it discovers its parent <see cref="Ship"/> and
    /// fans that ship's <see cref="ShipView"/> out to every <see cref="IShipVisual"/> child. A
    /// headless/RL session leaves this subtree disabled (see the game-tier presentation gate), so it
    /// never binds, renders, or ticks while the ship stays fully simulated.
    /// </summary>
    public sealed class ShipVisualRig : MonoBehaviour
    {
        private IShipVisual[] visuals;

        // Component discovery happens once in Awake; Bind only fans a view out to the cached children.
        private void Awake() => visuals = GetComponentsInChildren<IShipVisual>(true);

        // Self-wire from the parent ship once all sim components have Awoken. Runs only when this
        // subtree is active — a gated-off (headless) rig never reaches Start.
        private void Start()
        {
            var ship = GetComponentInParent<Ship>();
            if (!ship) return;

            Bind(new ShipView(
                ship.transform,
                ship.Damage,
                () => ship.Movement.CurrentCommand,
                ship.Lock));
        }

        public void Bind(in ShipView view)
        {
            foreach (var visual in visuals)
                visual.Bind(view);
        }
    }
}
