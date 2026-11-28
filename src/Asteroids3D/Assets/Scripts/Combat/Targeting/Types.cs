using System;
using System.Collections.Generic;
using Combat.Weapons;
using Movement;
using UnityEngine;

namespace Combat.Targeting
{
    public enum LockState { Idle, Locking, Locked, Cooldown }

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

    public interface ITrackingMissile
    {
        Kinematics Kinematics { get; }
    }

    public interface ICarriedCharge
    {
        /// <summary>0 when the charge sticks, 1 as its fuse runs out.</summary>
        float FuseProgress { get; }

        event Action Ended;
    }

    /// <summary>
    /// One target's missile-lock events, raised by whoever locks it, the missiles tracking it,
    /// kept by those missiles, the charges that stick to it, announced by each charge, its
    /// concealment, held by the target's own hiding parts, and when it was last sighted, stamped
    /// by whoever sees it. It lives as long as the target.
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

        /// <summary>Raised the moment the target conceals itself; locks and seekers drop it on the spot.</summary>
        public event Action Concealed;

        private int concealers;

        public bool IsConcealed => concealers > 0;

        public float LastSightedAt { get; private set; } = float.NegativeInfinity;

        /// <summary>Each part that hides the target conceals once and reveals once; it shows when the last lets go.</summary>
        public void Conceal()
        {
            if (concealers++ > 0) return;
            Concealed?.Invoke();
        }

        public void Reveal()
        {
            if (concealers == 0)
                throw new InvalidOperationException("Reveal without a matching Conceal: every hiding part reveals only what it concealed.");
            concealers--;
        }

        public void NoteSighted(float time) => LastSightedAt = time;

        /// <summary>Raised when a charge sticks to the target; it rides until its <see cref="ICarriedCharge.Ended"/>.</summary>
        public event Action<ICarriedCharge> Carried;

        public void Carry(ICarriedCharge charge) => Carried?.Invoke(charge);

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
