using Ships.Command;
using UnityEngine;

namespace UI.Audio
{
    /// Plays a one-shot cue at the boost-ready edge. Polls the bound IShipStatus.
    [RequireComponent(typeof(AudioSource))]
    public class UIBoostAudio : MonoBehaviour
    {
        [Tooltip("One-shot played when boost comes off cooldown.")]
        [SerializeField] private AudioClip boostReadyClip;

        [Tooltip("Volume for the boost-ready cue.")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private AudioSource source;
        private IShipStatus status;
        private bool wasAvailable;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
        }

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
            if (available && !wasAvailable && boostReadyClip)
                source.PlayOneShot(boostReadyClip, volume);
            wasAvailable = available;
        }
    }
}
