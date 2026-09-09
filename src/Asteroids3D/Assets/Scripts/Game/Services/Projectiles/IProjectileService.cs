using System;
using UnityEngine;

namespace Game.Services.Projectiles
{
    /// <summary>Live-transient registry for damage-dealing pooled objects (projectiles, concussion waves): the arena-scoped answer to "what's checked out right now", so episode resets and sector transitions flush in-flight transients without scanning the scene. Registrants never hold the service — the firing weapon registers them, and their own return-to-pool events deregister them.</summary>
    public interface IProjectileService
    {
        /// <summary>Track a live instance with the action that returns it to its pool through its domain return path (never a raw pool release).</summary>
        void Register(MonoBehaviour instance, Action returnToPool);

        /// <summary>Return every tracked instance to its pool (episode reset / sector transition).</summary>
        void ReturnAllToPool();

        /// <summary>Number of live tracked instances.</summary>
        int ActiveCount { get; }

        /// <summary>Read-only visit of every live tracked instance (diagnostics overlays).</summary>
        void ForEachLive(Action<MonoBehaviour> visit);
    }
}
