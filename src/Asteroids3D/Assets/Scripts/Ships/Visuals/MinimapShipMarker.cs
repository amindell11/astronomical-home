using Ships.Presentation;
using Substrate;
using UnityEngine;

namespace Ships.Visuals
{
    /// <summary>
    /// Place on a Ship's MapMesh child. Switches this GameObject to the Minimap_Enemy
    /// layer for non-player ships. Player-ness is injected via <see cref="ShipView.IsPlayer"/>
    /// at bind time (see <see cref="IShipVisual"/>) rather than discovered from the parent ship.
    /// </summary>
    public class MinimapShipMarker : MonoBehaviour, IShipVisual
    {
        public void Bind(in ShipView view)
        {
            if (view.IsPlayer) return;
            var layer = LayerIds.MinimapEnemy;
            if (layer >= 0) gameObject.layer = layer;
        }
    }
}
