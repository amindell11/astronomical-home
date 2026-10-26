using Combat.Targeting;
using Ships.Presentation;
using Substrate;
using UnityEngine;

namespace Ships.Visuals
{
    /// <summary>
    /// Place on a Ship's MapMesh child. Switches this GameObject to the Minimap_Enemy
    /// layer for non-player ships. Player-ness is injected via <see cref="ShipView.IsPlayer"/>
    /// at bind time (see <see cref="IShipVisual"/>) rather than discovered from the parent ship.
    /// An enemy's marker hides while its ship is concealed.
    /// </summary>
    public class MinimapShipMarker : MonoBehaviour, IShipVisual
    {
        private Renderer marker;
        private LockChannel enemyLock;

        private void Awake() => marker = GetComponent<Renderer>();

        public void Bind(in ShipView view)
        {
            if (view.IsPlayer) return;
            enemyLock = view.Lock;
            var layer = LayerIds.MinimapEnemy;
            if (layer >= 0) gameObject.layer = layer;
        }

        private void LateUpdate()
        {
            if (enemyLock != null && marker) marker.enabled = !enemyLock.IsConcealed;
        }
    }
}
