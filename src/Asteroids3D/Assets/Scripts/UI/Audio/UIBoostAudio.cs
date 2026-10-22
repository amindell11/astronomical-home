using FMODUnity;
using Ships.Command;
using UnityEngine;

namespace UI.Audio
{
    /// Plays a one-shot cue at the boost-ready edge. Polls the bound IShipStatus.
    public class UIBoostAudio : MonoBehaviour
    {
        [Tooltip("FMOD one-shot played when boost comes off cooldown.")]
        [SerializeField] private EventReference boostReadyEvent;

        private IShipStatus status;
        private bool wasAvailable;

        /// <summary>Re-bindable: seeds the edge detector so a spawn with boost ready stays silent.</summary>
        public void Initialize(IShipStatus status)
        {
            this.status = status;
            wasAvailable = status != null && status.BoostAvailable;
        }

        private void Update()
        {
            if (status == null) return;

            var available = status.BoostAvailable;
            if (available && !wasAvailable)
                RuntimeManager.PlayOneShot(boostReadyEvent);
            wasAvailable = available;
        }
    }
}
