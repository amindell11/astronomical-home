using System;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// Producer-owned respawn rule, authored beside the spawn/adopt that creates a ship. Carried by
    /// <see cref="RingSpawner"/> (revives its products), <see cref="AdoptEntry"/> (revives an adopted
    /// ship), and the session-tier <c>PlayerRig</c> (revives the player). <see cref="Respawn.Wire"/> turns it into an
    /// <c>OnDeath → WaitAndRespawnShip</c> subscription. Reproduces the inline respawn math the
    /// sector subclasses used: revive at a random point within <see cref="radius"/> of the resolved
    /// anchor, after <see cref="delay"/> seconds.
    /// </summary>
    [Serializable]
    public struct RespawnPolicy
    {
        public enum Origin
        {
            /// <summary>No respawn (ship is not revived on death).</summary>
            None,
            /// <summary>Revive near a fixed plane-space <see cref="point"/>.</summary>
            FixedPoint,
            /// <summary>Revive near the world follower (the player anchor) at death time.</summary>
            FollowerRelative,
        }

        [Tooltip("None disables respawn. FixedPoint revives near 'point'. FollowerRelative revives near the world follower (player anchor).")]
        public Origin origin;

        [Tooltip("Plane-space anchor for FixedPoint (ignored for FollowerRelative).")]
        public Vector2 point;

        [Tooltip("Random radius (plane units) around the resolved anchor.")]
        public float radius;

        [Tooltip("Delay in seconds before the ship revives after death.")]
        public float delay;

        /// <summary>True if this policy actually revives (origin is not None).</summary>
        public bool Enabled => origin != Origin.None;
    }
}
