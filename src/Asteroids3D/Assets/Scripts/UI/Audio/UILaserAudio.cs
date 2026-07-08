using Combat.Conditions;
using UnityEngine;

namespace UI.Audio
{
    /// <summary>
    /// Plays audio cues driven by heat readout overheat events.
    /// Subscribes to the readout's OnOverheat and OnCooldownStart events.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class UILaserAudio : MonoBehaviour
    {
        [Header("Clips")]
        [Tooltip("Sound that plays when the laser gun overheats.")]
        [SerializeField] private AudioClip overheatClip;
        [Tooltip("Optional sound that plays when the laser gun starts cooling down from overheat.")]
        [SerializeField] private AudioClip cooldownClip;

        [Header("Settings")]
        [Tooltip("Volume for overheat sound effects.")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private AudioSource audioSource;
        private IHeatReadout heat;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        void OnDestroy()
        {
            if (heat != null)
            {
                heat.OnOverheat -= PlayOverheatSound;
                heat.OnCooldownStart -= PlayCooldownSound;
            }
        }

        public void Initialize(IHeatReadout heat)
        {
            if (this.heat != null)
            {
                this.heat.OnOverheat -= PlayOverheatSound;
                this.heat.OnCooldownStart -= PlayCooldownSound;
            }

            this.heat = heat;
            if (this.heat == null) return;

            this.heat.OnOverheat += PlayOverheatSound;
            this.heat.OnCooldownStart += PlayCooldownSound;
        }

        void PlayOverheatSound()
        {
            if (overheatClip && audioSource)
            {
                audioSource.PlayOneShot(overheatClip, volume);
            }
        }

        void PlayCooldownSound()
        {
            if (cooldownClip && audioSource)
            {
                audioSource.PlayOneShot(cooldownClip, volume);
            }
        }
    }
} 
