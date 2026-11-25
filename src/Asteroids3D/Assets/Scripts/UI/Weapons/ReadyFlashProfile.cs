using UnityEngine;

namespace UI.Weapons
{
    /// <summary>
    /// The named knobs of the HUD's ready flash, the brighten-and-fade on a weapon's arc when it
    /// recovers. The game reads the asset its HUD serializes; the preview host's HUD target edits it.
    /// </summary>
    [CreateAssetMenu(fileName = "ReadyFlash", menuName = "Game/Ready Flash")]
    public sealed class ReadyFlashProfile : ScriptableObject
    {
        [Tooltip("Seconds the flash takes to fade out after a weapon recovers.")]
        [SerializeField, Min(.01f)] private float seconds = .25f;

        [Tooltip("Opacity the flash starts at: 1 lights the weapon's whole arc in full ink.")]
        [SerializeField, Range(0f, 1f)] private float intensity = 1f;

        public float Seconds => seconds;
        public float Intensity => intensity;
    }
}
