using Combat.Conditions;
using UnityEngine;

namespace UI.Audio
{
    /// <summary>
    /// Plays audio cues driven by Heat condition overheat events.
    /// Subscribes to the Heat's OnOverheat and OnCooldownStart events.
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
        private Heat heat;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        void OnDestroy()
        {
            if (heat)
            {
                heat.OnOverheat -= PlayOverheatSound;
                heat.OnCooldownStart -= PlayCooldownSound;
            }
        }

        public void Initialize(Heat heat)
        {
            if (this.heat)
            {
                this.heat.OnOverheat -= PlayOverheatSound;
                this.heat.OnCooldownStart -= PlayCooldownSound;
            }

            this.heat = heat;
            if (!this.heat) return;

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
