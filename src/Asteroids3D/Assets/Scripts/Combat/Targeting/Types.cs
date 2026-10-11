using System;
using System.Collections.Generic;
using Combat.Weapons;
using Movement;
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

    /// <summary>A missile's plane motion, as read by the ship it tracks.</summary>
    public interface ITrackingMissile
    {
        Kinematics Kinematics { get; }
    }

    /// <summary>
    /// One target's missile-lock events, raised by whoever locks it, and the missiles tracking it,
    /// kept by those missiles. It lives as long as the target.
    /// </summary>
    public sealed class LockChannel
    {
        private readonly List<ITrackingMissile> trackers = new();

        public IReadOnlyList<ITrackingMissile> Trackers => trackers;

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

        public void AddTracker(ITrackingMissile missile) => trackers.Add(missile);

        public void RemoveTracker(ITrackingMissile missile) => trackers.Remove(missile);
    }
}
