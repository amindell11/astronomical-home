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

    /// <summary><see cref="Lost"/> is a lost track; <see cref="Ended"/> is any other end of a tracking flight.</summary>
    public enum TrackChange { Added, Lost, Ended }

    /// <summary>
    /// One target's missile-lock events, raised by whoever locks it, and the count of missiles
    /// tracking it, kept by those missiles. It lives as long as the target, so the count stays
    /// right while a lock reticle bound to it is disabled through death and respawn.
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

        public int TrackingCount { get; private set; }

        public event Action<TrackChange> TrackingChanged;

        public void AddTrack() => ChangeTracking(1, TrackChange.Added);

        public void LoseTrack() => ChangeTracking(-1, TrackChange.Lost);

        public void EndTrack() => ChangeTracking(-1, TrackChange.Ended);

        private void ChangeTracking(int delta, TrackChange change)
        {
            TrackingCount += delta;
            TrackingChanged?.Invoke(change);
        }
    }
}
