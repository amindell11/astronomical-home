using System;

namespace Combat.Targeting
{
    public class TargetLock
    {
        private readonly float lockOnTime;
        private readonly float lockExpiry;
        private readonly Func<bool> canFireCheck;

        private float lockTimer;
        private float lockDuration;

        public event Action<LockState, LockState> OnStateChanged;

        public LockState State { get; private set; } = LockState.Idle;
        public ITargetable CurrentTarget { get; private set; }
        public float LockProgress => State == LockState.Locking && lockOnTime > 0f ? UnityEngine.Mathf.Clamp01(lockTimer / lockOnTime) : 0f;
        public bool IsLocked => State == LockState.Locked;

        public TargetLock(float lockOnTime, float lockExpiry, Func<bool> canFireCheck)
        {
            this.lockOnTime = lockOnTime;
            this.lockExpiry = lockExpiry;
            this.canFireCheck = canFireCheck;
        }

        public bool CanStartNewLock() => State == LockState.Idle && canFireCheck();

        public void Update(float deltaTime, bool isAcquired)
        {
            switch (State)
            {
                case LockState.Idle:
                    break;
                case LockState.Locking:
                    UpdateLockingState(deltaTime, isAcquired);
                    break;
                case LockState.Locked:
                    UpdateLockedState(deltaTime, isAcquired);
                    break;
                case LockState.Cooldown:
                    UpdateCooldownState();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool TryStartLock(ITargetable candidate)
        {
            if (candidate == null || State != LockState.Idle) return false;

            CurrentTarget = candidate;
            lockTimer = 0f;
            SetState(LockState.Locking);
            return true;
        }

        public ITargetable ConsumeLock()
        {
            if (!IsLocked) return null;

            var lockedTarget = CurrentTarget;
            ResetLock();
            SetState(LockState.Cooldown);
            return lockedTarget;
        }

        private void UpdateLockingState(float deltaTime, bool isAcquired)
        {
            if (!isAcquired)
            {
                CancelLock();
                return;
            }

            lockTimer += deltaTime;
            CurrentTarget?.Lock.RaiseProgress(LockProgress);

            if (lockTimer < lockOnTime) return;

            SetState(LockState.Locked);
            lockDuration = 0f;
            CurrentTarget?.Lock.RaiseAcquired();
        }

        private void UpdateLockedState(float deltaTime, bool isAcquired)
        {
            lockDuration += deltaTime;
            var lockExpired = lockDuration > lockExpiry;

            if (lockExpired || !isAcquired)
            {
                CancelLock();
            }
        }

        private void UpdateCooldownState()
        {
            if (canFireCheck())
                SetState(LockState.Idle);
        }

        private void CancelLock()
        {
            ResetLock();
            SetState(LockState.Idle);
        }

        private void ResetLock()
        {
            CurrentTarget?.Lock.RaiseReleased();
            CurrentTarget = null;
            lockTimer = 0f;
            lockDuration = 0f;
        }

        private void SetState(LockState newState)
        {
            if (State == newState) return;
            var previous = State;
            State = newState;
            OnStateChanged?.Invoke(previous, newState);
        }
    }
}
