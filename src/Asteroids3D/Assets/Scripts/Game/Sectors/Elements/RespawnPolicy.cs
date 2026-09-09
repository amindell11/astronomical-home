using System;
using UnityEngine;

namespace Game.Sectors.Elements
{
    /// <summary>
    /// Producer-owned respawn rule, authored beside the spawn/adopt that creates a ship. Carried by
    /// <see cref="RingSpawner"/> (revives its products), <see cref="AdoptEntry"/> (revives an adopted
    /// ship), and the game driver (revives the player). <see cref="Respawn.Wire"/> turns it into an
    /// <c>OnDeath → WaitAndRespawnShip</c> subscription. Revive at a random point within
    /// <see cref="radius"/> of the resolved anchor, after <see cref="delay"/> seconds. For
    /// <see cref="Origin.FixedPoint"/> the anchor is producer-relative: the producer's position snapshotted
    /// at spawn time plus <see cref="point"/> as an offset — so a default (zero) point revives the ship
    /// exactly where it started.
    /// </summary>
    [Serializable]
    public struct RespawnPolicy
    {
        public enum Origin
        {
            /// <summary>No respawn (ship is not revived on death).</summary>
            None,
            /// <summary>Revive at the producer's spawn position offset by <see cref="point"/>.</summary>
            FixedPoint,
            /// <summary>Revive near the world follower (the player anchor) at death time.</summary>
            FollowerRelative,
        }

        [Tooltip("None disables respawn. FixedPoint revives at the producer's spawn spot + 'point'. FollowerRelative revives near the world follower (player anchor).")]
        public Origin origin;

        [Tooltip("Plane-space offset from the producer's spawn position for FixedPoint (zero = revive where it started). Ignored for FollowerRelative.")]
        public Vector2 point;

        [Tooltip("Random radius (plane units) around the resolved anchor.")]
        public float radius;

        [Tooltip("Delay in seconds before the ship revives after death.")]
        public float delay;

        /// <summary>True if this policy actually revives (origin is not None).</summary>
        public bool Enabled => origin != Origin.None;
    }
}
