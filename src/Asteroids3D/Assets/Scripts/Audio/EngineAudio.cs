using ShipMain;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Engine audio system that plays separate loops for thrust and strafe movements.
    /// Thrust audio plays at reduced volume when in reverse.
    /// Audio only plays when the ship has valid commands and is actually moving.
    /// AudioSources should have their clips assigned directly in the Inspector.
    /// </summary>
    public sealed class EngineAudio : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource thrustSource;
        [SerializeField] private AudioSource strafeSource;

        [Header("Volume Settings")]
        [SerializeField, Range(0f, 1f)] private float thrustVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float strafeVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float reverseVolumeMultiplier = 0.5f;

        [Header("Pitch Modulation")]
        [Tooltip("Optional pitch modulation based on input intensity (0–1 → pitch)")]
        [SerializeField] private AnimationCurve inputToPitch = AnimationCurve.Linear(0, 1, 1, 1.3f);

        private Ship ship;
        private bool audioInitialized = false;

        void Awake()
        {
            ship = GetComponentInParent<Ship>();
        }

        void OnEnable()
        {
            // Initialize audio sources but don't play yet
            InitializeAudioSource(thrustSource);
            InitializeAudioSource(strafeSource);
            audioInitialized = true;
        }

        void InitializeAudioSource(AudioSource source)
        {
            if (source)
            {
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 1f; // 3-D positional
                source.volume = 0f; // Start silent
                if (source.clip)
                    source.Play(); // Play but at zero volume
            }
        }

        void Update()
        {
            if (!audioInitialized || ship == null || thrustSource == null || strafeSource == null) 
                return;

            float thrust = ship.CurrentCommand.Thrust;
            float strafe = ship.CurrentCommand.Strafe;

            // Calculate thrust volume (reduced for reverse)
            float thrustIntensity = Mathf.Abs(thrust);
            float finalThrustVolume = thrustIntensity * thrustVolume;
            if (thrust < 0f) // Reverse thrust
            {
                finalThrustVolume *= reverseVolumeMultiplier;
            }

            // Calculate strafe volume
            float strafeIntensity = Mathf.Abs(strafe);
            float finalStrafeVolume = strafeIntensity * strafeVolume;

            // Apply volumes
            thrustSource.volume = finalThrustVolume;
            strafeSource.volume = finalStrafeVolume;

            // Apply pitch modulation based on combined movement intensity
            float combinedIntensity = Mathf.Clamp01(thrustIntensity + strafeIntensity);
            float pitch = Mathf.Clamp(inputToPitch.Evaluate(combinedIntensity), 0.1f, 3f);
            thrustSource.pitch = pitch;
            strafeSource.pitch = pitch;
        }
    }
} 