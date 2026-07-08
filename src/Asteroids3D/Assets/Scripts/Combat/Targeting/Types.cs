using System;
using Combat.Weapons;
using UnityEngine;

namespace Combat.Targeting
{
    public enum LockState { Idle, Locking, Locked, Cooldown }

    public interface ILockProvider
    {
        LockState State { get; }
        ITargetable ConsumeLock();
    }

    /// <summary>Lock state as displayable weapon state: a lock readout on the owning weapon's HUD panel.</summary>
    public interface ILockStateSource : IWeaponReadout
    {
        LockState State { get; }
        event Action<LockState, LockState> OnStateChanged;
    }

    /// <summary>
    /// Marker interface for anything a missile can chase.
    /// Ships, Asteroids, etc. should implement this.
    /// </summary>
    public interface ITargetable
    {
        /// <summary>The point that missiles should aim for on this target.</summary>
        Transform TargetPoint { get; }

        /// <summary>Per-target lock channel that components can subscribe to or invoke.</summary>
        LockChannel Lock { get; }
    }

    /// <summary>
    /// Lightweight container that holds delegates related to missile lock-on events for a single target.
    /// Components may freely subscribe (+=) or invoke (?.Invoke) these delegates.
    /// </summary>
    public sealed class LockChannel
    {
        /// <summary>Called every frame while a lock is building. Parameter: progress [0-1].</summary>
        public event Action<float> Progress;

        /// <summary>Called once when lock acquisition completes.</summary>
        public event Action Acquired;

        /// <summary>Called when a lock is cancelled, expired, or the missile is launched.</summary>
        public event Action Released;

        public void RaiseProgress(float value)
        {
            Progress?.Invoke(value);
        }

        public void RaiseAcquired()
        {
            Acquired?.Invoke();
        }

        public void RaiseReleased()
        {
            Released?.Invoke();
        }
    }
}
