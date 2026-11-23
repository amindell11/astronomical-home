using UnityEngine;

namespace UI
{
    /// <summary>
    /// One weapon's block of the HUD: a stack of readout widgets.
    /// <see cref="Weapons.WeaponReadoutBuilder"/> creates these for lock feedback.
    /// </summary>
    public sealed class WeaponReadoutPanel : MonoBehaviour
    {
        [Tooltip("Parent for this weapon's readout widgets. Defaults to this transform.")]
        [SerializeField] private Transform widgetContainer;

        /// <summary>Where this panel's widgets are instantiated.</summary>
        public Transform Container => widgetContainer ? widgetContainer : transform;
    }
}
