using System;
using Ships.Command;
using Ships.Presentation;
using UnityEngine;

namespace Ships.Audio
{
    /// <summary>
    /// Engine audio system that plays separate loops for thrust and strafe movements.
    /// Thrust audio plays at reduced volume when in reverse.
    /// AudioSources should have their clips assigned directly in the Inspector.
    /// The live pilot command is injected via <see cref="Bind"/> (see <see cref="IShipVisual"/>).
    /// </summary>
    public sealed class EngineAudio : MonoBehaviour, IShipVisual
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource thrustSource;
        [SerializeField] private AudioSource strafeSource;

        [Header("Volume Settings")]
        [SerializeField, Range(0f, 1f)] private float thrustVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float strafeVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float reverseVolumeMultiplier = 0.5f;

        [Header("Pitch Modulation")]
        [Tooltip("Optional pitch modulation based on input intensity (0-1 -> pitch)")]
        [SerializeField] private AnimationCurve inputToPitch = AnimationCurve.Linear(0, 1, 1, 1.3f);

        private Func<PilotCommand> command;
        private bool audioInitialized;

        public void Bind(in ShipView view) => command = view.Command;

        private void OnEnable()
        {
            // Initialize audio sources but don't play yet
            InitializeAudioSource(thrustSource);
            InitializeAudioSource(strafeSource);
            audioInitialized = true;
        }

        private void InitializeAudioSource(AudioSource source)
        {
            if (!source) return;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f; // 3-D positional
            source.volume = 0f; // Start silent
            if (source.clip)
                source.Play(); // Play but at zero volume
        }

        private void Update()
        {
            if (!audioInitialized || command == null || !thrustSource || !strafeSource)
                return;

            var cmd = command();
            var thrust = cmd.thrust;
            var strafe = cmd.strafe;

            // Calculate thrust volume (reduced for reverse)
            var thrustIntensity = Mathf.Abs(thrust);
            var finalThrustVolume = thrustIntensity * thrustVolume;
            if (thrust < 0f) // Reverse thrust
            {
                finalThrustVolume *= reverseVolumeMultiplier;
            }

            // Calculate strafe volume
            var strafeIntensity = Mathf.Abs(strafe);
            var finalStrafeVolume = strafeIntensity * strafeVolume;

            // Apply volumes
            thrustSource.volume = finalThrustVolume;
            strafeSource.volume = finalStrafeVolume;

            // Apply pitch modulation based on combined movement intensity
            var combinedIntensity = Mathf.Clamp01(thrustIntensity + strafeIntensity);
            var pitch = Mathf.Clamp(inputToPitch.Evaluate(combinedIntensity), 0.1f, 3f);
            thrustSource.pitch = pitch;
            strafeSource.pitch = pitch;
        }
    }
}
