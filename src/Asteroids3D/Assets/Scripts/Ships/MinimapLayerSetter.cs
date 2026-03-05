using UnityEngine;

namespace Ships
{
    /// <summary>
    /// Place on a Ship's MapMesh child.
    /// After the ship is tagged, switches this GameObject's layer
    /// to Minimap_Enemy if the parent ship is not the player.
    /// </summary>
    public class MinimapLayerSetter : MonoBehaviour
    {
        private void Start()
        {
            var ship = GetComponentInParent<Ship>();
            if (!ship) return;

            if (!ship.CompareTag("Player"))
            {
                int layer = LayerMask.NameToLayer("Minimap_Enemy");
                if (layer >= 0) gameObject.layer = layer;
            }
        }
    }
}
