using Combat.Targeting;
using UnityEngine;

namespace UI.Audio
{
    /// Plays audio cues driven by LockOnSensor state changes.
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
        private ILockStateSource lockStateSource;

        private void Awake()
        {
            EnsureSource();
        }

        public void Initialize(LockOnSensor targetingComputer)
        {
            Initialize((ILockStateSource)targetingComputer);
        }

        public void Initialize(ILockStateSource targetingSource)
        {
            UnsubscribeFromSource();
            lockStateSource = targetingSource;
            if (lockStateSource == null)
            {
                StopAudio();
                return;
            }

            if (isActiveAndEnabled)
                SubscribeToSource();
            HandleStateChange(lockStateSource.State);
        }

        private void OnEnable()
        {
            SubscribeToSource();
            if (lockStateSource != null)
                HandleStateChange(lockStateSource.State);
        }

        private void OnDisable()
        {
            UnsubscribeFromSource();
            StopAudio();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSource();
            lockStateSource = null;
        }

        private void HandleStateChanged(LockState _, LockState next)
        {
            HandleStateChange(next);
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
            if (!EnsureSource()) return;

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
            if (!lockedClip || !EnsureSource())
                return;

            source.PlayOneShot(lockedClip, volume);
        }

        private void StopAudio()
        {
            if (!EnsureSource()) return;
            source.loop = false;
            source.Stop();
            source.clip = null;
        }

        private bool EnsureSource()
        {
            if (source)
                return true;

            source = GetComponent<AudioSource>();
            if (!source)
                return false;

            source.playOnAwake = false;
            source.loop = false;
            return true;
        }

        private void SubscribeToSource()
        {
            if (lockStateSource == null) return;
            lockStateSource.OnStateChanged -= HandleStateChanged;
            lockStateSource.OnStateChanged += HandleStateChanged;
        }

        private void UnsubscribeFromSource()
        {
            if (lockStateSource == null) return;
            lockStateSource.OnStateChanged -= HandleStateChanged;
        }
    }
}
