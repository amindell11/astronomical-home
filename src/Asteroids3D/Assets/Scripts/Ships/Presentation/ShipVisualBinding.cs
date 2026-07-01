using UnityEngine;

namespace Ships.Presentation
{
    /// <summary>
    /// Data-only declaration co-located on a sim ship prefab: names the <see cref="ShipVisualRig"/>
    /// prefab to attach when presentation is enabled. The sim never reads this — only the game-tier
    /// presentation installer does — so stripping it leaves ship behavior unchanged.
    ///
    /// This is the interim ship→rig mapping. The longer-term plan is a ShipType archetype asset that
    /// bundles identity + settings + rig (tracked on the project board); the installer's shape does not
    /// change when that lands.
    /// </summary>
    public sealed class ShipVisualBinding : MonoBehaviour
    {
        [SerializeField] private ShipVisualRig visualRigPrefab;

        /// <summary>The visual-rig prefab to instantiate for this ship, or null for none.</summary>
        public ShipVisualRig VisualRigPrefab => visualRigPrefab;
    }
}
