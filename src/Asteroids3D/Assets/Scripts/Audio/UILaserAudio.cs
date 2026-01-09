using UnityEngine;
using Utils;
using Weapons;
using Ships.Weapons.Conditions;

namespace Audio
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
        private LaserGun laserGun;
        private Heat heat;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;

            // Attempt to find the player's laser gun at startup
            TryAssignLaserGun();
        }

        void Start()
        {
            // Try again in Start in case player wasn't ready in Awake
            if (!laserGun)
            {
                TryAssignLaserGun();
            }
        }

        void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (heat)
            {
                heat.OnOverheat -= PlayOverheatSound;
                heat.OnCooldownStart -= PlayCooldownSound;
            }
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

        void TryAssignLaserGun()
        {
            var playerObj = GameObject.FindGameObjectWithTag(TagNames.Player);
            if (playerObj)
            {
                laserGun = playerObj.GetComponentInChildren<LaserGun>();
                heat = laserGun ? laserGun.GetComponent<Heat>() : null;
                
                // Subscribe to events
                if (heat)
                {
                    heat.OnOverheat += PlayOverheatSound;
                    heat.OnCooldownStart += PlayCooldownSound;
                }
            }
        }
    }
} 