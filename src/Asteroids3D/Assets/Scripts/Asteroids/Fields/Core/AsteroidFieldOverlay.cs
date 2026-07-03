using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Fields.Core
{
    /// <summary>
    /// A fragment (or other authored asteroid) persisted by the overlay: full
    /// pose + identity, because it has no LUT home to regenerate from.
    /// Snapshotted at rest on chunk unload; velocity/spin persistence is a
    /// deferred follow-up (see the feature plan).
    /// </summary>
    [Serializable]
    public struct AuthoredAsteroid
    {
        public AsteroidId Id;
        public Vector2 PlanePosition;
        public Quaternion Rotation;
        public int MeshIndex;
        public float Mass;
        public float Scale;
        public float HealthFraction;
    }

    /// <summary>
    /// The sparse override layer: only deviations from the baseline LUT are
    /// stored, keyed by stable ID. Session-only and in-memory today, but kept
    /// a plain serializable POCO so disk saves can serialize it later.
    /// </summary>
    public class AsteroidFieldOverlay
    {
        /// <summary>Destroyed baseline IDs — skipped on load.</summary>
        public readonly HashSet<AsteroidId> Tombstones = new();

        /// <summary>Authored asteroids (fragments) keyed by their authored ID.</summary>
        public readonly Dictionary<AsteroidId, AuthoredAsteroid> Authored = new();

        /// <summary>Baseline asteroids shot-but-alive: remaining health fraction in (0, 1).</summary>
        public readonly Dictionary<AsteroidId, float> DamagedHealth = new();

        public bool IsTombstoned(AsteroidId id) => Tombstones.Contains(id);

        /// <summary>Marks a baseline asteroid destroyed; its damage entry becomes moot.</summary>
        public void Tombstone(AsteroidId id)
        {
            Tombstones.Add(id);
            DamagedHealth.Remove(id);
        }

        public void RecordDamage(AsteroidId id, float healthFraction)
        {
            if (healthFraction >= 1f) DamagedHealth.Remove(id);
            else DamagedHealth[id] = healthFraction;
        }

        public void WriteAuthored(in AuthoredAsteroid entry) => Authored[entry.Id] = entry;

        /// <summary>A destroyed authored asteroid simply leaves the overlay.</summary>
        public void RemoveAuthored(AsteroidId id) => Authored.Remove(id);

        public void Clear()
        {
            Tombstones.Clear();
            Authored.Clear();
            DamagedHealth.Clear();
        }
    }
}
