using UnityEngine;

namespace Ships.Presentation
{
    /// <summary>
    /// Root of a ship's visual-rig prefab (hull renderer, particles, light, audio, canvases). The
    /// game-tier presentation installer instantiates it under a sim ship and calls <see cref="Bind"/>,
    /// which fans the ship's <see cref="ShipView"/> out to every <see cref="IShipVisual"/> child. Holds
    /// no sim references of its own, and the sim ship holds no reference to it — the coupling is
    /// one-directional and established only at attach time.
    /// </summary>
    public sealed class ShipVisualRig : MonoBehaviour
    {
        private IShipVisual[] visuals;

        // Component discovery happens once in Awake (runs during Instantiate, before the installer's
        // Bind call); Bind only fans the injected view out to the cached children.
        private void Awake() => visuals = GetComponentsInChildren<IShipVisual>(true);

        public void Bind(in ShipView view)
        {
            foreach (var visual in visuals)
                visual.Bind(view);
        }
    }
}
