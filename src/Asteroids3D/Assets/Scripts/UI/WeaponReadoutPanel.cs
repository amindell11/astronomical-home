using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// One weapon's block of the HUD: the weapon's display name above a stack of readout
    /// widgets. <see cref="WeaponReadoutBuilder"/> generates one per equipped slot and fills
    /// <see cref="Container"/> with that weapon's widgets.
    /// </summary>
    public sealed class WeaponReadoutPanel : MonoBehaviour
    {
        [Tooltip("Label showing the weapon's display name.")]
        [SerializeField] private Text nameLabel;

        [Tooltip("Parent for this weapon's readout widgets. Defaults to this transform.")]
        [SerializeField] private Transform widgetContainer;

        /// <summary>Where this panel's widgets are instantiated.</summary>
        public Transform Container => widgetContainer ? widgetContainer : transform;

        public void Initialize(string displayName)
        {
            if (nameLabel)
                nameLabel.text = displayName;
        }
    }
}
