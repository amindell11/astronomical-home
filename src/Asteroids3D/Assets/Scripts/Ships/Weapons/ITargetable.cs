using System;
using UnityEngine;

namespace Weapons
{
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
    /// Lightweight container that holds delegates related to missile lock‐on events for a single target.
    /// Components may freely subscribe (+=) or invoke (?.Invoke) these delegates.
    /// </summary>
    public sealed class LockChannel
    {
        /// <summary>Called every frame while a lock is building. Parameter: progress [0–1].</summary>
        public Action<float> Progress;

        /// <summary>Called once when lock acquisition completes.</summary>
        public Action Acquired;

        /// <summary>Called when a lock is cancelled, expired, or the missile is launched.</summary>
        public Action Released;
    }
}