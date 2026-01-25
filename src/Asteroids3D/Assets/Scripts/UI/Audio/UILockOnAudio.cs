using Combat.Targeting;
using UnityEngine;

namespace UI.Audio
{
    /// Plays audio cues driven by TargetingComputer state changes.
    [RequireComponent(typeof(AudioSource))]
    public class UILockOnAudio : MonoBehaviour
    {
        [Header("Clips")]
        [Tooltip("Looping sound that plays while the launcher is locking on a target.")]
        [SerializeField] private AudioClip lockingLoopClip;
        [Tooltip("One-shot sound that plays when the launcher has fully locked on a target.")]
        [SerializeField] private AudioClip lockedClip;

        [Header("Settings")]
        [Tooltip("Volume for lock-on sound effects.")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private AudioSource source;
        private TargetingComputer targeting;
        private LockState lastState = LockState.Idle;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop        = false;
        }

        public void Initialize(TargetingComputer targetingComputer)
        {
            targeting = targetingComputer;
            lastState = targeting.State;
        }

        private void OnDisable()
        {
            StopAudio();
        }

        private void Update()
        {
            if (!targeting) return;

            var currentState = targeting.State;
            if (currentState == lastState) return;
            HandleStateChange(currentState);
            lastState = currentState;
        }

        private void HandleStateChange(LockState newState)
        {
            switch (newState)
            {
                case LockState.Locking:
                    PlayLockingLoop();
                    break;
                case LockState.Locked:
                    PlayLockedClip();
                    break;
                case LockState.Idle:
                case LockState.Cooldown:
                default: // Idle, Cooldown, etc.
                    StopAudio();
                    break;
            }
        }

        private void PlayLockingLoop()
        {
            if (!lockingLoopClip) return;

            source.loop = true;
            source.clip = lockingLoopClip;
            source.volume = volume;
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void PlayLockedClip()
        {
            // Ensure any looping clip is halted first
            StopAudio();
            if (lockedClip)
            {
                source.PlayOneShot(lockedClip, volume);
            }
        }

        private void StopAudio()
        {
            source.loop = false;
            source.Stop();
            source.clip = null;
        }

    }
}
