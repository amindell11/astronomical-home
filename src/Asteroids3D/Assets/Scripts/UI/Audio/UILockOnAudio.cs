using Combat.Targeting;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace UI.Audio
{
    /// Plays audio cues driven by LockOnSensor state changes.
    public class UILockOnAudio : MonoBehaviour
    {
        [Header("Events")]
        [Tooltip("Looping FMOD event held while the launcher is locking on a target.")]
        [SerializeField] private EventReference lockingEvent;
        [Tooltip("FMOD one-shot played when the launcher has fully locked on a target.")]
        [SerializeField] private EventReference lockedEvent;

        private ILockStateSource lockStateSource;
        private EventInstance locking;

        public void Initialize(LockOnSensor targetingComputer)
        {
            Initialize((ILockStateSource)targetingComputer);
        }

        public void Initialize(ILockStateSource targetingSource)
        {
            UnsubscribeFromSource();
            StopLocking();
            lockStateSource = targetingSource;
            if (lockStateSource == null) return;

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
            StopLocking();
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
                    StopLocking();
                    RuntimeManager.PlayOneShot(lockedEvent);
                    break;
                case LockState.Idle:
                case LockState.Cooldown:
                default:
                    StopLocking();
                    break;
            }
        }

        private void PlayLockingLoop()
        {
            if (locking.isValid()) return;
            locking = RuntimeManager.CreateInstance(lockingEvent);
            locking.start();
        }

        private void StopLocking()
        {
            if (!locking.isValid()) return;
            locking.stop(STOP_MODE.ALLOWFADEOUT);
            locking.release();
            locking = default;
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
